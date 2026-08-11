using System.Text.Json;
using System.Text.Json.Serialization;

namespace Descartes.CertaintyLab.ThoughtCompanion;

public sealed record CompanionValidationResult(CompanionAnswer? Answer, string? FailureCode)
{
    public bool IsValid => Answer is not null;
}

public sealed class CompanionResponseValidator
{
    public const int MaxJsonCharacters = 65_536;
    public const int MaxHeardCharacters = 2_048;
    public const int MaxQuestionCharacters = 1_024;
    public const int MaxRelationCharacters = 2_048;
    public const int MaxBasisLabelCharacters = 8;
    public const int MaxSourceCount = 12;
    public const int MaxSourceIdCharacters = 128;
    public const int MaxVoiceCharacters = 32;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    private static readonly HashSet<string> AnswerFields =
        new(["heard", "question", "relation", "basisLabel", "sources"], StringComparer.Ordinal);

    private static readonly HashSet<string> SourceFields =
        new(["claimId", "evidenceId", "voice"], StringComparer.Ordinal);

    public CompanionValidationResult Validate(string json, CompanionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (json is null)
        {
            return InvalidJson();
        }

        if (json.Length > MaxJsonCharacters)
        {
            return TooLarge();
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            return InvalidJson();
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return InvalidJson();
            }

            if (!HasExactProperties(document.RootElement, AnswerFields) ||
                !HasString(document.RootElement, "heard") ||
                !HasString(document.RootElement, "question") ||
                !HasString(document.RootElement, "relation") ||
                !HasString(document.RootElement, "basisLabel") ||
                !document.RootElement.TryGetProperty("sources", out JsonElement sourceElements) ||
                sourceElements.ValueKind != JsonValueKind.Array ||
                sourceElements.GetArrayLength() == 0)
            {
                return SchemaFailure();
            }

            if (sourceElements.GetArrayLength() > MaxSourceCount ||
                IsStringOverLimit(document.RootElement, "heard", MaxHeardCharacters) ||
                IsStringOverLimit(document.RootElement, "question", MaxQuestionCharacters) ||
                IsStringOverLimit(document.RootElement, "relation", MaxRelationCharacters) ||
                IsStringOverLimit(document.RootElement, "basisLabel", MaxBasisLabelCharacters))
            {
                return TooLarge();
            }

            foreach (JsonElement sourceElement in sourceElements.EnumerateArray())
            {
                if (sourceElement.ValueKind != JsonValueKind.Object ||
                    !HasExactProperties(sourceElement, SourceFields) ||
                    !HasString(sourceElement, "claimId") ||
                    !HasString(sourceElement, "evidenceId") ||
                    !HasString(sourceElement, "voice"))
                {
                    return SchemaFailure();
                }

                if (IsStringOverLimit(sourceElement, "claimId", MaxSourceIdCharacters) ||
                    IsStringOverLimit(sourceElement, "evidenceId", MaxSourceIdCharacters) ||
                    IsStringOverLimit(sourceElement, "voice", MaxVoiceCharacters))
                {
                    return TooLarge();
                }
            }
        }

        WireAnswer? wire;
        try
        {
            wire = JsonSerializer.Deserialize<WireAnswer>(json, Options);
        }
        catch (JsonException)
        {
            return SchemaFailure();
        }

        if (wire is null ||
            string.IsNullOrWhiteSpace(wire.Heard) ||
            string.IsNullOrWhiteSpace(wire.Question) ||
            string.IsNullOrWhiteSpace(wire.Relation) ||
            wire.Sources is null ||
            wire.Sources.Count == 0 ||
            !TryBasis(wire.BasisLabel, out CompanionBasisLabel basis))
        {
            return SchemaFailure();
        }

        string question = wire.Question.Trim();
        if (question.Count(character => character is '?' or '？') != 1 ||
            question[^1] is not ('?' or '？'))
        {
            return new(null, "question-count");
        }

        var sources = new List<CompanionSource>(wire.Sources.Count);
        var sourceKeys = new HashSet<(string ClaimId, string EvidenceId, CompanionVoice Voice)>();
        foreach (WireSource? source in wire.Sources)
        {
            if (source is null)
            {
                return SchemaFailure();
            }

            if (string.IsNullOrWhiteSpace(source.ClaimId) ||
                string.IsNullOrWhiteSpace(source.EvidenceId) ||
                string.IsNullOrWhiteSpace(source.Voice) ||
                !context.ClaimIds.Contains(source.ClaimId) ||
                !context.EvidenceIds.Contains(source.EvidenceId) ||
                !TryVoice(source.Voice, out CompanionVoice voice) ||
                !context.Claims.Any(claim =>
                    claim.Id == source.ClaimId &&
                    claim.Voice == voice &&
                    claim.Evidence.Any(evidence => evidence.Id == source.EvidenceId)) ||
                !sourceKeys.Add((source.ClaimId, source.EvidenceId, voice)))
            {
                return new(null, "source-reference");
            }

            sources.Add(new CompanionSource(source.ClaimId, source.EvidenceId, voice));
        }

        return new(
            new CompanionAnswer(
                wire.Heard.Trim(),
                question,
                wire.Relation.Trim(),
                basis,
                sources.AsReadOnly()),
            null);
    }

    private static bool HasExactProperties(JsonElement element, IReadOnlySet<string> expected)
    {
        var actual = new HashSet<string>(StringComparer.Ordinal);
        int count = 0;
        foreach (JsonProperty property in element.EnumerateObject())
        {
            count++;
            if (!actual.Add(property.Name))
            {
                return false;
            }
        }

        return count == expected.Count && actual.SetEquals(expected);
    }

    private static bool HasString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out JsonElement value) &&
        value.ValueKind == JsonValueKind.String;

    private static bool IsStringOverLimit(JsonElement element, string propertyName, int maximum) =>
        element.GetProperty(propertyName).GetString()!.Length > maximum;

    private static bool TryBasis(string? value, out CompanionBasisLabel basis)
    {
        basis = value switch
        {
            "资料支持" => CompanionBasisLabel.资料支持,
            "具名解释" => CompanionBasisLabel.具名解释,
            "现代重构" => CompanionBasisLabel.现代重构,
            "AI 提问" => CompanionBasisLabel.AI提问,
            _ => default
        };

        return value is "资料支持" or "具名解释" or "现代重构" or "AI 提问";
    }

    private static bool TryVoice(string value, out CompanionVoice voice)
    {
        voice = value switch
        {
            "SourceSupported" => CompanionVoice.SourceSupported,
            "NamedInterpretation" => CompanionVoice.NamedInterpretation,
            "ModernReconstruction" => CompanionVoice.ModernReconstruction,
            "AiQuestion" => CompanionVoice.AiQuestion,
            _ => default
        };

        return value is "SourceSupported" or "NamedInterpretation" or "ModernReconstruction" or "AiQuestion";
    }

    private static CompanionValidationResult InvalidJson() => new(null, "invalid-json");

    private static CompanionValidationResult SchemaFailure() => new(null, "schema");

    private static CompanionValidationResult TooLarge() => new(null, "response-too-large");

    private sealed record WireAnswer(
        [property: JsonPropertyName("heard")] string? Heard,
        [property: JsonPropertyName("question")] string? Question,
        [property: JsonPropertyName("relation")] string? Relation,
        [property: JsonPropertyName("basisLabel")] string? BasisLabel,
        [property: JsonPropertyName("sources")] IReadOnlyList<WireSource?>? Sources);

    private sealed record WireSource(
        [property: JsonPropertyName("claimId")] string? ClaimId,
        [property: JsonPropertyName("evidenceId")] string? EvidenceId,
        [property: JsonPropertyName("voice")] string? Voice);
}

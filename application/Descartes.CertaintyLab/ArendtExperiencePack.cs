using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Descartes.CertaintyLab;

public sealed record ArendtExperiencePack(
    string Id,
    string Title,
    string Opening,
    string Prompt,
    IReadOnlyList<ArendtStartingPointDefinition> StartingPoints,
    string OwnThoughtPrompt,
    string ReplySceneText,
    string OwnThoughtReplyText,
    string BoundarySceneText,
    string BoundaryDiscovery,
    string CompletionIdentityText,
    string CompletionText,
    string InterpretationText,
    string BoundaryText,
    string CausalBoundary,
    IReadOnlyList<LifeAndThoughtStepDefinition> LifeAndThoughtSteps,
    ArendtSourceNoteDefinition SourceNote)
{
    private static readonly string[] RequiredRecordIds =
    [
        "K-HA-R03-ARREST-FLIGHT",
        "K-HA-R05-INTERNMENT-US",
        "K-HA-R48-STATELESSNESS-RIGHTS",
    ];

    public static ArendtExperiencePack Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        };

        ArendtExperiencePack pack =
            JsonSerializer.Deserialize<ArendtExperiencePack>(stream, options)
            ?? throw new InvalidDataException("The Arendt experience pack is empty.");

        Validate(pack);
        return pack;
    }

    private static void Validate(ArendtExperiencePack pack)
    {
        EnsureText(pack.Title, "title");
        EnsureText(pack.Opening, "opening");
        EnsureText(pack.Prompt, "prompt");
        EnsureText(pack.CausalBoundary, "causal boundary");
        EnsureText(pack.CompletionIdentityText, "completion identity");
        EnsureText(pack.CompletionText, "completion text");
        EnsureText(pack.InterpretationText, "interpretation");
        EnsureText(pack.BoundaryText, "boundary");

        if (pack.StartingPoints.Count != 4)
        {
            throw new InvalidDataException(
                "The Arendt experience must offer four ways forward, including uncertainty.");
        }

        EnsureUnique(pack.StartingPoints.Select(item => item.Id), "starting point");
        if (pack.StartingPoints.Any(item =>
                string.IsNullOrWhiteSpace(item.Text) ||
                string.IsNullOrWhiteSpace(item.Consequence) ||
                string.IsNullOrWhiteSpace(item.PathSummary)))
        {
            throw new InvalidDataException(
                "Every starting point needs natural text, a consequence, and a path summary.");
        }

        if (pack.LifeAndThoughtSteps.Count != 3)
        {
            throw new InvalidDataException(
                "The life-and-thought bridge must contain exactly three careful steps.");
        }

        EnsureUnique(pack.LifeAndThoughtSteps.Select(item => item.Heading), "life step");
        if (pack.LifeAndThoughtSteps.Any(item =>
                string.IsNullOrWhiteSpace(item.Body) ||
                string.IsNullOrWhiteSpace(item.EvidenceKind)))
        {
            throw new InvalidDataException(
                "Every life-and-thought step needs a body and evidence kind.");
        }

        EnsureUnique(pack.SourceNote.RecordIds, "source record");
        if (RequiredRecordIds.Any(required =>
                !pack.SourceNote.RecordIds.Contains(required, StringComparer.Ordinal)))
        {
            throw new InvalidDataException(
                "The source note is missing a required Arendt record.");
        }

        EnsureText(pack.SourceNote.DisplayText, "source note");
    }

    private static void EnsureText(string value, string kind)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException($"The {kind} cannot be empty.");
        }
    }

    private static void EnsureUnique(IEnumerable<string> values, string kind)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string value in values)
        {
            if (string.IsNullOrWhiteSpace(value) || !seen.Add(value))
            {
                throw new InvalidDataException(
                    $"Duplicate or empty {kind} value '{value}'.");
            }
        }
    }
}

public sealed record ArendtStartingPointDefinition(
    string Id,
    string Text,
    string Consequence,
    string PathSummary);

public sealed record LifeAndThoughtStepDefinition(
    string Heading,
    string Body,
    string EvidenceKind);

public sealed record ArendtSourceNoteDefinition(
    string DisplayText,
    IReadOnlyList<string> RecordIds,
    IReadOnlyList<string> OpenBoundaryIds);


using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Descartes.CertaintyLab;

public enum KnowledgeNodeStatus
{
    Draft,
    TeachingReady,
}

public sealed class KnowledgeNodeStatusJsonConverter
    : JsonConverter<KnowledgeNodeStatus>
{
    private readonly bool allowAuthoringCandidate;

    public KnowledgeNodeStatusJsonConverter(bool allowAuthoringCandidate = false) =>
        this.allowAuthoringCandidate = allowAuthoringCandidate;

    public override KnowledgeNodeStatus Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        string value = reader.TokenType == JsonTokenType.String
            ? reader.GetString() ?? throw new JsonException("知识节点状态为空。")
            : throw new JsonException("知识节点状态必须是字符串。");
        return value switch
        {
            "draft" => KnowledgeNodeStatus.Draft,
            "teachingReady" or "ready-for-review" =>
                KnowledgeNodeStatus.TeachingReady,
            "authoringCandidate" when allowAuthoringCandidate =>
                KnowledgeNodeStatus.TeachingReady,
            _ => throw new JsonException($"未知知识节点状态“{value}”。"),
        };
    }

    public override void Write(
        Utf8JsonWriter writer,
        KnowledgeNodeStatus value,
        JsonSerializerOptions options) =>
        writer.WriteStringValue(value switch
        {
            KnowledgeNodeStatus.Draft => "draft",
            KnowledgeNodeStatus.TeachingReady => "teachingReady",
            _ => throw new JsonException($"未知知识节点状态“{value}”。"),
        });
}

public enum KnowledgeNodeKind
{
    Question,
    Concept,
    Claim,
    ArgumentStep,
    Conclusion,
    Warning,
    Controversy,
    Reception,
}

public sealed class KnowledgeNodeKindJsonConverter
    : JsonConverter<KnowledgeNodeKind>
{
    public override KnowledgeNodeKind Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        string value = reader.TokenType == JsonTokenType.String
            ? reader.GetString() ?? throw new JsonException("知识节点类型为空。")
            : throw new JsonException("知识节点类型必须是字符串。");
        return value switch
        {
            "question" => KnowledgeNodeKind.Question,
            "concept" => KnowledgeNodeKind.Concept,
            "claim" or "paragraph-claim" or "philosopher-longform" =>
                KnowledgeNodeKind.Claim,
            "argumentStep" => KnowledgeNodeKind.ArgumentStep,
            "conclusion" => KnowledgeNodeKind.Conclusion,
            "warning" => KnowledgeNodeKind.Warning,
            "controversy" => KnowledgeNodeKind.Controversy,
            "reception" => KnowledgeNodeKind.Reception,
            _ => throw new JsonException($"未知知识节点类型“{value}”。"),
        };
    }

    public override void Write(
        Utf8JsonWriter writer,
        KnowledgeNodeKind value,
        JsonSerializerOptions options) =>
        writer.WriteStringValue(value switch
        {
            KnowledgeNodeKind.Question => "question",
            KnowledgeNodeKind.Concept => "concept",
            KnowledgeNodeKind.Claim => "claim",
            KnowledgeNodeKind.ArgumentStep => "argumentStep",
            KnowledgeNodeKind.Conclusion => "conclusion",
            KnowledgeNodeKind.Warning => "warning",
            KnowledgeNodeKind.Controversy => "controversy",
            KnowledgeNodeKind.Reception => "reception",
            _ => throw new JsonException($"未知知识节点类型“{value}”。"),
        });
}

public enum LessonSectionKind
{
    Unknown,
    Problem,
    Concept,
    Argument,
    Case,
    Counterexample,
    CloseReading,
    Boundary,
    Controversy,
    Connection,
    Bridge,
    Comparison,
    Transfer,
    Recap,
}

public sealed class LessonSectionKindJsonConverter
    : JsonConverter<LessonSectionKind>
{
    private static readonly HashSet<string> PublishedAliases =
        """
abductive-role
abhidharma-external-voice
abstraction-defense
abstraction-pressure
access-argument
access-question
acquisition
act-content-distinction
alternative-foundation
analogy-analysis
analogy-limit
analysis
anti-atomism
anti-psychologism
antisemitic-harm
apophatic-boundary
application
archival-analysis
archival-conflict
archive-conflict
argument
argument-analysis
argument-boundary
argumentative-role
assertoric-force
assertoric-role
astronomical-model
attribute-derivation
attribution-boundary
audience-analysis
axiom-analysis
biographical-boundary
boundary
boundary-and-pressure-test
bounded-conclusion
bounded-response
bridge
caesar-objection
capacity-analysis
case
case-analysis
case-evaluation
category-analysis
causal-analysis
causal-audit
causal-boundary
causal-distinction
causal-explanation
causal-map
causal-qualification
causal-relation
charitable-objection
chronological-boundary
chronological-synthesis
chronological-tension
chronology
classification
classification-limit
clinical-analysis
clinical-boundary
clinical-conflict
clinical-record
close
closeReading
cognitive-objection
cognitive-puzzle
collective-practice
collective-record
communal-practice
comparative-analysis
comparative-boundary
comparative-diagnosis
comparative-history
comparative-method
comparative-transition
comparison
compositional-argument
concept
concept-analysis
conceptual-analysis
conceptual-argument
conceptual-distinction
conceptual-entry
conceptual-revision
conditional-analysis
connection
constraint-and-objection
contextual-limit
controversy
correspondence-event
counterargument
counterexample
critical-analysis
critical-reconstruction
cross-genre-synthesis
cross-genre-transition
cultural-history
curriculum
deductive-role
deliberation
demonstration
dependency-analysis
developmental-analysis
developmental-hypothesis
diagrammatic-syntax
dialectical-comparison
dialectical-turn
difference-within-continuity
distinction
document-analysis
downstream-cost
dramatic-analysis
editorial-boundary
editorial-chronology
editorial-layer
embodied-objection
embodied-operation
emergency-assessment
emergency-power-analysis
epistemic-analysis
epistemic-cancellation
epistemic-objection
epistemic-pressure
epistemic-relation
epistolary-analysis
ethical-transition
evaluative-synthesis
evidence
evidence-boundary
evidential-limits
evolutionary-cosmology
explanatory-boundary
expressive-contrast
faculty-map
failure-analysis
fallibilist-limit
formal-innovation
forward-link
four-debts
fragment-objection
freedom-case
functional-distinction
gendered-evidence-gap
genealogy
genre-analysis
global-verdict
higher-order-response
historical-analysis
historical-assessment
historical-boundary
historical-case-analysis
historical-context
historical-development
historical-objection
historical-origin
historical-problem
historical-reconstruction
historical-test
historical-thesis
historical-turn
historical-verdict
historiographical-analysis
hypothesis-selection
identity-context
identity-debt
identity-objection
imperial-assessment
inductive-qualification
inductive-role
inferential-sequence
inferential-transition
inquiry-cycle
inquiry-sequence
institutional-analysis
institutional-assessment
institutional-counterweight
institutional-distinction
institutional-limit
instrumental-evidence
intellect-stages
international-analysis
interpretant-distinctions
interpretant-relation
interpretation
interpretive-authority
interpretive-boundary
intertextual-analysis
iteration-problem
james-reception
kantian-dispute
knowledge-action-gap
knowledge-problem
later-development
later-metatheory
later-receptions
law-practice
leadership-analysis
legitimacy-boundary
letter-evidence
limit
linguistic-medium
local-boundary
logical-function
madhyamaka-external-voice
manuscript-status
mathematical-background
mathematical-model
means-end-boundary
means-selection
mechanism
mediated-comparison
medium-limitation
membership-analysis
merit
metalanguage-objection
metalinguistic-objection
metaphysical-distinction
metaphysical-transition
method
methodological-analysis
methodological-assessment
methodological-principle
methodological-qualification
mixed-sign-analysis
modern-boundary
modern-objection
modern-objection-field
modern-reconstruction
modern-result
named-reception-history
narrative-analysis
network-comparison
non-excuse-analysis
natural-continuous-argument
normative-analysis
normative-audit
normative-boundary
normative-effect
normative-epistemic-limit
normative-horizon
normative-objection
normative-order
normative-question
normative-reception-boundary
normative-scope
normative-standard
normative-tension
normative-transition
object-distinctions
object-relation
objecthood-pressure
objection
objection-analysis
objection-pair
objection-response
objective-idealism
objectivity-question
ontological-objection
ontology
open-problem
open-question
organism-analogy
organizational-analysis
paradox-reconstruction
paradox-risk
paradox-source
perceptual-entry
phenomenology
philological-boundary
plural-realization
plural-reception
political-communication
political-critique
political-economy
political-objection
political-order
political-rhetoric
political-rights-harm
political-use
practical-boundary
practical-domain
practical-limits
pragmatic-debt
pragmaticist-qualification
pressure
primary-normative-analysis
primary-text
probabilistic-model
problem
problem-framing
problem-pressure
programmatic-argument
proof-audit
psycho-social-analysis
public-consequence
realism-of-generals
recap
reception
reception-analysis
reception-boundary
reception-conflict
reception-history
reception-objection
reception-problem
reception-source-critique
reception-synthesis
reconstruction-boundary
recorded-opponent-analysis
regime-typology
relational-classification
renunciation-research-question
repair-goals
research-allocation
restricted-verdict
rhetorical-analysis
rhetorical-assessment
rhythmic-analysis
rival-theories
ruler-knowledge
scholarly-debate
scope-and-debt
scope-audit
scope-boundary
scope-limitation
second-level-analysis
semantic-analysis
semantic-distinction
semantic-mechanism
semantic-question
semiosis-process
semiotic-account
shared-pressure
skeptical-objection
social-analysis
soteriological-boundary
source-comparison
source-criticism
source-dialogue
source-evaluation
source-frame
source-layer
source-stratification
stability-limit
strategic-conflict
strong-objection
substitution-failure
succession-problem
support-and-objection
symbolic-translation
synthesis
synthetic-analysis
system-critique
system-dependency
system-pressure
systematic-role
technical-analysis
terminological-history
testing-boundary
text-identity
textual-analysis
textual-attribution
textual-criticism
textual-evidence
textual-identity
textual-sequence
textual-stage
theological-distinction
theoretical-reconstruction
timeline-boundary
transfer
transformational-inference
transition
translation-boundary
translation-institution
translation-problem
translation-test
transmission-evidence
truth-objection
tychistic-hypothesis
unity-problem
unpaid-debt
unpaid-explanatory-debt
value-range-role
virtue-formation
voice-analysis
voice-boundary
voice-conflict
voice-layering
voice-synthesis
yogacara-external-voice
""".Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries).ToHashSet(
                StringComparer.Ordinal);

    public override LessonSectionKind Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        string value = reader.GetString() ??
            throw new JsonException("正文部分类型为空。");
        LessonSectionKind? canonical = value switch
        {
            "problem" => LessonSectionKind.Problem,
            "concept" or "conceptual-analysis" => LessonSectionKind.Concept,
            "argument" or "analysis" or "causal-analysis" or "mechanism" or
                "normative-analysis" => LessonSectionKind.Argument,
            "case" or "case-analysis" => LessonSectionKind.Case,
            "counterexample" => LessonSectionKind.Counterexample,
            "closeReading" or "evidence" or "textual-analysis" or
                "voice-analysis" => LessonSectionKind.CloseReading,
            "boundary" => LessonSectionKind.Boundary,
            "controversy" or "objection-response" or
                "system-critique" => LessonSectionKind.Controversy,
            "connection" or "reception-history" =>
                LessonSectionKind.Connection,
            "bridge" => LessonSectionKind.Bridge,
            "comparison" or "comparative-analysis" =>
                LessonSectionKind.Comparison,
            "transfer" => LessonSectionKind.Transfer,
            "recap" => LessonSectionKind.Recap,
            _ => null,
        };
        if (canonical is not null)
        {
            return canonical.Value;
        }
        if (!PublishedAliases.Contains(value))
        {
            throw new JsonException($"未知正文部分类型“{value}”。");
        }
        return MapPublishedAlias(value);
    }

    private static LessonSectionKind MapPublishedAlias(string alias)
    {
        if (ContainsAny(alias, "boundary", "limit", "limitation",
                "qualification", "scope", "debt", "status"))
        {
            return LessonSectionKind.Boundary;
        }
        if (alias.StartsWith("anti-", StringComparison.Ordinal) ||
            alias.EndsWith("-harm", StringComparison.Ordinal) ||
            ContainsAny(alias, "objection", "conflict", "critique", "debate",
                "dispute", "counterargument"))
        {
            return LessonSectionKind.Controversy;
        }
        if (ContainsAny(alias, "counterexample", "failure"))
        {
            return LessonSectionKind.Counterexample;
        }
        if (ContainsAny(alias, "problem", "question", "pressure", "puzzle",
                "risk", "gap", "tension"))
        {
            return LessonSectionKind.Problem;
        }
        if (alias is "translation-institution" ||
            ContainsAny(alias, "reception", "history", "historical-development",
                "historical-origin", "genealogy", "later-development",
                "transmission"))
        {
            return LessonSectionKind.Connection;
        }
        if (ContainsAny(alias, "comparison", "comparative", "contrast",
                "difference-within"))
        {
            return LessonSectionKind.Comparison;
        }
        if (ContainsAny(alias, "transition", "turn", "forward-link"))
        {
            return LessonSectionKind.Bridge;
        }
        if (ContainsAny(alias, "case", "analogy", "application"))
        {
            return LessonSectionKind.Case;
        }
        if (ContainsAny(alias, "textual", "text-", "primary-text", "source",
                "evidence", "voice", "archiv", "record", "document",
                "editorial", "epistolary", "letter", "manuscript",
                "philological", "interpret", "genre", "narrative",
                "rhetorical", "audience", "chronolog", "attribution",
                "correspondence"))
        {
            return LessonSectionKind.CloseReading;
        }
        if (ContainsAny(alias, "synthesis", "conclusion", "verdict", "close"))
        {
            return LessonSectionKind.Recap;
        }
        if (alias is "collective-practice" or "communal-practice" or
            "law-practice" or "deliberation" or "political-use" or
            "research-allocation" or "virtue-formation" or "acquisition")
        {
            return LessonSectionKind.Transfer;
        }
        if (ContainsAny(alias, "distinction", "classification", "typology",
                "ontology", "phenomenology", "model", "map", "syntax",
                "account", "stages", "domain", "medium", "object-relation",
                "objectivity", "object-distinctions"))
        {
            return LessonSectionKind.Concept;
        }
        return LessonSectionKind.Argument;
    }

    private static bool ContainsAny(string value, params string[] fragments) =>
        fragments.Any(fragment =>
            value.Contains(fragment, StringComparison.Ordinal));

    public override void Write(
        Utf8JsonWriter writer,
        LessonSectionKind value,
        JsonSerializerOptions options) =>
        writer.WriteStringValue(
            JsonNamingPolicy.CamelCase.ConvertName(value.ToString()));
}

public sealed record KnowledgeNodeDefinition(
    string Id,
    KnowledgeNodeKind Kind,
    KnowledgeNodeStatus Status,
    string ReaderTitle,
    string Explanation,
    string Identity,
    IReadOnlyList<string> EvidenceLinkIds,
    IReadOnlyList<string> AbilityIds,
    JsonElement? AttributionProfile = null,
    string? BindingMode = null,
    IReadOnlyList<string>? ClaimObjectKeys = null,
    IReadOnlyList<string>? Countries = null,
    string? Country = null,
    JsonElement? EvidenceCitations = null,
    string? Label = null,
    bool? NamedAttribution = null,
    string? ObjectKey = null,
    int? ParagraphIndex = null,
    int? SectionIndex = null,
    JsonElement? SourceBindings = null,
    JsonElement? SourceRoles = null,
    string? VoiceClass = null,
    IReadOnlyList<string>? VoiceClasses = null,
    JsonElement? LegacyMetadata = null);

public sealed record EvidenceLinkDefinition(
    string Id,
    string WorkId,
    string Edition,
    string Locator,
    bool LocatorVerified,
    string Identity,
    string? Title = null,
    string? StableUrl = null,
    string? WorkStage = null,
    string? PublicationState = null,
    string? QuotationMode = null,
    string? BoundaryZh = null,
    string? Author = null,
    string? Country = null,
    string? LocatorLimit = null,
    string? LocatorStatus = null,
    string? ObjectKey = null,
    string? Translator = null,
    string? Url = null,
    string? Voice = null,
    string? VoiceClass = null,
    string? VoiceLayer = null,
    string? Year = null,
    string? LessonId = null,
    [property: JsonPropertyName("boundary")] string? Boundary = null,
    [property: JsonPropertyName("claim")] string? Claim = null,
    [property: JsonPropertyName("claimNodeId")] string? ClaimNodeId = null,
    [property: JsonPropertyName("genre")] string? Genre = null,
    [property: JsonPropertyName("locatorAuditStatus")] string? LocatorAuditStatus = null,
    [property: JsonPropertyName("locatorPendingReason")] string? LocatorPendingReason = null,
    [property: JsonPropertyName("object")] string? Object = null,
    [property: JsonPropertyName("objectRole")] string? ObjectRole = null,
    [property: JsonPropertyName("period")] string? Period = null,
    [property: JsonPropertyName("source")] string? Source = null,
    [property: JsonPropertyName("sourceType")] string? SourceType = null,
    [property: JsonPropertyName("locatorAuditable")] bool? LocatorAuditable = null,
    [property: JsonPropertyName("intendedClaimIds")] IReadOnlyList<string>? IntendedClaimIds = null,
    [property: JsonPropertyName("editionIdentity")] JsonElement? EditionIdentity = null,
    [property: JsonPropertyName("locatorEvidence")] JsonElement? LocatorEvidence = null,
    JsonElement? LegacyMetadata = null);

public sealed record LearningRouteDefinition(
    string Id,
    string Version,
    string Title,
    IReadOnlyList<string> LessonIds,
    string? HostKind = null,
    JsonElement? Person = null,
    string? Summary = null,
    JsonElement? LegacyMetadata = null);

public sealed record LessonSectionDefinition(
    string Heading,
    IReadOnlyList<string> Paragraphs,
    string Identity,
    LessonSectionKind Kind = LessonSectionKind.Unknown,
    JsonElement? ClaimSourceMap = null,
    JsonElement? EvidenceCitations = null,
    IReadOnlyList<string>? EvidenceIds = null,
    IReadOnlyList<string>? EvidenceLinkIds = null,
    string? Id = null,
    string? NodeId = null,
    JsonElement? ParagraphClaims = null,
    IReadOnlyList<string>? ParagraphVoices = null,
    string? ClaimNodeId = null,
    string? Voice = null,
    JsonElement? ParagraphMetadata = null,
    JsonElement? LegacyMetadata = null);

internal sealed record LessonSectionWire(
    string Heading,
    JsonElement Paragraphs,
    string Identity,
    LessonSectionKind Kind = LessonSectionKind.Unknown,
    JsonElement? ClaimSourceMap = null,
    JsonElement? EvidenceCitations = null,
    IReadOnlyList<string>? EvidenceIds = null,
    IReadOnlyList<string>? EvidenceLinkIds = null,
    string? Id = null,
    string? NodeId = null,
    JsonElement? ParagraphClaims = null,
    IReadOnlyList<string>? ParagraphVoices = null,
    string? ClaimNodeId = null,
    string? Voice = null,
    JsonElement? ParagraphMetadata = null,
    JsonElement? LegacyMetadata = null);

public sealed class LessonSectionDefinitionJsonConverter
    : JsonConverter<LessonSectionDefinition>
{
    private static readonly HashSet<string> ParagraphObjectFields =
        ["paragraphId", "claim", "identity", "evidenceBindings"];

    public override LessonSectionDefinition Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        LessonSectionWire wire = JsonSerializer.Deserialize<LessonSectionWire>(
            document.RootElement.GetRawText(), options) ??
            throw new JsonException("正文部分为空。");
        if (wire.Paragraphs.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("正文段落必须是数组。");
        }

        var paragraphs = new List<string>();
        var metadata = new List<JsonElement>();
        foreach (JsonElement paragraph in wire.Paragraphs.EnumerateArray())
        {
            if (paragraph.ValueKind == JsonValueKind.String)
            {
                paragraphs.Add(paragraph.GetString()!);
                continue;
            }
            if (paragraph.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException("正文段落必须是字符串或正式段落对象。");
            }
            foreach (JsonProperty property in paragraph.EnumerateObject())
            {
                if (!ParagraphObjectFields.Contains(property.Name))
                {
                    throw new JsonException(
                        $"未知正式段落字段“{property.Name}”。");
                }
            }
            string claim = paragraph.GetProperty("claim").GetString() ??
                throw new JsonException("正式段落 claim 为空。");
            paragraphs.Add(claim);
            metadata.Add(paragraph.Clone());
        }

        JsonElement? paragraphMetadata = metadata.Count == 0
            ? wire.ParagraphMetadata
            : JsonSerializer.SerializeToElement(metadata, options);
        return new LessonSectionDefinition(
            wire.Heading, paragraphs, wire.Identity, wire.Kind,
            wire.ClaimSourceMap, wire.EvidenceCitations, wire.EvidenceIds,
            wire.EvidenceLinkIds, wire.Id, wire.NodeId, wire.ParagraphClaims,
            wire.ParagraphVoices, wire.ClaimNodeId, wire.Voice,
            paragraphMetadata, wire.LegacyMetadata);
    }

    public override void Write(
        Utf8JsonWriter writer,
        LessonSectionDefinition value,
        JsonSerializerOptions options)
    {
        JsonElement paragraphs = value.ParagraphMetadata is
            { ValueKind: JsonValueKind.Array } metadata
            ? metadata
            : JsonSerializer.SerializeToElement(value.Paragraphs, options);
        var wire = new LessonSectionWire(
            value.Heading, paragraphs, value.Identity, value.Kind,
            value.ClaimSourceMap, value.EvidenceCitations, value.EvidenceIds,
            value.EvidenceLinkIds, value.Id, value.NodeId,
            value.ParagraphClaims, value.ParagraphVoices, value.ClaimNodeId,
            value.Voice, value.ParagraphMetadata, value.LegacyMetadata);
        JsonSerializer.Serialize(writer, wire, options);
    }
}

public sealed record LessonDefinition(
    string Id,
    int Order,
    string Title,
    string Depth,
    string GuidingQuestion,
    string CoreExplanation,
    IReadOnlyList<LessonSectionDefinition> Sections,
    string CaseText,
    IReadOnlyList<string> NodeIds,
    IReadOnlyList<string> CheckIds,
    [property: JsonConverter(typeof(StringOrStringArrayJsonConverter))]
    IReadOnlyList<string> AbilitySummary,
    string? RecommendedNextLessonId,
    [property: JsonConverter(typeof(RenderAuxiliaryJsonConverter))]
    bool? RenderAuxiliary = null,
    IReadOnlyList<string>? SourceIds = null,
    JsonElement? ContentStatistics = null,
    string? EssentialQuestion = null,
    string? InitialFocusId = null,
    string? RouteId = null,
    string? FocusTarget = null,
    JsonElement? FormalFocusTarget = null,
    JsonElement? LegacyMetadata = null);

public sealed class StringOrStringArrayJsonConverter
    : JsonConverter<IReadOnlyList<string>>
{
    public override IReadOnlyList<string> Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return [reader.GetString()!];
        }
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException("字段必须是字符串或字符串数组。");
        }
        var values = new List<string>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException("字符串数组包含非字符串值。");
            }
            values.Add(reader.GetString()!);
        }
        return values;
    }

    public override void Write(
        Utf8JsonWriter writer,
        IReadOnlyList<string> value,
        JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (string item in value)
        {
            writer.WriteStringValue(item);
        }
        writer.WriteEndArray();
    }
}

public sealed class RenderAuxiliaryJsonConverter : JsonConverter<bool?>
{
    public override bool? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType is JsonTokenType.True or JsonTokenType.False)
        {
            return reader.GetBoolean();
        }
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("renderAuxiliary 必须是布尔值或正式审计对象。");
        }
        foreach (JsonProperty property in document.RootElement.EnumerateObject())
        {
            if (property.Name is not ("evidenceLinkIds" or "readerVisibleAuditText"))
            {
                throw new JsonException(
                    $"未知 renderAuxiliary 审计字段“{property.Name}”。");
            }
        }
        return false;
    }

    public override void Write(
        Utf8JsonWriter writer,
        bool? value,
        JsonSerializerOptions options)
    {
        if (value.HasValue)
        {
            writer.WriteBooleanValue(value.Value);
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}

public sealed record CheckOptionDefinition(
    string Id,
    string Text,
    string Feedback,
    bool IsCorrect,
    JsonElement? SemanticBinding = null,
    string? AnalysisAttribution = null,
    JsonElement? ClaimEvidence = null,
    JsonElement? ClaimEvidenceBindings = null,
    IReadOnlyList<string>? ClaimNodeIds = null,
    JsonElement? EvidenceBindings = null,
    IReadOnlyList<string>? EvidenceLinkIds = null,
    string? EvidenceRole = null,
    IReadOnlyList<string>? FeedbackClaimNodeIds = null,
    JsonElement? FeedbackEvidence = null,
    JsonElement? FeedbackEvidenceBindings = null,
    IReadOnlyList<string>? FeedbackEvidenceLinkIds = null,
    JsonElement? FeedbackSource = null,
    JsonElement? OptionSource = null,
    JsonElement? Binding = null,
    JsonElement? FeedbackBinding = null,
    JsonElement? LegacyMetadata = null);

public sealed record KnowledgeCheckDefinition(
    string Id,
    string Kind,
    string Prompt,
    IReadOnlyList<string> TargetNodeIds,
    IReadOnlyList<CheckOptionDefinition> Options,
    string? CorrectOptionId,
    string MisconceptionId,
    string? BindingSha256 = null,
    IReadOnlyList<string>? EvidenceLinkIds = null,
    IReadOnlyList<string>? ObjectNodeIds = null,
    IReadOnlyList<string>? PromptClaimNodeIds = null,
    JsonElement? PromptEvidence = null,
    JsonElement? PromptEvidenceBindings = null,
    JsonElement? PromptSource = null,
    JsonElement? SemanticFacets = null,
    IReadOnlyList<string>? TargetParagraphIds = null,
    IReadOnlyList<string>? TargetVoices = null,
    JsonElement? TruthEvidence = null,
    JsonElement? TruthEvidenceBindings = null,
    JsonElement? TruthSource = null,
    string? LessonId = null,
    JsonElement? PromptBinding = null,
    string? Voice = null,
    JsonElement? LegacyMetadata = null);

public sealed record LearningPack(
    string SchemaVersion,
    IReadOnlyList<KnowledgeNodeDefinition> Nodes,
    IReadOnlyList<EvidenceLinkDefinition> EvidenceLinks,
    IReadOnlyList<LearningRouteDefinition> Routes,
    IReadOnlyList<LessonDefinition> Lessons,
    IReadOnlyList<KnowledgeCheckDefinition> Checks,
    JsonElement? ContentStatistics = null,
    JsonElement? IntegrationContract = null,
    string? SourceSchemaVersion = null,
    [property: JsonPropertyName("integrationProjection")]
    JsonElement? IntegrationProjection = null,
    JsonElement? LegacyMetadata = null)
{
    private IReadOnlyDictionary<string, KnowledgeNodeDefinition>? nodesById;
    private IReadOnlyDictionary<string, LearningRouteDefinition>? routesById;
    private IReadOnlyDictionary<string, LessonDefinition>? lessonsById;
    private IReadOnlyDictionary<string, KnowledgeCheckDefinition>? checksById;

    public static LearningPack Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using JsonDocument source = JsonDocument.Parse(stream);
        string? sourceSchema = source.RootElement.TryGetProperty(
            "schemaVersion", out JsonElement schemaElement)
            ? schemaElement.GetString() : null;
        JsonElement sourceRoutes = source.RootElement.GetProperty("routes");
        string? sourceRouteId = sourceRoutes.GetArrayLength() == 1
            ? sourceRoutes[0].GetProperty("id").GetString() : null;
        EnsureSupportedFormalAliasIdentity(sourceSchema, sourceRouteId);

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        };
        options.Converters.Add(
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        options.Converters.Insert(0, new KnowledgeNodeStatusJsonConverter(
            SupportedAuthoringCandidateRouteIds.Contains(sourceRouteId ?? "")));
        options.Converters.Insert(0, new KnowledgeNodeKindJsonConverter());
        options.Converters.Insert(0, new LessonSectionDefinitionJsonConverter());
        options.Converters.Insert(0, new LessonSectionKindJsonConverter());

        LearningPack pack = IsSupportedStrictSixLegacyPack(source.RootElement)
            ? LoadStrictSixLegacyPack(source.RootElement, options)
            : JsonSerializer.Deserialize<LearningPack>(
                source.RootElement.GetRawText(), options) ??
                throw new InvalidDataException("学习包为空。");
        pack = pack with { LegacyMetadata = source.RootElement.Clone() };
        pack = NormalizeFormalPack(pack);
        pack.Validate();
        return pack;
    }

    private static LearningPack NormalizeFormalPack(LearningPack pack)
    {
        string? routeId = pack.Routes.Count == 1 ? pack.Routes[0].Id : null;
        if (pack.SchemaVersion == "LearningPack 1.0" &&
            routeId == "machiavelli-power-liberty-republic")
        {
            pack = pack with
            {
                SchemaVersion = "1.0",
                SourceSchemaVersion = "LearningPack 1.0",
            };
        }

        pack = pack with
        {
            EvidenceLinks = pack.EvidenceLinks.Select(link => link with
            {
                WorkId = string.IsNullOrWhiteSpace(link.WorkId)
                    ? link.Title ?? throw new InvalidDataException(
                        $"证据链接“{link.Id}”缺少作品身份。")
                    : link.WorkId,
                Edition = string.IsNullOrWhiteSpace(link.Edition)
                    ? link.Author ?? link.Identity
                    : link.Edition,
            }).ToArray(),
            Lessons = pack.Lessons.Select(lesson => lesson with
            {
                Sections = lesson.Sections.Select(section => section with
                {
                    Identity = string.IsNullOrWhiteSpace(section.Identity)
                        ? $"formal-section:{section.Kind}" : section.Identity,
                }).ToArray(),
            }).ToArray(),
        };

        if (routeId == "fanon-sociogeny-decolonization-humanism")
        {
            var lessonNodes = pack.Lessons.ToDictionary(
                lesson => lesson.Id,
                lesson => lesson.Sections
                    .SelectMany(section => ReadNodeIds(section.ParagraphClaims))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray(),
                StringComparer.Ordinal);
            pack = pack with
            {
                Routes = pack.Routes.Select(route => route with
                {
                    Version = string.IsNullOrWhiteSpace(route.Version)
                        ? "1.0-formal" : route.Version,
                }).ToArray(),
                Nodes = pack.Nodes.Select(node => node with
                {
                    Status = KnowledgeNodeStatus.TeachingReady,
                    ReaderTitle = string.IsNullOrWhiteSpace(node.ReaderTitle)
                        ? node.Label ?? node.Id
                        : node.ReaderTitle,
                    Identity = string.IsNullOrWhiteSpace(node.Identity)
                        ? node.VoiceClass ?? node.BindingMode ?? "formal-claim"
                        : node.Identity,
                    AbilityIds = node.AbilityIds is null || node.AbilityIds.Count == 0
                        ? [$"understand-{node.Id}"]
                        : node.AbilityIds,
                }).ToArray(),
                Lessons = pack.Lessons.Select((lesson, index) => lesson with
                {
                    Order = lesson.Order == 0 ? index + 1 : lesson.Order,
                    Depth = string.IsNullOrWhiteSpace(lesson.Depth)
                        ? "longform" : lesson.Depth,
                    GuidingQuestion = string.IsNullOrWhiteSpace(lesson.GuidingQuestion)
                        ? lesson.EssentialQuestion ?? lesson.Title
                        : lesson.GuidingQuestion,
                    CaseText = string.IsNullOrWhiteSpace(lesson.CaseText)
                        ? lesson.CoreExplanation : lesson.CaseText,
                    NodeIds = lesson.NodeIds is null || lesson.NodeIds.Count == 0
                        ? lessonNodes[lesson.Id] : lesson.NodeIds,
                    AbilitySummary = lesson.AbilitySummary is null ||
                        lesson.AbilitySummary.Count == 0
                        ? [lesson.EssentialQuestion ?? lesson.Title]
                        : lesson.AbilitySummary,
                    Sections = lesson.Sections.Select(section => section with
                    {
                        Identity = string.IsNullOrWhiteSpace(section.Identity)
                            ? "formal-paragraph-claims" : section.Identity,
                    }).ToArray(),
                }).ToArray(),
                Checks = pack.Checks.Select(check => check with
                {
                    MisconceptionId = string.IsNullOrWhiteSpace(check.MisconceptionId)
                        ? $"{check.Id}-misconception"
                        : check.MisconceptionId,
                }).ToArray(),
            };
        }

        return pack;
    }

    private static IEnumerable<string> ReadNodeIds(JsonElement? claims)
    {
        if (claims is not { ValueKind: JsonValueKind.Array } array)
        {
            yield break;
        }
        foreach (JsonElement claim in array.EnumerateArray())
        {
            if (claim.TryGetProperty("nodeId", out JsonElement nodeId) &&
                nodeId.ValueKind == JsonValueKind.String)
            {
                yield return nodeId.GetString()!;
            }
        }
    }

    private static readonly HashSet<string> SupportedStrictSixRouteIds =
    [
        "peirce-inquiry-sign-continuity",
        "habermas-communication-public-law",
        "ramanuja-qualified-nonduality",
        "ibn-arabi-disclosure-imagination-reception",
        "merleau-ponty-perception-body-flesh",
        "dogen-practice-time-expression",
        "gadamer-truth-understanding-language",
        "jl-austin-ordinary-language-speech-acts",
        "ibn-rushd-demonstration-commentary-intellect",
    ];

    private static readonly HashSet<string> SupportedAuthoringCandidateRouteIds =
    [
        "deleuze-difference-immanence-becoming",
        "thomas-kuhn-paradigms-revolutions-incommensurability",
        "levinas-otherness-responsibility-justice",
        "wang-yangming-mind-knowledge-practice",
        "heraclitus-logos-strife-measure",
        "parmenides-being-thinking-appearance",
        "bertrand-russell-analysis-facts-revision",
        "henri-bergson-duration-memory-creation",
        "al-ghazali-philosophy-critique-practice",
        "schelling-nature-freedom-actuality",
        "william-james-experience-belief-truth",
        "anscombe-intention-action-justice",
        "seneca-judgment-time-practice",
        "popper-criticism-knowledge-open-society",
        "simone-weil-attention-force-rootedness",
        "nishida-experience-place-historical-world",
        "cicero-republic-skepticism-duty",
        "sextus-empiricus-suspension-appearance-practice",
        "william-of-ockham-signs-cognition-power",
        "quine-web-of-belief-reference-ontology",
        "vasubandhu-abhidharma-representation-three-natures",
        "ibn-khaldun-civilization-history-solidarity",
        "elisabeth-of-bohemia-mind-body-virtue",
        "margaret-cavendish-self-moving-matter-perception",
        "emilie-du-chatelet-hypotheses-force-happiness",
        "judith-butler-performativity-recognition-precarity",
        "enrique-dussel-exteriority-liberation-transmodernity",
        "kwasi-wiredu-conceptual-decolonization-consensus",
        "mary-astell-reason-education-freedom",
        "watsuji-tetsuro-betweenness-ethics-climate",
        "maria-lugones-world-travelling-coloniality-resistance",
        "anton-wilhelm-amo-mind-body-knowledge-method",
    ];

    private static void EnsureSupportedFormalAliasIdentity(
        string? schema, string? routeId)
    {
        bool supported = schema switch
        {
            "1.0" or "strict-six/v1" => true,
            "LearningPack 1.0" => routeId is
                "machiavelli-power-liberty-republic" or
                "deleuze-difference-immanence-becoming",
            "LearningPack-1.0" => routeId is
                "thomas-kuhn-paradigms-revolutions-incommensurability" or
                "levinas-otherness-responsibility-justice" or
                "wang-yangming-mind-knowledge-practice",
            _ => false,
        };
        if (!supported)
        {
            throw new JsonException(
                "学习包 schema alias 与 route identity 未在兼容表中注册。");
        }
    }

    private static bool IsSupportedStrictSixLegacyPack(JsonElement root)
    {
        string? schema = root.TryGetProperty("schemaVersion", out JsonElement value)
            ? value.GetString() : null;
        if (schema != "strict-six/v1")
        {
            return false;
        }
        JsonElement routes = root.GetProperty("routes");
        string? routeId = routes.GetArrayLength() == 1
            ? routes[0].GetProperty("id").GetString()
            : null;
        if (routeId is null || !SupportedStrictSixRouteIds.Contains(routeId))
        {
            throw new JsonException(
                "strict-six/v1 legacy adapter 仅允许显式注册的正式或候选 route。");
        }
        return true;
    }

    private static LearningPack LoadStrictSixLegacyPack(
        JsonElement root,
        JsonSerializerOptions options) =>
        root.GetProperty("routes")[0].GetProperty("id").GetString() ==
            "peirce-inquiry-sign-continuity"
            ? LoadPeirceLegacyPack(root, options)
            : LoadV17CandidateStrictSixPack(root, options);

    private static LearningPack LoadV17CandidateStrictSixPack(
        JsonElement root,
        JsonSerializerOptions options)
    {
        EnsureOnlyProperties(root,
            "schemaVersion", "nodes", "evidenceLinks", "routes", "lessons", "checks");
        JsonElement routeSource = root.GetProperty("routes")[0];
        EnsureOnlyProperties(routeSource,
            "id", "catalogEntryId", "title", "summary", "person", "lessonIds");
        EnsureNestedObject(routeSource, "person", "name", "nameZh",
            "canonicalName", "displayName", "aliasesForDedupOnly",
            "disambiguation");

        KnowledgeNodeDefinition[] nodes = root.GetProperty("nodes")
            .EnumerateArray().Select(node =>
            {
                EnsureOnlyProperties(node,
                    "id", "type", "kind", "lessonId", "text", "evidenceLinkIds",
                    "period", "identity", "object", "voice");
                string id = node.GetProperty("id").GetString()!;
                string text = node.GetProperty("text").GetString()!;
                string identity = GetOptionalString(node, "identity") ??
                    GetOptionalString(node, "voice") ?? "fixed-claim";
                return new KnowledgeNodeDefinition(
                    id, KnowledgeNodeKind.Claim, KnowledgeNodeStatus.TeachingReady,
                    text, text, identity, ReadStrings(node, "evidenceLinkIds"),
                    [$"understand-{id}"],
                    ObjectKey: GetOptionalString(node, "object"),
                    VoiceClass: GetOptionalString(node, "voice"),
                    LegacyMetadata: node.Clone());
            }).ToArray();

        EvidenceLinkDefinition[] evidence = root.GetProperty("evidenceLinks")
            .EnumerateArray().Select(link =>
            {
                EnsureOnlyProperties(link,
                    "id", "lessonId", "claimNodeId", "locator", "period",
                    "identity", "object", "voice", "source", "claim",
                    "workId", "edition", "boundary",
                    "locatorVerified", "locatorAuditable", "locatorAuditStatus",
                    "locatorPendingReason");
                string identity = GetOptionalString(link, "identity") ??
                    GetOptionalString(link, "source") ?? "fixed-evidence";
                string workId = GetOptionalString(link, "workId") ??
                    GetOptionalString(link, "object") ??
                    GetOptionalString(link, "source") ?? identity;
                string edition = GetOptionalString(link, "edition") ??
                    GetOptionalString(link, "period") ?? identity;
                return new EvidenceLinkDefinition(
                    link.GetProperty("id").GetString()!, workId, edition,
                    link.GetProperty("locator").GetString()!,
                    link.GetProperty("locatorVerified").GetBoolean(), identity,
                    LocatorStatus: GetOptionalString(link, "locatorAuditStatus"),
                    ObjectKey: workId,
                    Voice: GetOptionalString(link, "voice"),
                    LessonId: GetOptionalString(link, "lessonId"),
                    LegacyMetadata: link.Clone());
            }).ToArray();

        LessonDefinition[] lessons = root.GetProperty("lessons")
            .EnumerateArray().Select((lesson, index) =>
            {
                EnsureOnlyProperties(lesson,
                    "id", "title", "guidingQuestion", "initialFocusId", "focusTarget",
                    "sections", "claimNodeIds", "nodeIds", "sourceIds", "checkIds",
                    "order", "objection", "response", "acceptanceBoundary");
                string lessonId = lesson.GetProperty("id").GetString()!;
                LessonSectionDefinition[] sections = lesson.GetProperty("sections")
                    .EnumerateArray().Select(section =>
                    {
                        EnsureOnlyProperties(section,
                            "id", "heading", "kind", "paragraphs", "claimNodeIds",
                            "evidenceLinkIds", "voice");
                        JsonElement[] paragraphMetadata = section.GetProperty("paragraphs")
                            .EnumerateArray().Select(paragraph =>
                            {
                                EnsureOnlyProperties(paragraph,
                                    "paragraphId", "text", "claim", "claimNodeIds",
                                    "evidenceLinkIds", "voice");
                                return paragraph.Clone();
                            }).ToArray();
                        string[] paragraphs = section.GetProperty("paragraphs")
                            .EnumerateArray()
                            .Select(paragraph => GetOptionalString(paragraph, "text") ??
                                GetOptionalString(paragraph, "claim")!)
                            .ToArray();
                        string[] claimNodeIds = ReadStrings(section, "claimNodeIds").ToArray();
                        string[] evidenceIds = ReadStrings(section, "evidenceLinkIds").ToArray();
                        if (evidenceIds.Length == 0)
                        {
                            evidenceIds = claimNodeIds
                                .SelectMany(nodeId => nodes.Single(node => node.Id == nodeId)
                                    .EvidenceLinkIds)
                                .Distinct(StringComparer.Ordinal).ToArray();
                        }
                        return new LessonSectionDefinition(
                            section.GetProperty("heading").GetString()!, paragraphs,
                            "v17-strict-six-candidate", LessonSectionKind.Argument,
                            EvidenceLinkIds: evidenceIds,
                            Id: section.GetProperty("id").GetString(),
                            ClaimNodeId: claimNodeIds.FirstOrDefault(),
                            ParagraphMetadata: JsonSerializer.SerializeToElement(
                                paragraphMetadata, options),
                            LegacyMetadata: section.Clone());
                    }).ToArray();
                string guidingQuestion = lesson.GetProperty("guidingQuestion").GetString()!;
                string core = sections.SelectMany(section => section.Paragraphs)
                    .FirstOrDefault() ?? guidingQuestion;
                return new LessonDefinition(
                    lessonId, index + 1, lesson.GetProperty("title").GetString()!,
                    "longform", guidingQuestion, core, sections, core,
                    ReadStrings(lesson, "claimNodeIds").Concat(
                        ReadStrings(lesson, "nodeIds").Where(id =>
                            nodes.Any(node => node.Id == id))).Concat(
                        root.GetProperty("nodes").EnumerateArray()
                            .Where(node => GetOptionalString(node, "lessonId") == lessonId)
                            .Select(node => node.GetProperty("id").GetString()!))
                        .Distinct(StringComparer.Ordinal).ToArray(),
                    ReadStrings(lesson, "checkIds"), [guidingQuestion],
                    index + 1 < root.GetProperty("lessons").GetArrayLength()
                        ? root.GetProperty("lessons")[index + 1]
                            .GetProperty("id").GetString()
                        : null,
                    SourceIds: ReadStrings(lesson, "sourceIds"),
                    InitialFocusId: GetOptionalString(lesson, "initialFocusId"),
                    FocusTarget: GetOptionalString(lesson, "focusTarget"),
                    LegacyMetadata: lesson.Clone());
            }).ToArray();

        KnowledgeCheckDefinition[] checks = root.GetProperty("checks")
            .EnumerateArray().Select(check =>
            {
                EnsureOnlyProperties(check,
                    "id", "lessonId", "gate", "prompt", "promptBinding",
                    "targetNodeIds", "evidenceLinkIds", "voice", "voices",
                    "options", "correctOptionId", "bindingSha256");
                CheckOptionDefinition[] checkOptions = check.GetProperty("options")
                    .EnumerateArray().Select(option =>
                    {
                        EnsureOnlyProperties(option,
                            "id", "text", "isCorrect", "feedback", "binding",
                            "feedbackBinding");
                        return new CheckOptionDefinition(
                            option.GetProperty("id").GetString()!,
                            option.GetProperty("text").GetString()!,
                            option.GetProperty("feedback").GetString()!,
                            option.GetProperty("isCorrect").GetBoolean(),
                            Binding: GetOptionalElement(option, "binding"),
                            FeedbackBinding: GetOptionalElement(option, "feedbackBinding"),
                            LegacyMetadata: option.Clone());
                    }).ToArray();
                string id = check.GetProperty("id").GetString()!;
                return new KnowledgeCheckDefinition(
                    id, GetOptionalString(check, "gate") ?? "legacy-bound",
                    check.GetProperty("prompt").GetString()!,
                    ReadStrings(check, "targetNodeIds"), checkOptions,
                    GetOptionalString(check, "correctOptionId"),
                    $"{id}-misconception",
                    BindingSha256: GetOptionalString(check, "bindingSha256"),
                    EvidenceLinkIds: ReadStrings(check, "evidenceLinkIds"),
                    LessonId: GetOptionalString(check, "lessonId"),
                    PromptBinding: GetOptionalElement(check, "promptBinding"),
                    Voice: GetOptionalString(check, "voice") ??
                        GetOptionalString(check, "voices"),
                    LegacyMetadata: check.Clone());
            }).ToArray();

        var route = new LearningRouteDefinition(
            routeSource.GetProperty("id").GetString()!, "strict-six/v1",
            routeSource.GetProperty("title").GetString()!,
            ReadStrings(routeSource, "lessonIds"),
            Person: GetOptionalElement(routeSource, "person"),
            Summary: GetOptionalString(routeSource, "summary"),
            LegacyMetadata: routeSource.Clone());
        return new LearningPack(
            "1.0", nodes, evidence, [route], lessons, checks,
            SourceSchemaVersion: "strict-six/v1",
            LegacyMetadata: root.Clone());
    }

    private static LearningPack LoadPeirceLegacyPack(
        JsonElement root,
        JsonSerializerOptions options)
    {
        EnsureOnlyProperties(root,
            "schemaVersion", "nodes", "evidenceLinks", "routes", "lessons", "checks");
        JsonElement routeSource = root.GetProperty("routes")[0];
        EnsureOnlyProperties(routeSource,
            "id", "title", "summary", "person", "lessonIds");
        EnsureNestedObject(routeSource, "person", "name", "nameZh");

        KnowledgeNodeDefinition[] nodes = root.GetProperty("nodes")
            .EnumerateArray().Select(node =>
            {
                EnsureOnlyProperties(node,
                    "id", "type", "lessonId", "sectionId", "text", "evidenceLinkIds", "voice");
                string type = node.GetProperty("type").GetString()!;
                if (type != "claim")
                {
                    throw new JsonException($"未知 Peirce legacy node type“{type}”。");
                }
                string id = node.GetProperty("id").GetString()!;
                string text = node.GetProperty("text").GetString()!;
                string voice = node.GetProperty("voice").GetString()!;
                return new KnowledgeNodeDefinition(
                    id, KnowledgeNodeKind.Claim, KnowledgeNodeStatus.TeachingReady,
                    text, text, voice,
                    ReadStrings(node, "evidenceLinkIds"),
                    [$"understand-{id}"],
                    LegacyMetadata: node.Clone());
            }).ToArray();

        EvidenceLinkDefinition[] evidence = root.GetProperty("evidenceLinks")
            .EnumerateArray().Select(link =>
            {
                EnsureOnlyProperties(link,
                    "id", "workId", "edition", "locator", "locatorVerified", "identity", "lessonId");
                return new EvidenceLinkDefinition(
                    link.GetProperty("id").GetString()!,
                    link.GetProperty("workId").GetString()!,
                    link.GetProperty("edition").GetString()!,
                    link.GetProperty("locator").GetString()!,
                    link.GetProperty("locatorVerified").GetBoolean(),
                    link.GetProperty("identity").GetString()!,
                    LessonId: GetOptionalString(link, "lessonId"),
                    LegacyMetadata: link.Clone());
            }).ToArray();

        LessonDefinition[] lessons = root.GetProperty("lessons")
            .EnumerateArray().Select((lesson, index) =>
            {
                EnsureOnlyProperties(lesson,
                    "id", "title", "guidingQuestion", "sections", "caseText",
                    "checkIds", "abilitySummary", "sourceIds", "focusTarget", "initialFocusId");
                string lessonId = lesson.GetProperty("id").GetString()!;
                LessonSectionDefinition[] sections = lesson.GetProperty("sections")
                    .EnumerateArray().Select(section => ReadPeirceSection(section, options))
                    .ToArray();
                string core = sections.SelectMany(section => section.Paragraphs)
                    .FirstOrDefault() ?? lesson.GetProperty("guidingQuestion").GetString()!;
                return new LessonDefinition(
                    lessonId, index + 1,
                    lesson.GetProperty("title").GetString()!, "longform",
                    lesson.GetProperty("guidingQuestion").GetString()!, core,
                    sections, lesson.GetProperty("caseText").GetString()!,
                    nodes.Where(node => node.LegacyMetadata is JsonElement metadata &&
                            metadata.GetProperty("lessonId").GetString() == lessonId)
                        .Select(node => node.Id).ToArray(),
                    ReadStrings(lesson, "checkIds"),
                    ReadStringOrArray(lesson, "abilitySummary"), null, false,
                    ReadStrings(lesson, "sourceIds"),
                    InitialFocusId: GetOptionalString(lesson, "initialFocusId"),
                    FocusTarget: GetOptionalString(lesson, "focusTarget"),
                    LegacyMetadata: lesson.Clone());
            }).ToArray();

        KnowledgeCheckDefinition[] checks = root.GetProperty("checks")
            .EnumerateArray().Select(check => ReadPeirceCheck(check))
            .ToArray();
        var route = new LearningRouteDefinition(
            routeSource.GetProperty("id").GetString()!, "strict-six/v1",
            routeSource.GetProperty("title").GetString()!,
            ReadStrings(routeSource, "lessonIds"),
            Person: GetOptionalElement(routeSource, "person"),
            Summary: GetOptionalString(routeSource, "summary"),
            LegacyMetadata: routeSource.Clone());
        return new LearningPack(
            "1.0", nodes, evidence, [route], lessons, checks,
            SourceSchemaVersion: "strict-six/v1",
            LegacyMetadata: root.Clone());
    }

    private static LessonSectionDefinition ReadPeirceSection(
        JsonElement section,
        JsonSerializerOptions options)
    {
        EnsureOnlyProperties(section,
            "id", "kind", "heading", "paragraphs", "claimNodeId", "evidenceLinkIds", "voice");
        var paragraphs = new List<string>();
        var metadata = new List<JsonElement>();
        foreach (JsonElement paragraph in section.GetProperty("paragraphs").EnumerateArray())
        {
            EnsureOnlyProperties(paragraph,
                "paragraphId", "claim", "voice", "evidenceLinkIds", "locators");
            EnsureNestedArrayObjects(paragraph, "locators",
                "evidenceLinkId", "workId", "edition", "locator",
                "identity", "locatorVerified");
            paragraphs.Add(paragraph.GetProperty("claim").GetString()!);
            metadata.Add(paragraph.Clone());
        }
        LessonSectionKind kind = JsonSerializer.Deserialize<LessonSectionKind>(
            section.GetProperty("kind").GetRawText(), options);
        string voice = section.GetProperty("voice").GetString()!;
        return new LessonSectionDefinition(
            section.GetProperty("heading").GetString()!, paragraphs, voice, kind,
            EvidenceLinkIds: ReadStrings(section, "evidenceLinkIds"),
            Id: section.GetProperty("id").GetString(),
            ClaimNodeId: GetOptionalString(section, "claimNodeId"),
            Voice: voice,
            ParagraphMetadata: JsonSerializer.SerializeToElement(metadata, options),
            LegacyMetadata: section.Clone());
    }

    private static KnowledgeCheckDefinition ReadPeirceCheck(JsonElement check)
    {
        EnsureOnlyProperties(check,
            "id", "lessonId", "prompt", "promptBinding", "targetNodeIds",
            "options", "correctOptionId", "bindingSha256", "evidenceLinkIds", "voice");
        EnsureNestedObject(check, "promptBinding",
            "lessonId", "targetNodeIds", "evidenceLinkIds", "voice");
        CheckOptionDefinition[] options = check.GetProperty("options")
            .EnumerateArray().Select(option =>
            {
                EnsureOnlyProperties(option,
                    "id", "text", "feedback", "isCorrect", "binding", "feedbackBinding");
                EnsureNestedObject(option, "binding",
                    "lessonId", "targetNodeIds", "evidenceLinkIds", "voice");
                EnsureNestedObject(option, "feedbackBinding",
                    "lessonId", "targetNodeIds", "evidenceLinkIds", "voice");
                return new CheckOptionDefinition(
                    option.GetProperty("id").GetString()!,
                    option.GetProperty("text").GetString()!,
                    option.GetProperty("feedback").GetString()!,
                    option.GetProperty("isCorrect").GetBoolean(),
                    Binding: GetOptionalElement(option, "binding"),
                    FeedbackBinding: GetOptionalElement(option, "feedbackBinding"),
                    LegacyMetadata: option.Clone());
            }).ToArray();
        string id = check.GetProperty("id").GetString()!;
        return new KnowledgeCheckDefinition(
            id, "legacy-bound", check.GetProperty("prompt").GetString()!,
            ReadStrings(check, "targetNodeIds"), options,
            GetOptionalString(check, "correctOptionId"), $"{id}-misconception",
            BindingSha256: GetOptionalString(check, "bindingSha256"),
            EvidenceLinkIds: ReadStrings(check, "evidenceLinkIds"),
            LessonId: GetOptionalString(check, "lessonId"),
            PromptBinding: GetOptionalElement(check, "promptBinding"),
            Voice: GetOptionalString(check, "voice"),
            LegacyMetadata: check.Clone());
    }

    private static IReadOnlyList<string> ReadStrings(JsonElement source, string name) =>
        source.TryGetProperty(name, out JsonElement values) &&
        values.ValueKind == JsonValueKind.Array
            ? values.EnumerateArray().Select(value => value.GetString()!).ToArray()
            : [];

    private static IReadOnlyList<string> ReadStringOrArray(
        JsonElement source,
        string name)
    {
        if (!source.TryGetProperty(name, out JsonElement value))
        {
            return [];
        }
        return value.ValueKind switch
        {
            JsonValueKind.String => [value.GetString()!],
            JsonValueKind.Array => value.EnumerateArray()
                .Select(item => item.GetString()!).ToArray(),
            _ => throw new JsonException($"{name} 必须是字符串或字符串数组。"),
        };
    }

    private static string? GetOptionalString(JsonElement source, string name) =>
        source.TryGetProperty(name, out JsonElement value) &&
        value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static JsonElement? GetOptionalElement(JsonElement source, string name) =>
        source.TryGetProperty(name, out JsonElement value) ? value.Clone() : null;

    private static void EnsureOnlyProperties(
        JsonElement source,
        params string[] allowed)
    {
        HashSet<string> names = allowed.ToHashSet(StringComparer.Ordinal);
        foreach (JsonProperty property in source.EnumerateObject())
        {
            if (!names.Contains(property.Name))
            {
                throw new JsonException($"未知 legacy 字段“{property.Name}”。");
            }
        }
    }

    private static void EnsureNestedObject(
        JsonElement source,
        string propertyName,
        params string[] allowed)
    {
        if (!source.TryGetProperty(propertyName, out JsonElement nested))
        {
            return;
        }
        if (nested.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException($"{propertyName} 必须是对象。");
        }
        EnsureOnlyProperties(nested, allowed);
    }

    private static void EnsureNestedArrayObjects(
        JsonElement source,
        string propertyName,
        params string[] allowed)
    {
        if (!source.TryGetProperty(propertyName, out JsonElement nested))
        {
            return;
        }
        if (nested.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException($"{propertyName} 必须是对象数组。");
        }
        foreach (JsonElement item in nested.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException($"{propertyName} 包含非对象值。");
            }
            EnsureOnlyProperties(item, allowed);
        }
    }

    public KnowledgeNodeDefinition GetNode(string id) =>
        NodeIndex.TryGetValue(id, out KnowledgeNodeDefinition? node)
            ? node
            : throw new KeyNotFoundException($"未知知识节点“{id}”。");

    public LearningRouteDefinition GetRoute(string id) =>
        RouteIndex.TryGetValue(id, out LearningRouteDefinition? route)
            ? route
            : throw new KeyNotFoundException($"未知学习路线“{id}”。");

    public LessonDefinition GetLesson(string id) =>
        LessonIndex.TryGetValue(id, out LessonDefinition? lesson)
            ? lesson
            : throw new KeyNotFoundException($"未知课程章节“{id}”。");

    public KnowledgeCheckDefinition GetCheck(string id) =>
        CheckIndex.TryGetValue(id, out KnowledgeCheckDefinition? check)
            ? check
            : throw new KeyNotFoundException($"未知理解检查“{id}”。");

    private IReadOnlyDictionary<string, KnowledgeNodeDefinition> NodeIndex =>
        nodesById ??= Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);

    private IReadOnlyDictionary<string, LearningRouteDefinition> RouteIndex =>
        routesById ??= Routes.ToDictionary(route => route.Id, StringComparer.Ordinal);

    private IReadOnlyDictionary<string, LessonDefinition> LessonIndex =>
        lessonsById ??= Lessons.ToDictionary(lesson => lesson.Id, StringComparer.Ordinal);

    private IReadOnlyDictionary<string, KnowledgeCheckDefinition> CheckIndex =>
        checksById ??= Checks.ToDictionary(check => check.Id, StringComparer.Ordinal);

    private void Validate()
    {
        if (SchemaVersion is not ("1.0" or "LearningPack 1.0" or
                "LearningPack-1.0" or "strict-six/v1"))
        {
            throw new InvalidDataException("学习包版本必须为受支持的 1.0 固定身份格式。");
        }

        EnsureUnique(Nodes.Select(node => node.Id), "知识节点");
        EnsureUnique(EvidenceLinks.Select(link => link.Id), "证据链接");
        EnsureUnique(Routes.Select(route => route.Id), "学习路线");
        EnsureUnique(Lessons.Select(lesson => lesson.Id), "课程章节");
        EnsureUnique(Checks.Select(check => check.Id), "理解检查");

        var evidenceIds = EvidenceLinks
            .Select(link => link.Id)
            .ToHashSet(StringComparer.Ordinal);
        var lessonIds = Lessons
            .Select(lesson => lesson.Id)
            .ToHashSet(StringComparer.Ordinal);
        var checkIds = Checks
            .Select(check => check.Id)
            .ToHashSet(StringComparer.Ordinal);

        foreach (EvidenceLinkDefinition link in EvidenceLinks)
        {
            EnsureText(link.WorkId, "证据作品");
            EnsureText(link.Edition, "证据版本");
            EnsureText(link.Locator, "证据位置");
            EnsureText(link.Identity, "证据身份");
        }

        foreach (KnowledgeNodeDefinition node in Nodes)
        {
            EnsureText(node.ReaderTitle, "知识节点标题");
            EnsureText(node.Explanation, "知识节点解释");
            EnsureText(node.Identity, "知识节点身份");
            if (node.AbilityIds.Count == 0 ||
                node.AbilityIds.Any(string.IsNullOrWhiteSpace))
            {
                throw new InvalidDataException(
                    $"知识节点“{node.Id}”缺少能力目标。");
            }

            if (node.EvidenceLinkIds.Count == 0 ||
                node.EvidenceLinkIds.Any(id => !evidenceIds.Contains(id)))
            {
                throw new InvalidDataException(
                    $"知识节点“{node.Id}”包含未知或空证据链接。");
            }
        }

        foreach (LearningRouteDefinition route in Routes)
        {
            EnsureText(route.Version, "路线版本");
            EnsureText(route.Title, "路线标题");
            if (route.LessonIds.Count == 0 ||
                route.LessonIds.Any(id => !lessonIds.Contains(id)))
            {
                throw new InvalidDataException(
                    $"学习路线“{route.Id}”包含未知或空章节。");
            }

            EnsureUnique(route.LessonIds, $"路线“{route.Id}”章节");
        }

        foreach (LessonDefinition lesson in Lessons)
        {
            EnsureText(lesson.Title, "章节标题");
            EnsureText(lesson.Depth, "章节深度");
            if (lesson.RenderAuxiliary != false)
            {
                EnsureText(lesson.GuidingQuestion, "章节问题");
                EnsureText(lesson.CoreExplanation, "核心解释");
                EnsureText(lesson.CaseText, "章节案例");
            }
            if (lesson.Sections.Count == 0 ||
                lesson.Sections.Any(section =>
                    section.Kind == LessonSectionKind.Unknown ||
                    string.IsNullOrWhiteSpace(section.Heading) ||
                    string.IsNullOrWhiteSpace(section.Identity) ||
                    section.Paragraphs.Count == 0 ||
                    section.Paragraphs.Any(string.IsNullOrWhiteSpace)))
            {
                throw new InvalidDataException(
                    $"课程章节“{lesson.Id}”包含空正文部分。");
            }

            if (lesson.NodeIds.Count == 0)
            {
                throw new InvalidDataException(
                    $"课程章节“{lesson.Id}”没有知识节点。");
            }

            foreach (string nodeId in lesson.NodeIds)
            {
                KnowledgeNodeDefinition node = GetNode(nodeId);
                if (node.Status != KnowledgeNodeStatus.TeachingReady)
                {
                    throw new InvalidDataException(
                        $"课程章节“{lesson.Id}”引用了非 teaching-ready 节点“{nodeId}”。");
                }
            }

            if (lesson.CheckIds.Count == 0 ||
                lesson.CheckIds.Any(id => !checkIds.Contains(id)))
            {
                throw new InvalidDataException(
                    $"课程章节“{lesson.Id}”包含未知或空理解检查。");
            }

            if (lesson.AbilitySummary.Count == 0 ||
                lesson.AbilitySummary.Any(string.IsNullOrWhiteSpace))
            {
                throw new InvalidDataException(
                    $"课程章节“{lesson.Id}”缺少能力总结。");
            }

            if (lesson.RecommendedNextLessonId is not null &&
                !lessonIds.Contains(lesson.RecommendedNextLessonId))
            {
                throw new InvalidDataException(
                    $"课程章节“{lesson.Id}”的推荐下一章不存在。");
            }
        }

        foreach (KnowledgeCheckDefinition check in Checks)
        {
            EnsureText(check.Kind, "检查类型");
            EnsureText(check.Prompt, "检查题目");
            EnsureText(check.MisconceptionId, "误解标识");
            if (check.TargetNodeIds.Count == 0)
            {
                throw new InvalidDataException(
                    $"理解检查“{check.Id}”没有目标知识节点。");
            }

            var targetNodes = check.TargetNodeIds
                .Select(GetNode)
                .ToList();
            bool isControversy = targetNodes.Any(
                node => node.Kind == KnowledgeNodeKind.Controversy);
            if (check.Options.Count is < 2 or > 4)
            {
                throw new InvalidDataException(
                    $"理解检查“{check.Id}”必须有二到四个选项。");
            }

            EnsureUnique(
                check.Options.Select(option => option.Id),
                $"理解检查“{check.Id}”选项");
            if (check.Options.Any(option =>
                    string.IsNullOrWhiteSpace(option.Text) ||
                    string.IsNullOrWhiteSpace(option.Feedback)))
            {
                throw new InvalidDataException(
                    $"理解检查“{check.Id}”包含空选项或反馈。");
            }

            int correctCount = check.Options.Count(option => option.IsCorrect);
            if (!isControversy &&
                (correctCount != 1 ||
                 string.IsNullOrWhiteSpace(check.CorrectOptionId) ||
                 !check.Options.Any(option =>
                     option.Id == check.CorrectOptionId &&
                     option.IsCorrect)))
            {
                throw new InvalidDataException(
                    $"理解检查“{check.Id}”必须有一个明确正确选项。");
            }
        }

        EnsureRecommendedNextHasNoCycles();
    }

    private void EnsureRecommendedNextHasNoCycles()
    {
        foreach (LessonDefinition start in Lessons)
        {
            var visited = new HashSet<string>(StringComparer.Ordinal);
            LessonDefinition current = start;
            while (current.RecommendedNextLessonId is not null)
            {
                if (!visited.Add(current.Id))
                {
                    throw new InvalidDataException(
                        $"课程章节“{start.Id}”的推荐路径形成循环。");
                }

                current = GetLesson(current.RecommendedNextLessonId);
            }
        }
    }

    private static void EnsureText(string value, string kind)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException($"{kind}不能为空。");
        }
    }

    private static void EnsureUnique(IEnumerable<string> ids, string kind)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string id in ids)
        {
            if (string.IsNullOrWhiteSpace(id) || !seen.Add(id))
            {
                throw new InvalidDataException(
                    $"{kind}存在空 ID 或重复 ID“{id}”。");
            }
        }
    }
}

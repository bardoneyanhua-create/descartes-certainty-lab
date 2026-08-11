using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Descartes.CertaintyLab;

public sealed record ExperiencePack(
    string Id,
    string Title,
    string Opening,
    string ReasonPrompt,
    IReadOnlyList<ReasonDefinition> Reasons,
    string OwnReasonPrompt,
    string OwnReasonPrivateSceneText,
    string OwnReasonTestResult,
    string OwnReasonPathSummary,
    string OwnReasonPrivatePathSummary,
    string QuestionWholeExperienceAction,
    string WholeExperienceText,
    string WholeExperienceDiscovery,
    string WholeExperienceThoughtTraceText,
    string AskWhatRemainsAction,
    string ReflectionText,
    string ReflectionThoughtTraceText,
    string DoubtThinkingAction,
    string ReflexiveDiscovery,
    string CompletionIdentityText,
    string CompletionText,
    IReadOnlyList<ExplanationSectionDefinition> ExplanationSections,
    SourceNoteDefinition SourceNote)
{
    private static readonly string[] RequiredRecordIds =
    [
        "DC-REC-METHODIC-DOUBT",
        "DC-REC-DREAM-ARGUMENT",
        "DC-REC-EVIL-DEMON",
        "DC-REC-COGITO-DISCOURSE",
        "DC-REC-EGO-SUM-MEDITATION",
    ];

    private static readonly string[] RequiredBoundaryIds =
    [
        "DC-BLK-PRIMARY-COLLATION",
        "DC-BLK-EPISTEMIC-CLASS",
    ];

    public static ExperiencePack Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        };

        ExperiencePack pack = JsonSerializer.Deserialize<ExperiencePack>(stream, options)
            ?? throw new InvalidDataException("The experience pack is empty.");

        Validate(pack);
        return pack;
    }

    private static void Validate(ExperiencePack pack)
    {
        EnsureText(pack.Title, "title");
        EnsureText(pack.Opening, "opening");
        EnsureText(pack.ReasonPrompt, "reason prompt");
        EnsureText(pack.OwnReasonPrompt, "own reason prompt");
        EnsureText(pack.OwnReasonPrivateSceneText, "private reason scene");
        EnsureText(pack.OwnReasonTestResult, "own reason test result");
        EnsureText(pack.OwnReasonPathSummary, "own reason path summary");
        EnsureText(pack.OwnReasonPrivatePathSummary, "private reason path summary");
        EnsureText(pack.WholeExperienceThoughtTraceText, "whole-experience thought trace");
        EnsureText(pack.ReflectionThoughtTraceText, "reflection thought trace");
        EnsureText(pack.ReflexiveDiscovery, "reflexive discovery");
        EnsureText(pack.CompletionIdentityText, "completion identity");
        EnsureText(pack.CompletionText, "completion text");

        if (pack.ExplanationSections.Count != 4)
        {
            throw new InvalidDataException(
                "The completion must contain four explanation sections.");
        }

        EnsureUnique(
            pack.ExplanationSections.Select(section => section.Heading),
            "explanation heading");
        if (pack.ExplanationSections.Any(section =>
                string.IsNullOrWhiteSpace(section.Body)))
        {
            throw new InvalidDataException(
                "Every explanation section needs a body.");
        }

        if (pack.Reasons.Count != 2)
        {
            throw new InvalidDataException(
                "The experience must offer two meaningfully different reasons.");
        }

        EnsureUnique(pack.Reasons.Select(reason => reason.Id), "reason");
        if (pack.Reasons.Any(reason =>
                string.IsNullOrWhiteSpace(reason.Text) ||
                string.IsNullOrWhiteSpace(reason.TestResult) ||
                string.IsNullOrWhiteSpace(reason.PathSummary) ||
                string.IsNullOrWhiteSpace(reason.ThoughtTraceText) ||
                string.IsNullOrWhiteSpace(reason.ExplanationBridge)))
        {
            throw new InvalidDataException(
                "Every reason needs text, a consequence, and a path summary.");
        }

        EnsureUnique(pack.SourceNote.RecordIds, "source record");
        EnsureUnique(pack.SourceNote.OpenBoundaryIds, "open boundary");

        if (RequiredRecordIds.Any(required =>
                !pack.SourceNote.RecordIds.Contains(required, StringComparer.Ordinal)))
        {
            throw new InvalidDataException("The source note is missing a required record.");
        }

        if (RequiredBoundaryIds.Any(required =>
                !pack.SourceNote.OpenBoundaryIds.Contains(required, StringComparer.Ordinal)))
        {
            throw new InvalidDataException("The source note is missing an open boundary.");
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

    private static void EnsureUnique(IEnumerable<string> ids, string kind)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string id in ids)
        {
            if (string.IsNullOrWhiteSpace(id) || !seen.Add(id))
            {
                throw new InvalidDataException($"Duplicate or empty {kind} ID '{id}'.");
            }
        }
    }
}

public sealed record ReasonDefinition(
    string Id,
    string Text,
    string TestResult,
    string PathSummary,
    string ThoughtTraceText,
    string ExplanationBridge);

public sealed record ExplanationSectionDefinition(
    string Heading,
    string Body);

public sealed record SourceNoteDefinition(
    string DisplayText,
    IReadOnlyList<string> RecordIds,
    IReadOnlyList<string> OpenBoundaryIds);

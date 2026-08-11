using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Descartes.CertaintyLab.ThoughtCompanion;

public sealed record CompanionPrivacyPreview(
    int ClaimCount,
    int EvidenceCount,
    int CurrentTurnCharacters,
    bool IncludesApprovedExcerpt,
    string ExternalService);

public sealed record CompanionCostEstimate(long MicroCurrencyUnits, string Currency);

public sealed record CompanionAuditEvent(
    DateTimeOffset At,
    CompanionAction Action,
    string Outcome,
    CompanionFailureKind Failure,
    int PromptTokens,
    int CompletionTokens,
    int CacheHitTokens,
    long EstimatedCostMicrounits,
    string CostCurrency);

public interface ICompanionAuditSink
{
    void Write(CompanionAuditEvent auditEvent);
}

internal static class CompanionAuditJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    internal static string Serialize(CompanionAuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        return JsonSerializer.Serialize(auditEvent, Options);
    }
}

public sealed class FileCompanionAuditSink : ICompanionAuditSink
{
    private static readonly object FileGate = new();
    private static readonly HashSet<string> AllowedOutcomes =
        new(["completed", "failed"], StringComparer.Ordinal);

    private readonly string directory;

    public FileCompanionAuditSink()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PhilosophyVault",
            "DescartesSliceV2",
            "ai-audit"))
    {
    }

    internal FileCompanionAuditSink(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ArgumentException("审计目录不能为空。", nameof(directory));
        }

        this.directory = Path.GetFullPath(directory);
    }

    public void Write(CompanionAuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        if (!AllowedOutcomes.Contains(auditEvent.Outcome) ||
            string.IsNullOrWhiteSpace(auditEvent.CostCurrency) ||
            auditEvent.CostCurrency.Length > 8 ||
            auditEvent.PromptTokens < 0 ||
            auditEvent.CompletionTokens < 0 ||
            auditEvent.CacheHitTokens < 0 ||
            auditEvent.EstimatedCostMicrounits < 0)
        {
            throw new ArgumentException("审计事件不符合固定元数据结构。", nameof(auditEvent));
        }

        string line = CompanionAuditJson.Serialize(auditEvent) + Environment.NewLine;
        byte[] bytes = Encoding.UTF8.GetBytes(line);
        try
        {
            lock (FileGate)
            {
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, $"{auditEvent.At:yyyy-MM-dd}.jsonl");
                using var stream = new FileStream(
                    path,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.Read,
                    4096,
                    FileOptions.WriteThrough);
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }
}

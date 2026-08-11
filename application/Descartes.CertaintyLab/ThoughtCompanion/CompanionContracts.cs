using Descartes.CertaintyLab.ThoughtCompanion.Security;

namespace Descartes.CertaintyLab.ThoughtCompanion;

public enum CompanionAction { ReflectMe, QuestionMe, ChallengeMe, CompareMe }
public enum CompanionVoice { SourceSupported, NamedInterpretation, ModernReconstruction, AiQuestion }
public enum CompanionBasisLabel { 资料支持, 具名解释, 现代重构, AI提问 }
public enum CompanionFailureKind { None, MissingCredential, Offline, Timeout, Unauthorized, InsufficientBalance, RateLimited, InvalidResponse, ContentFiltered, ProviderUnavailable, BudgetBlocked, Busy, Cancelled }

public sealed record CompanionEvidence(string Id, string WorkId, string Edition, string Locator, bool LocatorVerified);
public sealed record CompanionClaim(string Id, string Title, string Explanation, CompanionVoice Voice, IReadOnlyList<CompanionEvidence> Evidence);
public sealed record CompanionExplicitExcerpt(string Text, bool UserApprovedForThisRequest);
public sealed record CompanionDraft(CompanionAction Action, string CurrentTurn, IReadOnlyList<CompanionClaim> CurrentPageClaims, CompanionExplicitExcerpt? ExplicitExcerpt);
public sealed record CompanionContext(IReadOnlyList<CompanionClaim> Claims, string CurrentTurn, string? ApprovedExcerpt, IReadOnlySet<string> ClaimIds, IReadOnlySet<string> EvidenceIds);
public sealed record CompanionRequest(string SystemContract, CompanionContext Context, bool IsRepair, string? InvalidResponseCategory, int MaximumPromptTokens = int.MaxValue, int MaximumOutputTokens = int.MaxValue);
public sealed record CompanionSource(string ClaimId, string EvidenceId, CompanionVoice Voice);
public sealed record CompanionAnswer(string Heard, string Question, string Relation, CompanionBasisLabel BasisLabel, IReadOnlyList<CompanionSource> Sources);
public sealed record CompanionUsage(int PromptTokens, int CompletionTokens, int CacheHitTokens, int CacheMissTokens);
public sealed record CompanionProviderResult(string Json, CompanionUsage Usage, string FinishReason);
public sealed record CompanionOperationResult(CompanionAnswer? Answer, CompanionFailureKind Failure, string UserMessage, CompanionUsage? Usage);

public sealed class CompanionProviderException : Exception
{
    public CompanionProviderException(
        CompanionFailureKind kind,
        string safeDiagnostic,
        TimeSpan? retryAfter = null)
        : base(safeDiagnostic)
    {
        if (kind is CompanionFailureKind.None or CompanionFailureKind.MissingCredential or
            CompanionFailureKind.BudgetBlocked or CompanionFailureKind.Busy or CompanionFailureKind.Cancelled)
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }
        if (string.IsNullOrWhiteSpace(safeDiagnostic))
        {
            throw new ArgumentException("Safe diagnostic must not be blank.", nameof(safeDiagnostic));
        }

        Kind = kind;
        SafeDiagnostic = safeDiagnostic;
        RetryAfter = retryAfter;
    }

    public CompanionFailureKind Kind { get; }
    public string SafeDiagnostic { get; }
    public TimeSpan? RetryAfter { get; }
}

public interface IThoughtCompanionProvider
{
    int MaximumOutputTokens { get; }
    Task<CompanionProviderResult> CompleteAsync(
        CompanionRequest request,
        SensitiveBuffer credential,
        CancellationToken cancellationToken);
}

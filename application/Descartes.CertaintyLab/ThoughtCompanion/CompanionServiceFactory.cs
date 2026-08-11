using System.Net.Http;
using System.Diagnostics.CodeAnalysis;
using Descartes.CertaintyLab.ThoughtCompanion.OpenAICompatible;
using Descartes.CertaintyLab.ThoughtCompanion.Security;
using Descartes.CertaintyLab.ThoughtCompanion.Settings;

namespace Descartes.CertaintyLab.ThoughtCompanion;

public sealed record CompanionConnectionTestResult(
    bool IsSuccessful,
    CompanionFailureKind Failure,
    string UserMessage,
    string? Diagnostic);

public interface ICompanionServiceFactory
{
    ICompanionService Create(CompanionProfile profile);
    Task<CompanionConnectionTestResult> TestConnectionAsync(
        CompanionProfile profile,
        CancellationToken cancellationToken);
}

public static class CompanionServiceConsentExtensions
{
    public static Task<CompanionOperationResult> SendAsync(
        this ICompanionService service,
        CompanionDraft draft,
        bool firstSendConsentAccepted,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(service);
        return service is IFirstSendConsentCompanionService consentAware
            ? consentAware.SendWithConsentAsync(draft, firstSendConsentAccepted, cancellationToken)
            : service.SendAsync(draft, cancellationToken);
    }
}

internal interface IFirstSendConsentCompanionService
{
    Task<CompanionOperationResult> SendWithConsentAsync(
        CompanionDraft draft,
        bool firstSendConsentAccepted,
        CancellationToken cancellationToken);
}

internal static class CompanionCredentialPolicy
{
    internal static bool IsMissing([NotNullWhen(false)] SensitiveBuffer? credential)
    {
        if (credential is null)
        {
            return true;
        }

        ReadOnlySpan<char> characters = credential.Span;
        return characters.IsEmpty || characters.IsWhiteSpace();
    }
}

public sealed class CompanionServiceFactory : ICompanionServiceFactory
{
    private readonly HttpClient client;
    private readonly ICredentialStore credentials;
    private readonly CompanionBudgetOptions budgetOptions;
    private readonly ICompanionAuditSink audit;
    private readonly TimeProvider timeProvider;
    private readonly TimeSpan timeout;
    private readonly int maximumOutputTokens;

    public CompanionServiceFactory(
        HttpClient client,
        ICredentialStore credentials,
        CompanionBudgetOptions budgetOptions,
        ICompanionAuditSink audit,
        TimeProvider timeProvider,
        TimeSpan timeout,
        int maximumOutputTokens)
    {
        this.client = client ?? throw new ArgumentNullException(nameof(client));
        this.credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
        this.budgetOptions = budgetOptions ?? throw new ArgumentNullException(nameof(budgetOptions));
        this.audit = audit ?? throw new ArgumentNullException(nameof(audit));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        if (timeout <= TimeSpan.Zero || timeout > TimeSpan.FromMinutes(2))
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }
        if (maximumOutputTokens is < 64 or > 2048)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumOutputTokens));
        }
        this.timeout = timeout;
        this.maximumOutputTokens = maximumOutputTokens;
    }

    public ICompanionService Create(CompanionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (profile.Kind == CompanionProviderKind.OfflineDemo)
        {
            return new FakeCompanionService();
        }

        OpenAICompatibleProvider provider = CreateProvider(profile);
        var coordinator = new CompanionRequestCoordinator(
            new CompanionContextBuilder(3),
            new CompanionResponseValidator(),
            provider,
            credentials,
            new InMemoryCompanionBudget(budgetOptions),
            audit,
            timeProvider,
            profile.CredentialTarget!,
            profile.DisplayName);
        return new CoordinatorCompanionService(coordinator);
    }

    public async Task<CompanionConnectionTestResult> TestConnectionAsync(
        CompanionProfile profile,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (profile.Kind == CompanionProviderKind.OfflineDemo)
        {
            return new(true, CompanionFailureKind.None, "离线演示不需要网络连接。", null);
        }
        if (cancellationToken.IsCancellationRequested)
        {
            return Failure(CompanionFailureKind.Cancelled, "connection-test-cancelled", profile.DisplayName);
        }

        using SensitiveBuffer? credential = credentials.Read(profile.CredentialTarget!);
        if (CompanionCredentialPolicy.IsMissing(credential))
        {
            return Failure(CompanionFailureKind.MissingCredential, "credential-missing", profile.DisplayName);
        }

        try
        {
            CompanionRequest request = new(
                CompanionRequestCoordinator.SystemContract,
                new CompanionContext(
                    Array.Empty<CompanionClaim>(),
                    "connection-test",
                    null,
                    new HashSet<string>(StringComparer.Ordinal),
                    new HashSet<string>(StringComparer.Ordinal)),
                false,
                null,
                4_096,
                maximumOutputTokens);
            await CreateProvider(profile).CompleteAsync(request, credential, cancellationToken).ConfigureAwait(false);
            return new(true, CompanionFailureKind.None, $"已连接 {profile.DisplayName}。", null);
        }
        catch (OperationCanceledException)
        {
            return Failure(CompanionFailureKind.Cancelled, "connection-test-cancelled", profile.DisplayName);
        }
        catch (CompanionProviderException exception)
        {
            return Failure(exception.Kind, exception.SafeDiagnostic, profile.DisplayName);
        }
    }

    private OpenAICompatibleProvider CreateProvider(CompanionProfile profile) =>
        new(client, profile, timeout, maximumOutputTokens, timeProvider);

    private static CompanionConnectionTestResult Failure(
        CompanionFailureKind failure,
        string diagnostic,
        string providerDisplayName) => new(
            false,
            failure,
            failure switch
            {
                CompanionFailureKind.MissingCredential => $"请先安全保存 {providerDisplayName} 凭据。",
                CompanionFailureKind.Unauthorized => $"{providerDisplayName} 凭据无效或已失效。",
                CompanionFailureKind.InsufficientBalance => "服务余额不足。",
                CompanionFailureKind.RateLimited => "服务当前达到调用限制，请稍后手动重试。",
                CompanionFailureKind.Timeout => "连接测试超时，请稍后手动重试。",
                CompanionFailureKind.Offline => "当前无法连接外部服务。",
                CompanionFailureKind.ContentFiltered => "外部服务拒绝了连接测试内容。",
                CompanionFailureKind.InvalidResponse => "服务返回了无法识别的响应。",
                CompanionFailureKind.Cancelled => "连接测试已取消。",
                _ => "外部服务暂时不可用。"
            },
            diagnostic);

    private sealed class CoordinatorCompanionService(CompanionRequestCoordinator coordinator) :
        ICompanionService,
        IFirstSendConsentCompanionService
    {
        public Task<CompanionOperationResult> SendAsync(
            CompanionDraft draft,
            CancellationToken cancellationToken) =>
            SendWithConsentAsync(draft, firstSendConsentAccepted: false, cancellationToken);

        public Task<CompanionOperationResult> SendWithConsentAsync(
            CompanionDraft draft,
            bool firstSendConsentAccepted,
            CancellationToken cancellationToken) =>
            coordinator.SendAsync(draft, firstSendConsentAccepted, cancellationToken);
    }
}

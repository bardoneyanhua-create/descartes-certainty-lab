using Descartes.CertaintyLab.ThoughtCompanion.DeepSeek;
using Descartes.CertaintyLab.ThoughtCompanion.Security;
using System.Text;
using System.Text.Json;

namespace Descartes.CertaintyLab.ThoughtCompanion;

public sealed record CompanionBudgetOptions(
    int SoftTokenLimit,
    int HardTokenLimit,
    int MaximumConcurrency,
    string Currency,
    long CacheHitInputMicrounitsPerMillion,
    long CacheMissInputMicrounitsPerMillion,
    long OutputMicrounitsPerMillion);

public interface ICompanionCallReservation : IDisposable
{
    void MarkDispatched();
    bool TryReconcile(CompanionUsage usage);
}

public interface ICompanionBudgetLease : IDisposable
{
    bool TryReserveCall(int maximumTokens, out ICompanionCallReservation reservation);
}

public interface ICompanionBudget
{
    bool TryEnter(DateOnly localDay, out ICompanionBudgetLease lease, out CompanionFailureKind failure);
    CompanionCostEstimate Estimate(CompanionUsage usage);
    void StopForDay(DateOnly localDay, CompanionFailureKind reason);
}

public sealed class InMemoryCompanionBudget : ICompanionBudget
{
    private readonly object gate = new();
    private readonly CompanionBudgetOptions options;
    private readonly SemaphoreSlim concurrency;
    private DateOnly ledgerDay;
    private bool hasLedgerDay;
    private bool stopped;
    private long chargedTokens;
    private long reservedTokens;
    internal long ChargedTokensForTests { get { lock (gate) return chargedTokens; } }

    public InMemoryCompanionBudget(CompanionBudgetOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.SoftTokenLimit < 0 ||
            options.HardTokenLimit <= 0 ||
            options.SoftTokenLimit > options.HardTokenLimit ||
            options.MaximumConcurrency <= 0 ||
            string.IsNullOrWhiteSpace(options.Currency) ||
            options.Currency.Length > 8 ||
            options.CacheHitInputMicrounitsPerMillion < 0 ||
            options.CacheMissInputMicrounitsPerMillion < 0 ||
            options.OutputMicrounitsPerMillion < 0)
        {
            throw new ArgumentException("AI 预算配置无效。", nameof(options));
        }

        this.options = options;
        concurrency = new SemaphoreSlim(options.MaximumConcurrency, options.MaximumConcurrency);
    }

    public bool TryEnter(DateOnly localDay, out ICompanionBudgetLease lease, out CompanionFailureKind failure)
    {
        lock (gate)
        {
            ResetFor(localDay);
            if (stopped || chargedTokens + reservedTokens >= options.HardTokenLimit)
            {
                lease = NoopLease.Instance;
                failure = CompanionFailureKind.BudgetBlocked;
                return false;
            }
            if (!concurrency.Wait(0))
            {
                lease = NoopLease.Instance;
                failure = CompanionFailureKind.Busy;
                return false;
            }

            lease = new BudgetLease(this, localDay);
            failure = CompanionFailureKind.None;
            return true;
        }
    }

    public CompanionCostEstimate Estimate(CompanionUsage usage)
    {
        ValidateUsage(usage);
        long numerator = checked(
            checked((long)usage.CacheHitTokens * options.CacheHitInputMicrounitsPerMillion) +
            checked((long)usage.CacheMissTokens * options.CacheMissInputMicrounitsPerMillion) +
            checked((long)usage.CompletionTokens * options.OutputMicrounitsPerMillion));
        long microCurrencyUnits = checked(numerator + 999_999) / 1_000_000;
        return new(microCurrencyUnits, options.Currency);
    }

    private bool TryReserve(DateOnly localDay, int maximumTokens, out ICompanionCallReservation reservation)
    {
        if (maximumTokens <= 0) throw new ArgumentOutOfRangeException(nameof(maximumTokens));
        lock (gate)
        {
            if (!hasLedgerDay || ledgerDay != localDay || stopped ||
                checked(chargedTokens + reservedTokens + maximumTokens) > options.HardTokenLimit)
            {
                reservation = NoopCallReservation.Instance;
                return false;
            }
            reservedTokens = checked(reservedTokens + maximumTokens);
            reservation = new CallReservation(this, localDay, maximumTokens);
            return true;
        }
    }

    private bool Record(DateOnly localDay, ref long remainingReservation, CompanionUsage usage)
    {
        ValidateUsage(usage);
        long tokens = checked((long)usage.PromptTokens + usage.CompletionTokens);
        lock (gate)
        {
            if (!hasLedgerDay || ledgerDay != localDay)
            {
                return false;
            }

            if (tokens > remainingReservation)
            {
                chargedTokens = checked(chargedTokens + tokens);
                reservedTokens -= remainingReservation;
                remainingReservation = 0;
                stopped = true;
                return false;
            }

            chargedTokens = checked(chargedTokens + tokens);
            reservedTokens -= tokens;
            remainingReservation -= tokens;
            return true;
        }
    }

    private void FinalizeReservation(DateOnly localDay, ref long remainingReservation, bool consumeWorstCase)
    {
        lock (gate)
        {
            if (hasLedgerDay && ledgerDay == localDay)
            {
                reservedTokens -= remainingReservation;
                if (consumeWorstCase)
                {
                    chargedTokens = checked(chargedTokens + remainingReservation);
                }
            }
            remainingReservation = 0;
        }
    }

    public void StopForDay(DateOnly localDay, CompanionFailureKind reason)
    {
        if (reason is not (CompanionFailureKind.InsufficientBalance or CompanionFailureKind.RateLimited))
        {
            throw new ArgumentException("只有余额不足或限流可以停止当日调用。", nameof(reason));
        }

        lock (gate)
        {
            ResetFor(localDay);
            stopped = true;
        }
    }

    private static void ValidateUsage(CompanionUsage usage)
    {
        ArgumentNullException.ThrowIfNull(usage);
        if (usage.PromptTokens < 0 ||
            usage.CompletionTokens < 0 ||
            usage.CacheHitTokens < 0 ||
            usage.CacheMissTokens < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(usage));
        }
    }

    private void ResetFor(DateOnly localDay)
    {
        if (!hasLedgerDay || ledgerDay != localDay)
        {
            ledgerDay = localDay;
            hasLedgerDay = true;
            chargedTokens = 0;
            reservedTokens = 0;
            stopped = false;
        }
    }

    private sealed class BudgetLease(InMemoryCompanionBudget owner, DateOnly day) : ICompanionBudgetLease
    {
        private InMemoryCompanionBudget? owner = owner;
        public bool TryReserveCall(int maximumTokens, out ICompanionCallReservation reservation) =>
            (owner ?? throw new ObjectDisposedException(nameof(BudgetLease))).TryReserve(day, maximumTokens, out reservation);

        public void Dispose()
        {
            InMemoryCompanionBudget? current = Interlocked.Exchange(ref owner, null);
            if (current is not null) current.concurrency.Release();
        }
    }

    private sealed class CallReservation(InMemoryCompanionBudget owner, DateOnly day, long reservation) : ICompanionCallReservation
    {
        private InMemoryCompanionBudget? owner = owner;
        private long remainingReservation = reservation;
        private bool dispatched;
        private bool reconciled;
        public void MarkDispatched()
        {
            if (owner is null) throw new ObjectDisposedException(nameof(CallReservation));
            dispatched = true;
        }
        public bool TryReconcile(CompanionUsage usage)
        {
            bool result = (owner ?? throw new ObjectDisposedException(nameof(CallReservation)))
                .Record(day, ref remainingReservation, usage);
            reconciled = true;
            return result;
        }
        public void Dispose()
        {
            InMemoryCompanionBudget? current = Interlocked.Exchange(ref owner, null);
            current?.FinalizeReservation(day, ref remainingReservation, dispatched && !reconciled);
        }
    }

    private sealed class NoopLease : ICompanionBudgetLease
    {
        internal static readonly NoopLease Instance = new();
        public bool TryReserveCall(int maximumTokens, out ICompanionCallReservation reservation) { reservation = NoopCallReservation.Instance; return false; }
        public void Dispose() { }
    }

    private sealed class NoopCallReservation : ICompanionCallReservation
    {
        internal static readonly NoopCallReservation Instance = new();
        public void MarkDispatched() { }
        public bool TryReconcile(CompanionUsage usage) => false;
        public void Dispose() { }
    }
}

public sealed class CompanionRequestCoordinator
{
    public const string SystemContract =
        "Return json only with exactly heard, question, relation, basisLabel, and sources. Ask exactly one question. Use only supplied claim and evidence IDs. Never role-play a philosopher. State that current material is insufficient rather than inventing a fact.";

    private readonly CompanionContextBuilder builder;
    private readonly CompanionResponseValidator validator;
    private readonly IThoughtCompanionProvider provider;
    private readonly ICredentialStore credentials;
    private readonly ICompanionBudget budget;
    private readonly ICompanionAuditSink audit;
    private readonly TimeProvider time;
    private readonly string credentialTarget;
    private readonly string providerDisplayName;
    private readonly object commitGate = new();
    private long latestGeneration;

    public CompanionRequestCoordinator(
        CompanionContextBuilder builder,
        CompanionResponseValidator validator,
        IThoughtCompanionProvider provider,
        ICredentialStore credentials,
        ICompanionBudget budget,
        ICompanionAuditSink audit,
        TimeProvider time)
        : this(
            builder,
            validator,
            provider,
            credentials,
            budget,
            audit,
            time,
            WindowsCredentialStore.TargetName,
            "DeepSeek")
    {
    }

    public CompanionRequestCoordinator(
        CompanionContextBuilder builder,
        CompanionResponseValidator validator,
        IThoughtCompanionProvider provider,
        ICredentialStore credentials,
        ICompanionBudget budget,
        ICompanionAuditSink audit,
        TimeProvider time,
        string credentialTarget,
        string providerDisplayName)
    {
        this.builder = builder ?? throw new ArgumentNullException(nameof(builder));
        this.validator = validator ?? throw new ArgumentNullException(nameof(validator));
        this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        this.credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
        this.budget = budget ?? throw new ArgumentNullException(nameof(budget));
        this.audit = audit ?? throw new ArgumentNullException(nameof(audit));
        this.time = time ?? throw new ArgumentNullException(nameof(time));
        if (string.IsNullOrWhiteSpace(credentialTarget))
        {
            throw new ArgumentException("Credential target must not be blank.", nameof(credentialTarget));
        }
        if (string.IsNullOrWhiteSpace(providerDisplayName))
        {
            throw new ArgumentException("Provider display name must not be blank.", nameof(providerDisplayName));
        }
        this.credentialTarget = credentialTarget;
        this.providerDisplayName = providerDisplayName.Trim();
    }

    public CompanionPrivacyPreview CreatePreview(CompanionDraft draft)
    {
        CompanionContext context = builder.Build(draft);
        return new(
            context.Claims.Count,
            context.Claims.Sum(claim => claim.Evidence.Count),
            context.CurrentTurn.Length,
            context.ApprovedExcerpt is not null,
            providerDisplayName);
    }

    public async Task<CompanionOperationResult> SendAsync(
        CompanionDraft draft,
        bool firstSendConsentAccepted,
        CancellationToken cancellationToken)
    {
        if (!firstSendConsentAccepted)
        {
            return Cancelled("本次内容未发送。");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Cancelled("请求已取消，你的输入仍保留在本机。");
        }

        CompanionContext context = builder.Build(draft);
        DateOnly day = DateOnly.FromDateTime(time.GetLocalNow().DateTime);
        if (!budget.TryEnter(day, out ICompanionBudgetLease lease, out CompanionFailureKind gateFailure))
        {
            lease.Dispose();
            return new(
                null,
                gateFailure,
                gateFailure == CompanionFailureKind.Busy
                    ? "已有同行者请求正在进行。"
                    : "今天的 AI 调用已到本地上限。",
                null);
        }

        using (lease)
        {
            long generation = 0;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                using SensitiveBuffer? credential = credentials.Read(credentialTarget);
                if (CompanionCredentialPolicy.IsMissing(credential))
                {
                    return new(
                        null,
                        CompanionFailureKind.MissingCredential,
                        $"请先安全保存 {providerDisplayName} 凭据。",
                        null);
                }

                cancellationToken.ThrowIfCancellationRequested();
                CompanionRequest firstRequest = CreateBoundedRequest(context, false, null);
                if (!lease.TryReserveCall(MaximumUsage(firstRequest), out ICompanionCallReservation firstReservation))
                {
                    return new(null, CompanionFailureKind.BudgetBlocked, "今天的 AI 调用已到本地上限。", null);
                }
                generation = BeginGeneration();

                CompanionProviderResult raw;
                using (firstReservation)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    firstReservation.MarkDispatched();
                    raw = await provider.CompleteAsync(firstRequest, credential, cancellationToken).ConfigureAwait(false);
                    if (!firstReservation.TryReconcile(raw.Usage))
                    {
                        return new(null, CompanionFailureKind.InvalidResponse, "服务返回了超出请求上限的用量，今天不会继续调用。", null);
                    }
                }
                CompanionUsage totalUsage = raw.Usage;
                ThrowIfCannotContinue(generation, cancellationToken);

                CompanionValidationResult validation = validator.Validate(raw.Json, context);
                if (!validation.IsValid)
                {
                    CompanionRequest repairRequest = CreateBoundedRequest(context, true, validation.FailureCode);
                    if (!lease.TryReserveCall(MaximumUsage(repairRequest), out ICompanionCallReservation repairReservation))
                    {
                        return new(null, CompanionFailureKind.BudgetBlocked, "剩余本地预算不足以安全执行修复请求。", null);
                    }
                    using (repairReservation)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        repairReservation.MarkDispatched();
                        raw = await provider.CompleteAsync(repairRequest, credential, cancellationToken).ConfigureAwait(false);
                        if (!repairReservation.TryReconcile(raw.Usage))
                        {
                            return new(null, CompanionFailureKind.InvalidResponse, "服务返回了超出请求上限的用量，今天不会继续调用。", null);
                        }
                    }
                    totalUsage = AddUsage(totalUsage, raw.Usage);
                    ThrowIfCannotContinue(generation, cancellationToken);
                    validation = validator.Validate(raw.Json, context);
                }

                if (!validation.IsValid)
                {
                    WriteFailureIfCurrent(generation, cancellationToken, draft.Action, CompanionFailureKind.InvalidResponse);
                    return new(
                        null,
                        CompanionFailureKind.InvalidResponse,
                        "回答没有通过来源与结构检查，未展示。",
                        null);
                }

                lock (commitGate)
                {
                    ThrowIfCannotContinueUnderLock(generation, cancellationToken);
                    CompanionCostEstimate cost = budget.Estimate(totalUsage);
                    audit.Write(new CompanionAuditEvent(
                        time.GetUtcNow(),
                        draft.Action,
                        "completed",
                        CompanionFailureKind.None,
                        totalUsage.PromptTokens,
                        totalUsage.CompletionTokens,
                        totalUsage.CacheHitTokens,
                        cost.MicroCurrencyUnits,
                        cost.Currency));
                    return new(validation.Answer, CompanionFailureKind.None, string.Empty, totalUsage);
                }
            }
            catch (OperationCanceledException)
            {
                return Cancelled("请求已取消，你的输入仍保留在本机。");
            }
            catch (CompanionProviderException exception)
            {
                lock (commitGate)
                {
                    if (!CanContinueUnderLock(generation, cancellationToken))
                    {
                        return Cancelled("请求已取消，你的输入仍保留在本机。");
                    }

                    if (exception.Kind is CompanionFailureKind.InsufficientBalance or CompanionFailureKind.RateLimited)
                    {
                        budget.StopForDay(day, exception.Kind);
                    }

                    audit.Write(new CompanionAuditEvent(
                        time.GetUtcNow(),
                        draft.Action,
                        "failed",
                        exception.Kind,
                        0,
                        0,
                        0,
                        0,
                        "configured"));
                    return new(null, exception.Kind, UserMessage(exception.Kind, providerDisplayName), null);
                }
            }
        }
    }

    private long BeginGeneration()
    {
        lock (commitGate)
        {
            return ++latestGeneration;
        }
    }

    private void ThrowIfCannotContinue(long generation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (commitGate)
        {
            ThrowIfCannotContinueUnderLock(generation, cancellationToken);
        }
    }

    private void ThrowIfCannotContinueUnderLock(long generation, CancellationToken cancellationToken)
    {
        if (!CanContinueUnderLock(generation, cancellationToken))
        {
            throw new OperationCanceledException(cancellationToken);
        }
    }

    private bool CanContinueUnderLock(long generation, CancellationToken cancellationToken) =>
        generation == latestGeneration && !cancellationToken.IsCancellationRequested;

    private void WriteFailureIfCurrent(
        long generation,
        CancellationToken cancellationToken,
        CompanionAction action,
        CompanionFailureKind failure)
    {
        lock (commitGate)
        {
            ThrowIfCannotContinueUnderLock(generation, cancellationToken);
            audit.Write(new CompanionAuditEvent(
                time.GetUtcNow(), action, "failed", failure, 0, 0, 0, 0, "configured"));
        }
    }

    private static CompanionOperationResult Cancelled(string message) =>
        new(null, CompanionFailureKind.Cancelled, message, null);

    private static string UserMessage(CompanionFailureKind kind, string providerDisplayName) => kind switch
    {
        CompanionFailureKind.Unauthorized => $"{providerDisplayName} 凭据已失效，可以安全替换。",
        CompanionFailureKind.InsufficientBalance => "服务余额不足，今天不会继续自动调用。",
        CompanionFailureKind.RateLimited => "服务达到调用限制，今天不会继续自动调用。",
        CompanionFailureKind.Timeout => "连接超时，你的输入仍保留在本机，可稍后手动重试。",
        CompanionFailureKind.Offline => "当前无法连接外部服务，你的输入仍保留在本机。",
        CompanionFailureKind.ContentFiltered => "外部服务没有生成回答，本地不会伪造替代答案。",
        _ => "外部服务暂时不可用，没有生成回答。"
    };

    private static CompanionUsage AddUsage(CompanionUsage left, CompanionUsage right) => new(
        checked(left.PromptTokens + right.PromptTokens),
        checked(left.CompletionTokens + right.CompletionTokens),
        checked(left.CacheHitTokens + right.CacheHitTokens),
        checked(left.CacheMissTokens + right.CacheMissTokens));

    private CompanionRequest CreateBoundedRequest(
        CompanionContext context,
        bool isRepair,
        string? invalidResponseCategory)
    {
        int promptBytes = checked(
            Encoding.UTF8.GetByteCount(SystemContract) +
            Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(context)) +
            Encoding.UTF8.GetByteCount(invalidResponseCategory ?? string.Empty) +
            256);
        return new CompanionRequest(
            SystemContract,
            context,
            isRepair,
            invalidResponseCategory,
            promptBytes,
            provider.MaximumOutputTokens);
    }

    private static int MaximumUsage(CompanionRequest request) =>
        checked(request.MaximumPromptTokens + request.MaximumOutputTokens);
}

using System.ComponentModel;
using System.Runtime.CompilerServices;
using Descartes.CertaintyLab.ThoughtCompanion.Settings;

namespace Descartes.CertaintyLab.ThoughtCompanion;

public interface ICompanionService
{
    Task<CompanionOperationResult> SendAsync(
        CompanionDraft draft,
        CancellationToken cancellationToken);
}

public sealed class CompanionViewModel : INotifyPropertyChanged
{
    private static readonly IReadOnlyList<CompanionSource> NoSources =
        Array.Empty<CompanionSource>();

    private readonly ICompanionService service;
    private readonly CompanionPresentation presentation;
    private readonly CompanionConsentSession consentSession;
    private readonly ICompanionConsentPrompt consentPrompt;
    private IReadOnlyList<CompanionClaim> currentPageClaims =
        Array.Empty<CompanionClaim>();
    private CancellationTokenSource? activeCancellation;
    private long requestGeneration;
    private CompanionAction selectedAction = CompanionAction.ReflectMe;
    private string userText = string.Empty;
    private bool isLessonReady;
    private bool isBusy;
    private bool hasResponse;
    private string heard = string.Empty;
    private string question = string.Empty;
    private string relation = string.Empty;
    private string basisText = string.Empty;
    private IReadOnlyList<CompanionSource> sources = NoSources;
    private string status = "请先打开一篇文章。";
    private string accessibleStatus =
        "思想同行者不可用：请先打开一篇文章。";

    public CompanionViewModel(ICompanionService service)
        : this(
            service,
            CompanionPresentation.ForProfile(CompanionSettings.Default.Profiles.Single(
                profile => profile.Kind == CompanionProviderKind.OfflineDemo)),
            new CompanionConsentSession(preapproved: true),
            DenyConsentPrompt.Instance)
    {
    }

    public CompanionViewModel(
        ICompanionService service,
        CompanionPresentation presentation,
        CompanionConsentSession consentSession,
        ICompanionConsentPrompt consentPrompt)
    {
        this.service = service ?? throw new ArgumentNullException(nameof(service));
        this.presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));
        this.consentSession = consentSession ?? throw new ArgumentNullException(nameof(consentSession));
        this.consentPrompt = consentPrompt ?? throw new ArgumentNullException(nameof(consentPrompt));
    }

    public string ProfileBadgeText => presentation.BadgeText;

    public CompanionAction SelectedAction
    {
        get => selectedAction;
        set => SetField(ref selectedAction, value);
    }

    public string UserText
    {
        get => userText;
        set => SetField(ref userText, value ?? string.Empty);
    }

    public bool IsBusy
    {
        get => isBusy;
        private set
        {
            if (SetField(ref isBusy, value))
            {
                OnPropertyChanged(nameof(SendCancelText));
            }
        }
    }

    public bool IsLessonReady
    {
        get => isLessonReady;
        private set => SetField(ref isLessonReady, value);
    }

    public bool HasResponse
    {
        get => hasResponse;
        private set => SetField(ref hasResponse, value);
    }

    public string Heard
    {
        get => heard;
        private set => SetField(ref heard, value);
    }

    public string Question
    {
        get => question;
        private set => SetField(ref question, value);
    }

    public string Relation
    {
        get => relation;
        private set => SetField(ref relation, value);
    }

    public string BasisText
    {
        get => basisText;
        private set => SetField(ref basisText, value);
    }

    public IReadOnlyList<CompanionSource> Sources
    {
        get => sources;
        private set => SetField(ref sources, value);
    }

    public string Status
    {
        get => status;
        private set => SetField(ref status, value);
    }

    public string AccessibleStatus
    {
        get => accessibleStatus;
        private set => SetField(ref accessibleStatus, value);
    }

    public string SendCancelText => IsBusy ? "取消" : "发送";

    public string AnswerAutomationText => HasResponse
        ? $"我听见你在说：{Heard} 值得停一下的问题：{Question} 它与本篇的关系：{Relation} 依据：{BasisText}"
        : string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? ResponseCommitted;

    public void SetLesson(LearningPack pack, LessonDefinition lesson)
    {
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentNullException.ThrowIfNull(lesson);

        Cancel();
        currentPageClaims = lesson.NodeIds
            .Select(pack.GetNode)
            .Select(node => SnapshotClaim(pack, node))
            .ToArray();
        IsLessonReady = currentPageClaims.Count > 0;
        UserText = string.Empty;
        ClearResponse();
        if (IsLessonReady)
        {
            Status = "写下你目前的想法，再选择同行方式。";
            AccessibleStatus = "思想同行者已可用。";
        }
        else
        {
            Status = "请先打开一篇包含可用论点的文章。";
            AccessibleStatus = "思想同行者不可用：当前文章没有可用论点。";
        }
    }

    public async Task SendAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (!IsLessonReady || currentPageClaims.Count == 0)
        {
            Status = "请先打开一篇文章。";
            AccessibleStatus = "思想同行者不可用：请先打开一篇文章。";
            return;
        }

        string trimmed = UserText.Trim();
        if (trimmed.Length == 0)
        {
            Status = "请先写下你目前的想法。";
            return;
        }

        if (trimmed.Length > 600)
        {
            Status = "本轮输入不能超过 600 个字符。";
            return;
        }

        var draft = new CompanionDraft(
            SelectedAction,
            trimmed,
            currentPageClaims,
            null);
        bool consentAccepted = !presentation.IsRemote ||
            consentSession.EnsureApproved(presentation.Profile, consentPrompt);
        if (!consentAccepted)
        {
            Status = "未发送；你的输入仍保留。";
            AccessibleStatus = "未发送到外部服务；输入仍保留。";
            return;
        }

        long generation = Interlocked.Increment(ref requestGeneration);
        var cancellation = new CancellationTokenSource();
        activeCancellation = cancellation;
        CancellationToken token = cancellation.Token;
        ClearResponse();
        IsBusy = true;
        Status = "正在回答";
        AccessibleStatus = "思想同行者正在回答。";

        try
        {
            CompanionOperationResult result = await service.SendAsync(
                draft,
                firstSendConsentAccepted: consentAccepted,
                token);
            if (token.IsCancellationRequested ||
                generation != Volatile.Read(ref requestGeneration))
            {
                return;
            }

            if (result.Failure != CompanionFailureKind.None ||
                result.Answer is null)
            {
                string failureMessage = string.IsNullOrWhiteSpace(result.UserMessage)
                    ? "这次回答没有生成；你的输入仍保留，可以稍后重试。"
                    : result.UserMessage;
                Status = "回答失败：" + failureMessage;
                AccessibleStatus = "思想同行者回答失败；输入仍保留。";
                return;
            }

            CompanionAnswer answer = result.Answer;
            Heard = answer.Heard;
            Question = answer.Question;
            Relation = answer.Relation;
            BasisText = BasisLabel(answer.BasisLabel);
            Sources = answer.Sources.ToArray();
            HasResponse = true;
            OnPropertyChanged(nameof(AnswerAutomationText));
            Status = "回答完成";
            AccessibleStatus = "思想同行者回答完成。";
            ResponseCommitted?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            if (generation == Volatile.Read(ref requestGeneration))
            {
                Status = "已取消本次回答。";
                AccessibleStatus = "思想同行者请求已取消。";
            }
        }
        catch (Exception)
        {
            if (generation == Volatile.Read(ref requestGeneration))
            {
                Status = "回答失败：这次回答没有生成；你的输入仍保留，可以稍后重试。";
                AccessibleStatus = "思想同行者回答失败；输入仍保留。";
            }
        }
        finally
        {
            if (generation == Volatile.Read(ref requestGeneration))
            {
                IsBusy = false;
                activeCancellation?.Dispose();
                activeCancellation = null;
            }
        }
    }

    public void Cancel()
    {
        if (!IsBusy)
        {
            return;
        }

        Interlocked.Increment(ref requestGeneration);
        CancellationTokenSource? cancellation = activeCancellation;
        activeCancellation = null;
        cancellation?.Cancel();
        cancellation?.Dispose();
        IsBusy = false;
        Status = "已取消本次回答。";
        AccessibleStatus = "思想同行者请求已取消。";
    }

    private void ClearResponse()
    {
        HasResponse = false;
        Heard = string.Empty;
        Question = string.Empty;
        Relation = string.Empty;
        BasisText = string.Empty;
        Sources = NoSources;
        OnPropertyChanged(nameof(AnswerAutomationText));
    }

    private static CompanionClaim SnapshotClaim(
        LearningPack pack,
        KnowledgeNodeDefinition node)
    {
        CompanionVoice voice = node.Identity switch
        {
            "author-text-paraphrase" => CompanionVoice.SourceSupported,
            "named-interpretation" => CompanionVoice.NamedInterpretation,
            _ => CompanionVoice.ModernReconstruction,
        };
        CompanionEvidence[] evidence = node.EvidenceLinkIds
            .Select(id => pack.EvidenceLinks.Single(item =>
                string.Equals(item.Id, id, StringComparison.Ordinal)))
            .Select(item => new CompanionEvidence(
                item.Id,
                item.WorkId,
                item.Edition,
                item.Locator,
                item.LocatorVerified))
            .ToArray();
        return new CompanionClaim(
            node.Id,
            node.ReaderTitle,
            node.Explanation,
            voice,
            Array.AsReadOnly(evidence));
    }

    private static string BasisLabel(CompanionBasisLabel basis) => basis switch
    {
        CompanionBasisLabel.资料支持 => "资料支持",
        CompanionBasisLabel.具名解释 => "具名解释",
        CompanionBasisLabel.现代重构 => "现代重构",
        CompanionBasisLabel.AI提问 => "AI 提问",
        _ => throw new ArgumentOutOfRangeException(nameof(basis)),
    };

    private bool SetField<T>(
        ref T field,
        T value,
        [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private sealed class DenyConsentPrompt : ICompanionConsentPrompt
    {
        internal static DenyConsentPrompt Instance { get; } = new();
        public bool Confirm(CompanionConsentRequest request) => false;
    }
}

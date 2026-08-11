using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace Descartes.CertaintyLab;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly ExperiencePack pack;
    private readonly LabSession session;
    private LabSnapshot snapshot;
    private ReasonChoiceViewModel? selectedReason;
    private string ownReasonText = string.Empty;
    private bool showSourceNote;

    public MainViewModel(ExperiencePack pack)
    {
        this.pack = pack ?? throw new ArgumentNullException(nameof(pack));
        session = LabSession.Start(pack);
        snapshot = session.Snapshot;
        ReasonChoices = pack.Reasons
            .Select(reason => new ReasonChoiceViewModel(reason.Id, reason.Text))
            .ToArray();

        PrimaryActionCommand = new RelayCommand(_ => RunPrimaryAction());
        ChooseSelectedReasonCommand = new RelayCommand(_ => ChooseSelectedReason());
        UseOwnReasonCommand = new RelayCommand(_ => UseOwnReason());
        RehearSceneCommand = new RelayCommand(
            _ => FocusRequest?.Invoke(
                this,
                new FocusRequestEventArgs(FocusTarget.SceneSummary)));
        ToggleSourcesCommand = new RelayCommand(_ => ToggleSources());
        RestartCommand = new RelayCommand(_ => Restart());

        Refresh();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler<FocusRequestEventArgs>? FocusRequest;

    public string Title => pack.Title;

    public LabStage Stage => snapshot.Stage;

    public string SceneHeading => snapshot.Stage switch
    {
        LabStage.Opening => "清晨",
        LabStage.ChoosingReason => "你会怎么确认",
        LabStage.ReasonTested => "这个办法够可靠吗",
        LabStage.EvidenceQuestioned => "问题不在某一件东西",
        LabStage.Reflection => "还剩下一件正在发生的事",
        LabStage.Complete => "这是笛卡尔的怀疑方法",
        _ => string.Empty,
    };

    public string SceneText => snapshot.SceneText;

    public string SceneSummaryAutomationName =>
        ShowThoughtTrace
            ? $"{SceneHeading}。{SceneText}。{ThoughtTraceHeading}。{ThoughtTraceText}"
            : $"{SceneHeading}。{SceneText}";

    public bool ShowSceneSummary => snapshot.Stage != LabStage.Complete;

    public bool ShowSceneControls => snapshot.Stage != LabStage.Complete;

    public bool ShowThoughtTrace => snapshot.Stage is
        LabStage.ReasonTested or
        LabStage.EvidenceQuestioned or
        LabStage.Reflection;

    public string ThoughtTraceHeading => snapshot.Stage switch
    {
        LabStage.ReasonTested => "你暂时依靠的依据",
        LabStage.EvidenceQuestioned => "怀疑扩大之后",
        LabStage.Reflection => "现在还剩什么",
        _ => string.Empty,
    };

    public string ThoughtTraceText => snapshot.ThoughtTraceText;

    public string LatestDiscovery => snapshot.LatestDiscovery;

    public string StatusNotification { get; private set; } = string.Empty;

    public bool ShowLatestDiscovery =>
        snapshot.Stage != LabStage.Complete &&
        !string.IsNullOrWhiteSpace(snapshot.LatestDiscovery);

    public string ReasonPrompt => pack.ReasonPrompt;

    public string OwnReasonPrompt => pack.OwnReasonPrompt;

    public string OwnReasonActionText =>
        string.IsNullOrWhiteSpace(OwnReasonText)
            ? "先不写下来，继续想"
            : "沿我的办法继续";

    public bool ShowReasonChoices => snapshot.Stage == LabStage.ChoosingReason;

    public bool ShowPrimaryAction => snapshot.Stage is
        LabStage.Opening or
        LabStage.ReasonTested or
        LabStage.EvidenceQuestioned or
        LabStage.Reflection;

    public string PrimaryActionText => snapshot.Stage switch
    {
        LabStage.Opening => "想一个确认办法",
        LabStage.ReasonTested => pack.QuestionWholeExperienceAction,
        LabStage.EvidenceQuestioned => pack.AskWhatRemainsAction,
        LabStage.Reflection => pack.DoubtThinkingAction,
        _ => string.Empty,
    };

    public bool ShowCompletion => snapshot.Stage == LabStage.Complete;

    public string CompletionHeading => "这是笛卡尔的怀疑方法";

    public string CompletionIdentityText => pack.CompletionIdentityText;

    public string CompletionText => snapshot.CompletionText;

    public IReadOnlyList<ExplanationSectionDefinition> ExplanationSections =>
        pack.ExplanationSections;

    public string CompletionAutomationText
    {
        get
        {
            var parts = new List<string>
            {
                CompletionHeading,
                CompletionIdentityText,
                "你刚才发现了什么",
                CompletionText,
            };

            if (!string.IsNullOrWhiteSpace(PersonalizedExplanationText))
            {
                parts.Add("你的路线为什么会走到这里");
                parts.Add(PersonalizedExplanationText);
            }

            parts.Add("这段思想到底在说什么");
            foreach (ExplanationSectionDefinition section in ExplanationSections)
            {
                parts.Add(section.Heading);
                parts.Add(section.Body);
            }

            return string.Join("。", parts);
        }
    }

    public string PersonalPathText
    {
        get
        {
            if (snapshot.SelectedReasonId == "own-reason")
            {
                return snapshot.OwnReasonText.Length == 0
                    ? pack.OwnReasonPrivatePathSummary
                    : string.Format(
                        System.Globalization.CultureInfo.CurrentCulture,
                        pack.OwnReasonPathSummary,
                        snapshot.OwnReasonText);
            }

            ReasonDefinition? reason = pack.Reasons.SingleOrDefault(
                item => item.Id == snapshot.SelectedReasonId);
            return reason is null
                ? string.Empty
                : reason.PathSummary;
        }
    }

    public string PersonalizedExplanationText
    {
        get
        {
            if (snapshot.SelectedReasonId == "own-reason")
            {
                string startingPoint = snapshot.OwnReasonText.Length == 0
                    ? "你保留给自己的办法"
                    : $"“{snapshot.OwnReasonText}”";
                return $"你从{startingPoint}出发。它没有被判成错误；你只是用同一个问题检验它：它是否也可能完整地出现在梦里？这正是方法性怀疑的做法——不急着否定，而是检查它能否成为绝对可靠的起点。";
            }

            ReasonDefinition? reason = pack.Reasons.SingleOrDefault(
                item => item.Id == snapshot.SelectedReasonId);
            return reason is null
                ? string.Empty
                : $"{reason.ExplanationBridge} 这正是方法性怀疑的做法——不急着否定，而是检查它能否成为绝对可靠的起点。";
        }
    }

    public string SourceNoteText => pack.SourceNote.DisplayText;

    public bool ShowSourceNote
    {
        get => showSourceNote;
        private set
        {
            if (showSourceNote == value)
            {
                return;
            }

            showSourceNote = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SourceButtonText));
        }
    }

    public string SourceButtonText =>
        ShowSourceNote ? "收起思想来源" : "思想来源（可选）";

    public IReadOnlyList<ReasonChoiceViewModel> ReasonChoices { get; }

    public ReasonChoiceViewModel? SelectedReason
    {
        get => selectedReason;
        set
        {
            if (Equals(selectedReason, value))
            {
                return;
            }

            selectedReason = value;
            OnPropertyChanged();
        }
    }

    public string OwnReasonText
    {
        get => ownReasonText;
        set
        {
            string normalized = value ?? string.Empty;
            if (ownReasonText == normalized)
            {
                return;
            }

            ownReasonText = normalized;
            OnPropertyChanged();
            OnPropertyChanged(nameof(OwnReasonActionText));
        }
    }

    public ICommand PrimaryActionCommand { get; }

    public ICommand ChooseSelectedReasonCommand { get; }

    public ICommand UseOwnReasonCommand { get; }

    public ICommand RehearSceneCommand { get; }

    public ICommand ToggleSourcesCommand { get; }

    public ICommand RestartCommand { get; }

    private void RunPrimaryAction()
    {
        snapshot = snapshot.Stage switch
        {
            LabStage.Opening => session.Begin(),
            LabStage.ReasonTested => session.QuestionWholeExperience(),
            LabStage.EvidenceQuestioned => session.AskWhatRemains(),
            LabStage.Reflection => session.DoubtThinking(),
            _ => snapshot,
        };

        FocusTarget focusTarget = snapshot.Stage switch
        {
            LabStage.ChoosingReason => FocusTarget.ReasonChoices,
            LabStage.Complete => FocusTarget.CompletionSummary,
            _ => FocusTarget.SceneSummary,
        };

        StatusNotification = string.Empty;
        Refresh();
        FocusRequest?.Invoke(this, new FocusRequestEventArgs(focusTarget));
    }

    private void ChooseSelectedReason()
    {
        if (snapshot.Stage != LabStage.ChoosingReason || SelectedReason is null)
        {
            return;
        }

        snapshot = session.ChooseReason(SelectedReason.Id);
        StatusNotification = snapshot.LatestDiscovery;
        Refresh();
        FocusRequest?.Invoke(
            this,
            new FocusRequestEventArgs(FocusTarget.SceneSummary));
    }

    private void UseOwnReason()
    {
        if (snapshot.Stage != LabStage.ChoosingReason)
        {
            return;
        }

        snapshot = session.UseOwnReason(OwnReasonText);
        StatusNotification = snapshot.LatestDiscovery;
        Refresh();
        FocusRequest?.Invoke(
            this,
            new FocusRequestEventArgs(FocusTarget.SceneSummary));
    }

    private void ToggleSources()
    {
        ShowSourceNote = !ShowSourceNote;
        FocusRequest?.Invoke(
            this,
            new FocusRequestEventArgs(
                ShowSourceNote
                    ? FocusTarget.SourceNote
                    : FocusTarget.SourceButton));
    }

    private void Restart()
    {
        snapshot = session.Restart();
        StatusNotification = string.Empty;
        OwnReasonText = string.Empty;
        ShowSourceNote = false;
        SelectedReason = ReasonChoices.FirstOrDefault();
        Refresh();
        FocusRequest?.Invoke(
            this,
            new FocusRequestEventArgs(FocusTarget.SceneSummary));
    }

    private void Refresh()
    {
        SelectedReason ??= ReasonChoices.FirstOrDefault();
        OnPropertyChanged(string.Empty);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class ReasonChoiceViewModel(string id, string text)
{
    public string Id { get; } = id;

    public string Text { get; } = text;

    public string AutomationName => $"{Text}。按回车沿这条理由继续。";
}

public enum FocusTarget
{
    SceneSummary,
    ReasonChoices,
    CompletionSummary,
    SourceNote,
    SourceButton,
}

public sealed record FocusRequestEventArgs(FocusTarget Target);

internal sealed class RelayCommand(Action<object?> execute) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => execute(parameter);
}

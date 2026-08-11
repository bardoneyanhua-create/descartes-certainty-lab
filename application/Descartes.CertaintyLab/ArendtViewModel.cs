using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace Descartes.CertaintyLab;

public sealed class ArendtViewModel : INotifyPropertyChanged
{
    private readonly ArendtExperiencePack pack;
    private readonly ArendtSession session;
    private ArendtSnapshot snapshot;
    private ArendtStartingPointViewModel? selectedStartingPoint;
    private string ownThought = string.Empty;
    private bool showSourceNote;

    public ArendtViewModel(ArendtExperiencePack pack)
    {
        this.pack = pack ?? throw new ArgumentNullException(nameof(pack));
        session = ArendtSession.Start(pack);
        snapshot = session.Snapshot;
        StartingPoints = pack.StartingPoints
            .Select(item => new ArendtStartingPointViewModel(item.Id, item.Text))
            .ToArray();
        selectedStartingPoint = StartingPoints.FirstOrDefault();

        ChooseStartingPointCommand = new RelayCommand(ChooseStartingPoint);
        UseOwnThoughtCommand = new RelayCommand(_ => UseOwnThought());
        PrimaryActionCommand = new RelayCommand(_ => RunPrimaryAction());
        ToggleSourcesCommand = new RelayCommand(_ => ToggleSources());
        RestartCommand = new RelayCommand(_ => Restart());
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler<ArendtFocusRequestEventArgs>? FocusRequest;

    public string Title => pack.Title;

    public ArendtStage Stage => snapshot.Stage;

    public string SceneHeading => snapshot.Stage switch
    {
        ArendtStage.Opening => "窗口前",
        ArendtStage.ReplyArrives => "回信到了",
        ArendtStage.BoundaryFound => "真正缺少的东西",
        ArendtStage.Complete => "这是汉娜·阿伦特提出的问题",
        _ => string.Empty,
    };

    public string SceneText => snapshot.SceneText;

    public string SceneAutomationText =>
        string.IsNullOrWhiteSpace(snapshot.DiscoveryText)
            ? $"{SceneHeading}。{SceneText}"
            : $"{SceneHeading}。{SceneText}。你刚才发现。{snapshot.DiscoveryText}";

    public string Prompt => pack.Prompt;

    public string OwnThoughtPrompt => pack.OwnThoughtPrompt;

    public IReadOnlyList<ArendtStartingPointViewModel> StartingPoints { get; }

    public ArendtStartingPointViewModel? SelectedStartingPoint
    {
        get => selectedStartingPoint;
        set
        {
            if (Equals(selectedStartingPoint, value))
            {
                return;
            }

            selectedStartingPoint = value;
            OnPropertyChanged();
        }
    }

    public string OwnThought
    {
        get => ownThought;
        set
        {
            string normalized = value ?? string.Empty;
            if (ownThought == normalized)
            {
                return;
            }

            ownThought = normalized;
            OnPropertyChanged();
            OnPropertyChanged(nameof(OwnThoughtActionText));
        }
    }

    public string OwnThoughtActionText =>
        string.IsNullOrWhiteSpace(OwnThought)
            ? "我还拿不准，先继续看"
            : "带着我的想法继续";

    public bool ShowScene => snapshot.Stage != ArendtStage.Complete;

    public bool ShowStartingPoints => snapshot.Stage == ArendtStage.Opening;

    public bool ShowDiscovery =>
        snapshot.Stage is ArendtStage.ReplyArrives or ArendtStage.BoundaryFound;

    public string DiscoveryText => snapshot.DiscoveryText;

    public string StatusNotification { get; private set; } = string.Empty;

    public bool ShowPrimaryAction =>
        snapshot.Stage is ArendtStage.ReplyArrives or ArendtStage.BoundaryFound;

    public string PrimaryActionText => snapshot.Stage switch
    {
        ArendtStage.ReplyArrives => "看看真正缺少的是什么",
        ArendtStage.BoundaryFound => "揭开这个问题的来处",
        _ => string.Empty,
    };

    public bool ShowCompletion => snapshot.Stage == ArendtStage.Complete;

    public string CompletionHeading => "这是汉娜·阿伦特关于“拥有权利的权利”的思考";

    public string CompletionIdentityText => snapshot.CompletionIdentityText;

    public string CompletionText => snapshot.CompletionText;

    public string PersonalPathText => snapshot.PersonalPathText;

    public string InterpretationText => pack.InterpretationText;

    public string BoundaryText => pack.BoundaryText;

    public string CausalBoundary => pack.CausalBoundary;

    public IReadOnlyList<LifeAndThoughtStepDefinition> LifeAndThoughtSteps =>
        pack.LifeAndThoughtSteps;

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
            if (!string.IsNullOrWhiteSpace(PersonalPathText))
            {
                parts.Add("你从哪里进入了这个问题");
                parts.Add(PersonalPathText);
            }

            parts.Add("人物经历与思想");
            foreach (LifeAndThoughtStepDefinition step in LifeAndThoughtSteps)
            {
                parts.Add(step.Heading);
                parts.Add(step.Body);
            }

            parts.Add("这段思想在说什么");
            parts.Add(InterpretationText);
            parts.Add("它不意味着什么");
            parts.Add(BoundaryText);
            parts.Add(CausalBoundary);
            parts.Add(pack.SourceNote.DisplayText);
            return string.Join("。", parts);
        }
    }

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

    public string SourceNoteText => pack.SourceNote.DisplayText;

    public ICommand ChooseStartingPointCommand { get; }

    public ICommand UseOwnThoughtCommand { get; }

    public ICommand PrimaryActionCommand { get; }

    public ICommand ToggleSourcesCommand { get; }

    public ICommand RestartCommand { get; }

    private void ChooseStartingPoint(object? parameter)
    {
        ArendtStartingPointViewModel? choice =
            parameter as ArendtStartingPointViewModel ?? SelectedStartingPoint;
        if (snapshot.Stage != ArendtStage.Opening || choice is null)
        {
            return;
        }

        SelectedStartingPoint = choice;
        snapshot = session.ChooseStartingPoint(choice.Id);
        StatusNotification = snapshot.DiscoveryText;
        Refresh();
        FocusRequest?.Invoke(
            this,
            new ArendtFocusRequestEventArgs(ArendtFocusTarget.Scene));
    }

    private void UseOwnThought()
    {
        if (snapshot.Stage != ArendtStage.Opening)
        {
            return;
        }

        snapshot = session.UseOwnThought(OwnThought);
        StatusNotification = snapshot.DiscoveryText;
        Refresh();
        FocusRequest?.Invoke(
            this,
            new ArendtFocusRequestEventArgs(ArendtFocusTarget.Scene));
    }

    private void RunPrimaryAction()
    {
        snapshot = snapshot.Stage switch
        {
            ArendtStage.ReplyArrives => session.ExamineReply(),
            ArendtStage.BoundaryFound => session.RevealThinker(),
            _ => snapshot,
        };

        StatusNotification = snapshot.DiscoveryText;
        Refresh();
        FocusRequest?.Invoke(
            this,
            new ArendtFocusRequestEventArgs(
                snapshot.Stage == ArendtStage.Complete
                    ? ArendtFocusTarget.Completion
                    : ArendtFocusTarget.Scene));
    }

    private void ToggleSources()
    {
        ShowSourceNote = !ShowSourceNote;
        FocusRequest?.Invoke(
            this,
            new ArendtFocusRequestEventArgs(
                ShowSourceNote
                    ? ArendtFocusTarget.SourceNote
                    : ArendtFocusTarget.SourceButton));
    }

    private void Restart()
    {
        snapshot = session.Restart();
        StatusNotification = string.Empty;
        OwnThought = string.Empty;
        ShowSourceNote = false;
        SelectedStartingPoint = StartingPoints.FirstOrDefault();
        Refresh();
        FocusRequest?.Invoke(
            this,
            new ArendtFocusRequestEventArgs(ArendtFocusTarget.Scene));
    }

    private void Refresh() => OnPropertyChanged(string.Empty);

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed record ArendtStartingPointViewModel(string Id, string Text)
{
    public string AutomationName => $"{Text}。按回车直接沿这个方向继续。";
}

public enum ArendtFocusTarget
{
    Scene,
    Completion,
    SourceNote,
    SourceButton,
}

public sealed record ArendtFocusRequestEventArgs(ArendtFocusTarget Target);


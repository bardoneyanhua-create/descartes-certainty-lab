using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace Descartes.CertaintyLab;

public partial class LearningCheckWindow : Window
{
    private readonly LearningSession session;
    private readonly LearningProgressStore progressStore;

    public LearningCheckWindow(
        LearningSession session,
        LearningProgressStore progressStore,
        string? routeDisplayTitle = null)
    {
        InitializeComponent();
        string windowTitle = CreateTitle(routeDisplayTitle);
        Title = windowTitle;
        AutomationProperties.SetName(this, windowTitle);
        this.session =
            session ?? throw new ArgumentNullException(nameof(session));
        this.progressStore =
            progressStore ??
            throw new ArgumentNullException(nameof(progressStore));
        if (session.Stage == LearningStage.Lesson)
        {
            session.BeginChecks();
        }
        else if (session.Stage != LearningStage.Checking)
        {
            throw new InvalidOperationException(
                "理解检查窗口只能从章节正文打开。");
        }

        Loaded += (_, _) => ShowQuestion();
    }

    public static string CreateTitle(string? routeDisplayTitle) =>
        string.IsNullOrWhiteSpace(routeDisplayTitle)
            ? "课程理解检查"
            : $"{routeDisplayTitle.Trim()}课程理解检查";

    public string? LastProgressDiagnostic { get; private set; }

    private void ShowQuestion()
    {
        KnowledgeCheckDefinition check =
            session.CurrentCheck ??
            throw new InvalidOperationException("当前没有理解检查。");
        QuestionText.Text = check.Prompt;
        AutomationProperties.SetName(
            QuestionHeading,
            $"题目。{check.Prompt}");
        OptionsPanel.Children.Clear();
        foreach (CheckOptionDefinition option in check.Options)
        {
            var button = new Button
            {
                Tag = option,
                Content = new SilentTextBlock
                {
                    Text = option.Text,
                    TextWrapping = TextWrapping.Wrap,
                },
            };
            AutomationProperties.SetName(button, option.Text);
            button.Click += OnOptionSelected;
            OptionsPanel.Children.Add(button);
        }

        QuestionHeading.Visibility = Visibility.Visible;
        OptionsPanel.Visibility = Visibility.Visible;
        FeedbackHeading.Visibility = Visibility.Collapsed;
        ContinueButton.Visibility = Visibility.Collapsed;
        CheckStatus.Text =
            "用 Tab 浏览选项，按 Enter 选择；按 Escape 返回本章正文。";
        Dispatcher.BeginInvoke(
            () => QuestionHeading.Focus(),
            DispatcherPriority.ContextIdle);
    }

    private void OnOptionSelected(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button { Tag: CheckOptionDefinition option })
        {
            return;
        }

        try
        {
            session.Answer(option.Id);
            FeedbackText.Text =
                $"{(session.LastAnswerPassed ? "这一项通过。" : "这一项需要再想一想。")} {option.Feedback}";
            AutomationProperties.SetName(
                FeedbackHeading,
                $"反馈。{FeedbackText.Text}");
            OptionsPanel.Visibility = Visibility.Collapsed;
            FeedbackHeading.Visibility = Visibility.Visible;
            ContinueButton.Visibility = Visibility.Visible;
            SaveProgressWithoutClosing();
            Dispatcher.BeginInvoke(
                () => FeedbackHeading.Focus(),
                DispatcherPriority.ContextIdle);
        }
        catch (Exception exception)
        {
            CheckStatus.Text =
                $"这一选项暂时无法处理：{exception.Message}";
        }
    }

    private void OnContinue(object sender, RoutedEventArgs e)
    {
        ContinueAfterFeedback();
    }

    private void ContinueAfterFeedback()
    {
        if (session.Stage != LearningStage.Feedback)
        {
            return;
        }

        session.Continue();
        SaveProgressWithoutClosing();
        if (session.Stage == LearningStage.Checking)
        {
            ShowQuestion();
            return;
        }

        DialogResult = true;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter &&
            session.Stage == LearningStage.Feedback &&
            FeedbackHeading.IsKeyboardFocusWithin)
        {
            e.Handled = true;
            ContinueAfterFeedback();
            return;
        }

        if (e.Key != Key.Escape)
        {
            return;
        }

        e.Handled = true;
        if (session.Stage is
            LearningStage.Checking or
            LearningStage.Feedback)
        {
            session.CancelChecks();
        }
        SaveProgressWithoutClosing();
        DialogResult = false;
    }

    private void SaveProgressWithoutClosing()
    {
        try
        {
            progressStore.Save(session.Progress);
            LastProgressDiagnostic = null;
        }
        catch (Exception exception)
        {
            LastProgressDiagnostic =
                $"progress-save={exception.GetType().FullName}; " +
                $"message={exception.Message}";
            CheckStatus.Text =
                "答案仍然保留，但学习进度暂时无法保存。";
        }
    }
}

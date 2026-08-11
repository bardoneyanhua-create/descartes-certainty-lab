using System.IO;
using System.ComponentModel;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Descartes.CertaintyLab.ThoughtCompanion;
using Microsoft.Web.WebView2.Core;

namespace Descartes.CertaintyLab;

public sealed record LessonRouteItem(
    LessonDefinition Lesson,
    MasteryState State)
{
    public string DisplayTitle => $"{Lesson.Order}。{Lesson.Title}";

    public string StateText => State switch
    {
        MasteryState.Read => "状态：已读",
        MasteryState.Verified => "状态：已验证理解",
        MasteryState.Review => "状态：待复习",
        _ => "状态：未开始",
    };

    public string AutomationName =>
        $"{DisplayTitle}。{StateText}。按回车阅读。";
}

public partial class LearningRouteWindow : Window
{
    private readonly LearningPack pack;
    private readonly LearningRouteDefinition route;
    private readonly LearningProgressStore progressStore;
    private readonly string routeReaderName;
    private LearningSession session;
    private LessonRouteItem? returnItem;
    private bool generatedDocumentNavigationPending;
    private bool readingLesson;
    private string? lastNavigationUri;

    public LearningRouteWindow(
        string routeId,
        string? progressDirectory = null)
        : this(
            routeId,
            progressDirectory,
            initialLessonId: null,
            CompanionApplicationRuntime.Current.CreateWindowContext())
    {
    }

    public LearningRouteWindow(
        string routeId,
        string? progressDirectory,
        string initialLessonId)
        : this(
            routeId,
            progressDirectory,
            initialLessonId,
            CompanionApplicationRuntime.Current.CreateWindowContext())
    {
    }

    public LearningRouteWindow(
        string routeId,
        string? progressDirectory,
        ICompanionService companionService)
        : this(
            routeId,
            progressDirectory,
            initialLessonId: null,
            CreateOfflineContext(companionService))
    {
    }

    public LearningRouteWindow(
        string routeId,
        string? progressDirectory,
        string? initialLessonId,
        ICompanionService companionService)
        : this(routeId, progressDirectory, initialLessonId, CreateOfflineContext(companionService))
    {
    }

    private LearningRouteWindow(
        string routeId,
        string? progressDirectory,
        string? initialLessonId,
        CompanionWindowContext companionContext)
    {
        ArgumentNullException.ThrowIfNull(companionContext);
        Companion = new CompanionViewModel(
            companionContext.Service,
            companionContext.Presentation,
            companionContext.ConsentSession,
            new WindowConsentPrompt(() => this));
        InitializeComponent();
        DataContext = this;
        Companion.PropertyChanged += OnCompanionPropertyChanged;
        Companion.ResponseCommitted += OnCompanionResponseCommitted;
        string contentDirectory = Path.Combine(
            AppContext.BaseDirectory,
            "Content");
        LearningRouteCatalogItem routeRegistration =
            LearningRouteRegistry.Load(contentDirectory).Resolve(routeId);
        routeReaderName = routeRegistration.Title;
        string description = routeRegistration.Summary;
        string packPath = Path.Combine(
            contentDirectory,
            routeRegistration.FileName);
        using (FileStream stream = File.OpenRead(packPath))
        {
            pack = LearningPack.Load(stream);
        }

        route = pack.GetRoute(routeId);
        Title = route.Title;
        RouteTitle.Text = route.Title;
        RouteDescription.Text = description;
        System.Windows.Automation.AutomationProperties.SetName(
            LessonsList,
            $"{routeReaderName}。用上下方向键选择章节，按回车阅读。");
        System.Windows.Automation.AutomationProperties.SetName(
            LessonReader,
            $"{routeReaderName}语义网页正文");
        IReadOnlyDictionary<string, string> abilitySignatures =
            pack.Nodes.ToDictionary(
                node => node.Id,
                node => LearningProgressStore.CreateAbilitySignature(
                    node.AbilityIds),
                StringComparer.Ordinal);
        progressStore = progressDirectory is null
            ? new LearningProgressStore(
                Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.LocalApplicationData),
                    "PhilosophyVault",
                    "learning-progress"),
                abilitySignatures)
            : new LearningProgressStore(
                progressDirectory,
                abilitySignatures);
        ProgressLoadResult load =
            progressStore.Load(route.Id, route.Version);
        session = LearningSession.Start(pack, route.Id, load.Progress);
        LastProgressDiagnostic = load.Diagnostic;
        RefreshLessonItems(initialLessonId);
        Loaded += async (_, _) =>
        {
            LessonsList.SelectedIndex = Math.Max(
                0,
                LessonsList.SelectedIndex);
            FocusSelectedLesson();
            if (!string.IsNullOrWhiteSpace(LastProgressDiagnostic))
            {
                ReaderStatus.Text =
                    "学习进度未能读取，已从空进度开始。";
            }

            if (!string.IsNullOrWhiteSpace(initialLessonId) &&
                LessonsList.SelectedItem is LessonRouteItem item &&
                string.Equals(
                    item.Lesson.Id,
                    initialLessonId,
                    StringComparison.Ordinal))
            {
                returnItem = item;
                await LoadLessonAsync(item.Lesson);
            }
        };
        Unloaded += OnWindowUnloaded;
    }

    public CompanionViewModel Companion { get; }

    public string? LastReaderDiagnostic { get; private set; }

    public static string CreateCheckButtonAutomationName(
        LessonDefinition lesson)
    {
        ArgumentNullException.ThrowIfNull(lesson);
        string count = lesson.CheckIds.Count switch
        {
            4 => "四",
            5 => "五",
            _ => lesson.CheckIds.Count.ToString(),
        };
        return $"开始本章{count}道理解检查";
    }

    public string? LastProgressDiagnostic { get; private set; }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && readingLesson)
        {
            RestoreLessonFocus();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private async void OnLessonsListKeyDown(
        object sender,
        KeyEventArgs e)
    {
        if (e.Key != Key.Enter ||
            LessonsList.SelectedItem is not LessonRouteItem item)
        {
            return;
        }

        e.Handled = true;
        returnItem = item;
        await LoadLessonAsync(item.Lesson);
    }

    private async Task LoadLessonAsync(LessonDefinition lesson)
    {
        ReaderStatus.Text = $"正在加载{routeReaderName}语义正文。";
        try
        {
            if (session.Stage != LearningStage.Route)
            {
                session.ReturnToRoute();
            }
            session.OpenLesson(lesson.Id);
            Companion.SetLesson(pack, lesson);
            string checkName =
                CreateCheckButtonAutomationName(lesson);
            BeginChecksButton.Content =
                $"{checkName}（按回车）";
            System.Windows.Automation.AutomationProperties.SetName(
                BeginChecksButton,
                checkName);
            SaveProgressWithoutEscaping();
            RefreshLessonItems(lesson.Id);
            await LessonReader.EnsureCoreWebView2Async();
            LessonReader.CoreWebView2.Settings.AreDefaultContextMenusEnabled =
                false;
            LessonReader.CoreWebView2.Settings.AreDevToolsEnabled = false;
            LessonReader.CoreWebView2.NavigationStarting -= OnNavigationStarting;
            LessonReader.CoreWebView2.NavigationStarting += OnNavigationStarting;
            LessonReader.NavigationCompleted -= OnNavigationCompleted;
            LessonReader.NavigationCompleted += OnNavigationCompleted;
            generatedDocumentNavigationPending = true;
            LessonReader.NavigateToString(
                LearningDocument.CreateLesson(lesson, pack));
        }
        catch (Exception exception)
        {
            LastReaderDiagnostic =
                $"load-exception={exception.GetType().FullName}; " +
                $"hresult=0x{exception.HResult:X8}; message={exception.Message}";
            ReaderStatus.Text =
                "课程正文无法加载，焦点已返回原章节。";
            RestoreLessonFocus();
        }
    }

    private void OnCompanionActionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox picker ||
            picker.SelectedItem is not ComboBoxItem item ||
            item.Tag is not string tag ||
            !Enum.TryParse(tag, ignoreCase: false, out CompanionAction action))
        {
            return;
        }

        Companion.SelectedAction = action;
    }

    private async void OnCompanionSendOrCancel(
        object sender,
        RoutedEventArgs e)
    {
        if (Companion.IsBusy)
        {
            Companion.Cancel();
            return;
        }

        await Companion.SendAsync();
    }

    private void OnCompanionPropertyChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(CompanionViewModel.AccessibleStatus))
        {
            return;
        }

        if (Dispatcher.CheckAccess())
        {
            CompanionStatus.RaiseLiveRegionChanged();
        }
        else
        {
            Dispatcher.BeginInvoke(
                CompanionStatus.RaiseLiveRegionChanged,
                DispatcherPriority.ContextIdle);
        }
    }

    private void OnCompanionResponseCommitted(
        object? sender,
        EventArgs e)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(
                FocusCompanionAnswer,
                DispatcherPriority.ContextIdle);
            return;
        }

        Dispatcher.BeginInvoke(
            FocusCompanionAnswer,
            DispatcherPriority.ContextIdle);
    }

    private void FocusCompanionAnswer()
    {
        if (!IsLoaded ||
            !Companion.HasResponse ||
            !CompanionAnswer.IsVisible)
        {
            return;
        }

        CompanionAnswer.Focus();
        Keyboard.Focus(CompanionAnswer);
        AutomationPeer? peer =
            UIElementAutomationPeer.FromElement(CompanionAnswer) ??
            UIElementAutomationPeer.CreatePeerForElement(CompanionAnswer);
        peer?.RaiseAutomationEvent(
            AutomationEvents.AutomationFocusChanged);
    }

    private void OnWindowUnloaded(
        object sender,
        RoutedEventArgs e)
    {
        Companion.PropertyChanged -= OnCompanionPropertyChanged;
        Companion.ResponseCommitted -= OnCompanionResponseCommitted;
        Companion.Cancel();
        Unloaded -= OnWindowUnloaded;
    }

    private void OnNavigationStarting(
        object? sender,
        CoreWebView2NavigationStartingEventArgs e)
    {
        lastNavigationUri = e.Uri;
        if (generatedDocumentNavigationPending &&
            e.Uri.StartsWith(
                "data:text/html",
                StringComparison.OrdinalIgnoreCase))
        {
            generatedDocumentNavigationPending = false;
            return;
        }

        if (string.Equals(
                e.Uri,
                "about:blank",
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        e.Cancel = true;
        ReaderStatus.Text = "已阻止课程正文离开离线页面。";
    }

    private async void OnNavigationCompleted(
        object? sender,
        CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!e.IsSuccess)
        {
            generatedDocumentNavigationPending = false;
            LastReaderDiagnostic =
                $"navigation-failed={e.WebErrorStatus}; " +
                $"uri={DescribeUri(lastNavigationUri)}";
            ReaderStatus.Text =
                "课程正文导航失败，焦点已返回原章节。";
            RestoreLessonFocus();
            return;
        }

        try
        {
            LessonReader.Focus();
            await LessonReader.ExecuteScriptAsync(
                "document.getElementById('lesson-title').focus()");
            readingLesson = true;
            BeginChecksButton.IsEnabled = true;
            ReaderStatus.Text =
                $"正文已加载。{routeReaderName}尚待保益悦听验收。";
        }
        catch (Exception exception)
        {
            LastReaderDiagnostic =
                $"focus-exception={exception.GetType().FullName}; " +
                $"hresult=0x{exception.HResult:X8}; message={exception.Message}";
            ReaderStatus.Text =
                "课程标题无法获得焦点，已返回原章节。";
            RestoreLessonFocus();
        }
    }

    private void OnLessonReaderPreviewKeyDown(
        object sender,
        KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        e.Handled = true;
        RestoreLessonFocus();
    }

    private void RestoreLessonFocus()
    {
        readingLesson = false;
        BeginChecksButton.IsEnabled = false;
        if (session.Stage != LearningStage.Route)
        {
            session.ReturnToRoute();
        }

        RefreshLessonItems(returnItem?.Lesson.Id);
        Dispatcher.BeginInvoke(
            FocusSelectedLesson,
            DispatcherPriority.ContextIdle);
        ReaderStatus.Text = $"已返回{routeReaderName}原章节。";
    }

    private void FocusSelectedLesson()
    {
        LessonsList.Focus();
        if (LessonsList.SelectedItem is not LessonRouteItem item)
        {
            return;
        }

        LessonsList.ScrollIntoView(item);
        if (LessonsList.ItemContainerGenerator.ContainerFromItem(item)
            is ListBoxItem container)
        {
            container.Focus();
        }
    }

    private void RefreshLessonItems(string? selectedLessonId = null)
    {
        string? selection = selectedLessonId ??
            (LessonsList.SelectedItem as LessonRouteItem)?.Lesson.Id;
        LessonRouteItem[] items = route.LessonIds
            .Select(pack.GetLesson)
            .Select(lesson => new LessonRouteItem(
                lesson,
                lesson.NodeIds
                    .Select(session.Progress.For)
                    .DefaultIfEmpty(MasteryState.NotStarted)
                    .Max()))
            .ToArray();
        LessonsList.ItemsSource = items;
        LessonsList.SelectedItem = items.FirstOrDefault(item =>
            string.Equals(
                item.Lesson.Id,
                selection,
                StringComparison.Ordinal)) ?? items.FirstOrDefault();
        int verified = items.Count(item =>
            item.State == MasteryState.Verified);
        int review = items.Count(item =>
            item.State == MasteryState.Review);
        ProgressSummary.Text =
            $"共 {items.Length} 章。已验证 {verified} 章，待复习 {review} 章。所有章节都可直接打开。";
    }

    private void OnBeginChecks(object sender, RoutedEventArgs e)
    {
        if (session.Stage != LearningStage.Lesson)
        {
            return;
        }

        var checkWindow = new LearningCheckWindow(
            session,
            progressStore,
            route.Title)
        {
            Owner = this,
        };
        checkWindow.ShowDialog();
        LastProgressDiagnostic = checkWindow.LastProgressDiagnostic;
        SaveProgressWithoutEscaping();
        RefreshLessonItems(session.CurrentLesson?.Id);
        LessonReader.Focus();
    }

    private void SaveProgressWithoutEscaping()
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
            ReaderStatus.Text =
                "本次学习仍可继续，但进度暂时无法保存。";
        }
    }

    private static string DescribeUri(string? uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
        {
            return "(unknown)";
        }

        int separator = uri.IndexOf(':');
        return separator > 0 ? uri[..separator] : uri;
    }

    private static CompanionWindowContext CreateOfflineContext(ICompanionService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        var profile = ThoughtCompanion.Settings.CompanionSettings.Default.Profiles.Single(
            item => item.Kind == ThoughtCompanion.Settings.CompanionProviderKind.OfflineDemo);
        return new(
            service,
            CompanionPresentation.ForProfile(profile),
            new CompanionConsentSession(preapproved: true));
    }

    private sealed class WindowConsentPrompt(Func<Window?> owner) : ICompanionConsentPrompt
    {
        public bool Confirm(CompanionConsentRequest request) => MessageBox.Show(
            owner(),
            request.DisclosureText + "\n\n选择“否”不会发送任何内容，你的草稿会保留。",
            "首次远程发送确认",
            MessageBoxButton.YesNo,
            MessageBoxImage.Information,
            MessageBoxResult.No) == MessageBoxResult.Yes;
    }
}

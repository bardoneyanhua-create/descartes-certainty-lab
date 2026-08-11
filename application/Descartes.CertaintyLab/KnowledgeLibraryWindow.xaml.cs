using System.IO;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;

namespace Descartes.CertaintyLab;

public partial class KnowledgeLibraryWindow : Window
{
    private readonly KnowledgeLibraryViewModel viewModel;
    private KnowledgeResultViewModel? returnResult;
    private bool generatedDocumentNavigationPending;
    private bool readingArticle;
    private string? lastNavigationUri;

    public string? LastReaderDiagnostic { get; private set; }

    public KnowledgeLibraryWindow(string? initialEntryId = null)
    {
        InitializeComponent();
        string catalogPath = Path.Combine(
            AppContext.BaseDirectory,
            "Content",
            "knowledge-reader-catalog.json");
        KnowledgeCatalog catalog;
        try
        {
            catalog = KnowledgeCatalog.Load(catalogPath);
        }
        catch (InvalidDataException)
        {
            catalog = KnowledgeCatalog.CreateUnavailable();
        }
        viewModel = new KnowledgeLibraryViewModel(catalog);
        DataContext = viewModel;
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName != nameof(viewModel.ResultSummary))
            {
                return;
            }

            Dispatcher.BeginInvoke(
                RaiseResultSummaryChanged,
                DispatcherPriority.Background);
        };
        bool opensSpecificEntry = !string.IsNullOrWhiteSpace(initialEntryId);
        if (opensSpecificEntry && catalog.AllEntries.Count > 0)
        {
            viewModel.SelectById(initialEntryId!);
        }

        Loaded += async (_, _) =>
        {
            if (opensSpecificEntry)
            {
                returnResult = viewModel.SelectedResult;
                await LoadSelectedArticleAsync();
            }
            else
            {
                SearchBox.Focus();
            }
        };
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F &&
            Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && readingArticle)
        {
            RestoreResultFocus();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private void OnSearchBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        viewModel.SearchCommand.Execute(null);
        FocusSearchResultSummary();
        e.Handled = true;
    }

    private void OnSearchExecuted(object sender, RoutedEventArgs e)
    {
        FocusSearchResultSummary();
    }

    private async void OnResultsListKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || viewModel.SelectedResult is null)
        {
            return;
        }

        e.Handled = true;
        returnResult = viewModel.SelectedResult;
        await LoadSelectedArticleAsync();
    }

    private async Task LoadSelectedArticleAsync()
    {
        ReaderStatus.Text = "正在加载语义网页正文。";
        try
        {
            await ArticleReader.EnsureCoreWebView2Async();
            ArticleReader.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            ArticleReader.CoreWebView2.Settings.AreDevToolsEnabled = false;
            ArticleReader.CoreWebView2.NavigationStarting -= OnNavigationStarting;
            ArticleReader.CoreWebView2.NavigationStarting += OnNavigationStarting;
            ArticleReader.NavigationCompleted -= OnArticleNavigationCompleted;
            ArticleReader.NavigationCompleted += OnArticleNavigationCompleted;
            generatedDocumentNavigationPending = true;
            ArticleReader.NavigateToString(
                WebReaderDocument.Create(viewModel.Detail));
        }
        catch (Exception ex)
        {
            LastReaderDiagnostic =
                $"load-exception={ex.GetType().FullName}; hresult=0x{ex.HResult:X8}; " +
                $"message={ex.Message}";
            ReaderStatus.Text =
                "正文无法加载，焦点已返回原知识条目。";
            RestoreResultFocus();
        }
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

        if (string.Equals(e.Uri, "about:blank", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        e.Cancel = true;
        ReaderStatus.Text = "已阻止正文离开离线页面。";
    }

    private async void OnArticleNavigationCompleted(
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
                "正文导航失败，焦点已返回原知识条目。";
            RestoreResultFocus();
            return;
        }

        try
        {
            ArticleReader.Focus();
            await ArticleReader.ExecuteScriptAsync(
                "document.getElementById('article-title').focus()");
            readingArticle = true;
            ReaderStatus.Text =
                "正文已加载。正式窗口尚待保益悦听验收。";
        }
        catch (Exception ex)
        {
            LastReaderDiagnostic =
                $"focus-exception={ex.GetType().FullName}; hresult=0x{ex.HResult:X8}; " +
                $"message={ex.Message}";
            ReaderStatus.Text =
                "正文标题无法获得焦点，已返回原知识条目。";
            RestoreResultFocus();
        }
    }

    private void OnArticleReaderPreviewKeyDown(
        object sender,
        KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        e.Handled = true;
        RestoreResultFocus();
    }

    private void RestoreResultFocus()
    {
        readingArticle = false;
        if (returnResult is not null)
        {
            viewModel.SelectedResult = returnResult;
            ResultsList.ScrollIntoView(returnResult);
        }

        Dispatcher.BeginInvoke(
            FocusSelectedResult,
            DispatcherPriority.ContextIdle);
        ReaderStatus.Text = "已返回原知识条目。";
    }

    private void FocusSelectedResult()
    {
        ResultsList.Focus();
        if (viewModel.SelectedResult is null)
        {
            return;
        }

        ResultsList.ScrollIntoView(viewModel.SelectedResult);
        if (ResultsList.ItemContainerGenerator.ContainerFromItem(
                viewModel.SelectedResult) is System.Windows.Controls.ListBoxItem item)
        {
            item.Focus();
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

    private void FocusSearchResultSummary()
    {
        ResultSummaryText.Focus();
    }

    private void RaiseResultSummaryChanged()
    {
        AutomationPeer peer =
            FrameworkElementAutomationPeer.FromElement(ResultSummaryText) ??
            FrameworkElementAutomationPeer.CreatePeerForElement(ResultSummaryText);
        peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }

    private void OpenSelectedExperience(object sender, RoutedEventArgs e)
    {
        string? experienceId = viewModel.Detail.ExperienceId;
        Window? experience = experienceId switch
        {
            "descartes-waking-dream" => new MainWindow(),
            "arendt-right-to-rights" => new ArendtWindow(),
            _ => null
        };
        if (experience is null)
        {
            return;
        }

        experience.Owner = this;
        experience.ShowDialog();
        ExperienceButton.Focus();
    }

    private void OpenSystemLearningRoute(object sender, RoutedEventArgs e)
    {
        string? routeId = viewModel.Detail.LearningRouteId;
        if (string.IsNullOrWhiteSpace(routeId))
        {
            return;
        }

        var route = new LearningRouteWindow(routeId) { Owner = this };
        route.ShowDialog();
        SystemLearningButton.Focus();
    }
}

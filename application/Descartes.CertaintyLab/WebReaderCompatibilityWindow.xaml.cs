using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;

namespace Descartes.CertaintyLab;

public partial class WebReaderCompatibilityWindow : Window
{
    private readonly KnowledgeLibraryViewModel viewModel;
    private KnowledgeResultViewModel? returnResult;
    private bool generatedDocumentNavigationPending;
    private string? lastNavigationUri;

    public string? LastReaderDiagnostic { get; private set; }

    public WebReaderCompatibilityWindow()
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
        Loaded += (_, _) => FocusSelectedResult();
    }

    private async void OnResultsListKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || viewModel.SelectedResult is null)
        {
            return;
        }

        e.Handled = true;
        returnResult = viewModel.SelectedResult;
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
                "WebView2 正文无法加载。兼容性测试失败，焦点仍在原结果。";
            FocusSelectedResult();
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
        ReaderStatus.Text = "已阻止实验正文离开离线页面。";
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
                "WebView2 正文导航失败。兼容性测试未通过。";
            FocusSelectedResult();
            return;
        }

        try
        {
            ArticleReader.Focus();
            await ArticleReader.ExecuteScriptAsync(
                "document.getElementById('article-title').focus()");
            ReaderStatus.Text =
                "正文已加载。兼容性尚未通过，请用保益悦听测试方向键阅读。";
        }
        catch (Exception ex)
        {
            LastReaderDiagnostic =
                $"focus-exception={ex.GetType().FullName}; hresult=0x{ex.HResult:X8}; " +
                $"message={ex.Message}";
            ReaderStatus.Text =
                "正文标题无法获得焦点。兼容性测试未通过。";
            FocusSelectedResult();
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
                viewModel.SelectedResult) is ListBoxItem item)
        {
            item.Focus();
        }
    }
}

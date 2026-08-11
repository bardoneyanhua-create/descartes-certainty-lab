using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Descartes.CertaintyLab.ThoughtCompanion.Settings;

namespace Descartes.CertaintyLab;

public partial class ExperienceCatalogWindow : Window
{
    private readonly string contentDirectory;
    private readonly string progressDirectory;

    public ExperienceCatalogWindow()
    {
        InitializeComponent();
        contentDirectory = Path.Combine(AppContext.BaseDirectory, "Content");
        progressDirectory = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "PhilosophyVault",
            "learning-progress");
        LearningRoutes = LearningRouteRegistry.Load(contentDirectory).Routes;
        DataContext = this;
        Loaded += (_, _) =>
        {
            RefreshContinueLearning();
            RecentLearningTab.Focus();
        };
    }

    public IReadOnlyList<LearningRouteCatalogItem> LearningRoutes { get; }

    private void OnExperienceActionsListKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            ExperienceTab.Focus();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter && ExperienceActionsList.SelectedIndex >= 0)
        {
            OpenExperiences();
            e.Handled = true;
        }
    }

    private void OnExperienceActionsListDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ExperienceActionsList.SelectedIndex >= 0)
        {
            OpenExperiences();
        }
    }

    private void OpenExperiences()
    {
        var window = new ExperienceLibraryWindow { Owner = this };
        window.ShowDialog();
        ExperienceActionsList.Focus();
    }

    private void OnKnowledgeActionsListKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            KnowledgeTab.Focus();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter && KnowledgeActionsList.SelectedIndex >= 0)
        {
            OpenKnowledgeLibrary();
            e.Handled = true;
        }
    }

    private void OnKnowledgeActionsListDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (KnowledgeActionsList.SelectedIndex >= 0)
        {
            OpenKnowledgeLibrary();
        }
    }

    private void OpenKnowledgeLibrary()
    {
        var window = new KnowledgeLibraryWindow { Owner = this };
        window.ShowDialog();
        KnowledgeActionsList.Focus();
    }

    private void OnOpenAiSettings(object sender, RoutedEventArgs e)
    {
        try
        {
            var window = new AiSettingsWindow { Owner = this };
            window.ShowDialog();
        }
        catch (Exception exception) when (AiSettingsErrorMessages.IsCredentialBoundaryException(exception))
        {
            MessageBox.Show(
                this,
                "无法打开 AI 设置：" + AiSettingsErrorMessages.For(exception),
                "AI 设置",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            OpenAiSettingsButton.Focus();
        }
    }

    private void OnLearningRoutesListKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            SystemLearningTab.Focus();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter &&
            LearningRoutesList.SelectedItem is LearningRouteCatalogItem item)
        {
            OpenLearningRoute(item);
            e.Handled = true;
        }
    }

    private void OnLearningRoutesListDoubleClick(
        object sender,
        MouseButtonEventArgs e)
    {
        if (LearningRoutesList.SelectedItem is LearningRouteCatalogItem item)
        {
            OpenLearningRoute(item);
        }
    }

    private void OnContinueLearningListKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            RecentLearningTab.Focus();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter &&
            ContinueLearningList.SelectedItem is LearningProgressRouteItem item)
        {
            OpenContinueLearning(item);
            e.Handled = true;
        }
    }

    private void OnContinueLearningListDoubleClick(
        object sender,
        MouseButtonEventArgs e)
    {
        if (ContinueLearningList.SelectedItem is LearningProgressRouteItem item)
        {
            OpenContinueLearning(item);
        }
    }

    private void OpenLearningRoute(LearningRouteCatalogItem item)
    {
        var window = new LearningRouteWindow(item.RouteId) { Owner = this };
        window.ShowDialog();
        RefreshContinueLearning();
        LearningRoutesList.Focus();
    }

    private void OpenContinueLearning(LearningProgressRouteItem item)
    {

        var window = new LearningRouteWindow(
            item.RouteId,
            progressDirectory,
            item.LessonId)
        {
            Owner = this,
        };
        window.ShowDialog();
        RefreshContinueLearning();
        ContinueLearningList.Focus();
    }

    private void RefreshContinueLearning()
    {
        LearningProgressOverview overview = LearningProgressOverview.Load(
            contentDirectory,
            progressDirectory);
        ContinueLearningList.ItemsSource = overview.Items;
        ContinueLearningStatus.Text = overview.Items.Count == 0
            ? string.IsNullOrWhiteSpace(overview.Diagnostic)
                ? "还没有进行中的路线。打开任意章节后，这里会出现继续入口。"
                : "部分本地进度无法读取；没有可安全继续的路线。"
            : string.IsNullOrWhiteSpace(overview.Diagnostic)
                ? $"找到 {overview.Items.Count} 条最近或进行中的路线。"
                : $"找到 {overview.Items.Count} 条可继续路线；另有部分本地进度已安全忽略。";
    }
}

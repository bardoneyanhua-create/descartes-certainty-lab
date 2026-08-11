using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Input;
using System.Windows.Threading;

namespace Descartes.CertaintyLab;

public partial class ArendtWindow : Window
{
    private readonly ArendtViewModel viewModel;

    public ArendtWindow()
    {
        InitializeComponent();
        string packPath = Path.Combine(
            AppContext.BaseDirectory,
            "Content",
            "arendt-right-to-rights-pack.json");
        using FileStream stream = File.OpenRead(packPath);
        viewModel = new ArendtViewModel(ArendtExperiencePack.Load(stream));
        viewModel.FocusRequest += OnFocusRequest;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        DataContext = viewModel;
        Loaded += (_, _) => FocusElement(SceneRegion);
    }

    private void OnCloseExperience(object sender, RoutedEventArgs e) => Close();

    private void OnOpenKnowledge(object sender, RoutedEventArgs e)
    {
        var window = new KnowledgeLibraryWindow("entry-023") { Owner = this };
        window.ShowDialog();
        KnowledgeButton.Focus();
    }

    private void OnViewModelPropertyChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ArendtViewModel.StatusNotification) ||
            string.IsNullOrWhiteSpace(viewModel.StatusNotification))
        {
            return;
        }

        Dispatcher.BeginInvoke(
            DispatcherPriority.Background,
            new Action(() =>
            {
                AutomationPeer? peer =
                    UIElementAutomationPeer.FromElement(SceneRegion) ??
                    UIElementAutomationPeer.CreatePeerForElement(SceneRegion);
                peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
            }));
    }

    private void OnFocusRequest(
        object? sender,
        ArendtFocusRequestEventArgs e)
    {
        Dispatcher.BeginInvoke(
            DispatcherPriority.ContextIdle,
            new Action(() =>
            {
                IInputElement target = e.Target switch
                {
                    ArendtFocusTarget.Completion => CompletionRegion,
                    ArendtFocusTarget.SourceNote => SourceNoteRegion,
                    ArendtFocusTarget.SourceButton => SourceButton,
                    _ => SceneRegion,
                };
                FocusElement(target);
            }));
    }

    private static void FocusElement(IInputElement element)
    {
        Keyboard.Focus(element);
        if (element is FrameworkElement frameworkElement)
        {
            frameworkElement.BringIntoView();
        }
    }
}

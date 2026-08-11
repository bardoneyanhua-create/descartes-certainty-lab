using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace Descartes.CertaintyLab;

public partial class MainWindow : Window
{
    private readonly MainViewModel viewModel;

    public MainWindow()
    {
        InitializeComponent();

        string packPath = Path.Combine(
            AppContext.BaseDirectory,
            "Content",
            "certainty-collapse-pack.json");
        using FileStream stream = File.OpenRead(packPath);
        viewModel = new MainViewModel(ExperiencePack.Load(stream));
        viewModel.FocusRequest += OnFocusRequest;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        DataContext = viewModel;

        Loaded += (_, _) => FocusElement(SceneSummaryRegion);
    }

    private void OnReasonChoicesPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        viewModel.ChooseSelectedReasonCommand.Execute(null);
        e.Handled = true;
    }

    private void OnReasonChoicesMouseDoubleClick(
        object sender,
        MouseButtonEventArgs e)
    {
        viewModel.ChooseSelectedReasonCommand.Execute(null);
        e.Handled = true;
    }

    private void OnOwnReasonPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        viewModel.UseOwnReasonCommand.Execute(null);
        e.Handled = true;
    }

    private void OnOpenKnowledge(object sender, RoutedEventArgs e)
    {
        var window = new KnowledgeLibraryWindow("entry-008") { Owner = this };
        window.ShowDialog();
        KnowledgeButton.Focus();
    }

    private void OnViewModelPropertyChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.StatusNotification) ||
            string.IsNullOrWhiteSpace(viewModel.StatusNotification))
        {
            return;
        }

        Dispatcher.BeginInvoke(
            DispatcherPriority.Background,
            new Action(() =>
            {
                AutomationPeer? peer =
                    UIElementAutomationPeer.FromElement(DiscoveryRegion) ??
                    UIElementAutomationPeer.CreatePeerForElement(DiscoveryRegion);
                peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
            }));
    }

    private void OnFocusRequest(object? sender, FocusRequestEventArgs e)
    {
        Dispatcher.BeginInvoke(
            DispatcherPriority.ContextIdle,
            new Action(() =>
            {
                switch (e.Target)
                {
                    case FocusTarget.ReasonChoices:
                        FocusReasonChoices();
                        break;
                    case FocusTarget.CompletionSummary:
                        FocusElement(CompletionSummaryRegion);
                        break;
                    case FocusTarget.SourceNote:
                        FocusElement(SourceNoteRegion);
                        break;
                    case FocusTarget.SourceButton:
                        FocusElement(SourceButton);
                        break;
                    default:
                        FocusElement(SceneSummaryRegion);
                        break;
                }
            }));
    }

    private void FocusReasonChoices()
    {
        ReasonChoicesList.Focus();
        if (ReasonChoicesList.SelectedItem is null)
        {
            return;
        }

        ReasonChoicesList.ScrollIntoView(ReasonChoicesList.SelectedItem);
        if (ReasonChoicesList.ItemContainerGenerator.ContainerFromItem(
                ReasonChoicesList.SelectedItem) is ListBoxItem item)
        {
            item.Focus();
        }
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

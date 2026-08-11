using System.Windows;

namespace Descartes.CertaintyLab;

public partial class ExperienceLibraryWindow : Window
{
    public ExperienceLibraryWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => OpenDescartes.Focus();
    }

    private void OnOpenDescartes(object sender, RoutedEventArgs e)
    {
        var window = new MainWindow { Owner = this };
        window.ShowDialog();
        OpenDescartes.Focus();
    }

    private void OnOpenArendt(object sender, RoutedEventArgs e)
    {
        var window = new ArendtWindow { Owner = this };
        window.ShowDialog();
        OpenArendt.Focus();
    }
}

using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Descartes.CertaintyLab.ThoughtCompanion;
using Descartes.CertaintyLab.ThoughtCompanion.Settings;

namespace Descartes.CertaintyLab;

public partial class AiSettingsWindow : Window
{
    private readonly AiSettingsEditor editor;
    private bool refreshing;

    public AiSettingsWindow()
        : this(CompanionApplicationRuntime.Current)
    {
    }

    internal AiSettingsWindow(CompanionApplicationRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        editor = runtime.CreateSettingsEditor();
        InitializeComponent();
        RefreshFromEditor();
        Loaded += (_, _) => ModePicker.Focus();
    }

    private void OnModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (refreshing || ModePicker.SelectedItem is not ComboBoxItem item ||
            item.Tag is not string tag ||
            !Enum.TryParse(tag, out CompanionProviderKind mode))
        {
            return;
        }

        bool discardedReplacement = ApiKeyBox.SecurePassword.Length > 0;
        ApiKeyBox.Clear();
        try
        {
            editor.SelectMode(mode);
            RefreshFromEditor();
            if (discardedReplacement)
            {
                AnnounceStatus("已清除上一配置中未保存的 API key replacement。" +
                    (editor.ApiKeyState == "状态未知"
                        ? " " + AiSettingsErrorMessages.CredentialStateUnknownWarning
                        : string.Empty));
            }
        }
        catch (Exception exception) when (AiSettingsErrorMessages.IsCredentialBoundaryException(exception))
        {
            RefreshFromEditor();
            AnnounceStatus(AiSettingsErrorMessages.CredentialStateUnknownWarning);
        }
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        CopyFieldsToEditor();
        char[] replacement = ApiKeyBox.Password.ToCharArray();
        try
        {
            editor.Save(replacement);
            ApiKeyBox.Clear();
            RefreshFromEditor();
            AnnounceStatus(editor.Status);
        }
        catch (Exception exception) when (
            exception is ArgumentException or FormatException or InvalidOperationException or IOException or UnauthorizedAccessException or AiSettingsConsistencyException ||
            AiSettingsErrorMessages.IsCredentialBoundaryException(exception))
        {
            AnnounceStatus("设置未保存：" + AiSettingsErrorMessages.For(exception));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(replacement.AsSpan()));
        }
    }

    private async void OnTestConnection(object sender, RoutedEventArgs e)
    {
        CopyFieldsToEditor();
        TestConnectionButton.IsEnabled = false;
        AnnounceStatus("正在测试连接。");
        try
        {
            await editor.TestConnectionAsync(CancellationToken.None);
            AnnounceStatus(editor.Status);
        }
        catch (Exception exception) when (
            exception is ArgumentException or FormatException or InvalidOperationException or AiSettingsConsistencyException ||
            AiSettingsErrorMessages.IsCredentialBoundaryException(exception))
        {
            AnnounceStatus("无法测试连接：" + AiSettingsErrorMessages.For(exception));
        }
        finally
        {
            TestConnectionButton.IsEnabled = true;
        }
    }

    private void OnDeleteProfile(object sender, RoutedEventArgs e)
    {
        bool confirmed = MessageBox.Show(
            this,
            "将删除当前配置及其专属凭据。此操作不会影响其他配置。是否继续？",
            "确认删除 AI 配置",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No) == MessageBoxResult.Yes;
        try
        {
            if (editor.DeleteSelectedProfile(confirmed))
            {
                ApiKeyBox.Clear();
                RefreshFromEditor();
                AnnounceStatus(editor.Status);
            }
            else if (!confirmed)
            {
                AnnounceStatus("未删除配置。");
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or AiSettingsConsistencyException ||
            AiSettingsErrorMessages.IsCredentialBoundaryException(exception))
        {
            AnnounceStatus("无法删除配置：" + AiSettingsErrorMessages.For(exception));
        }
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private void CopyFieldsToEditor()
    {
        editor.DisplayName = DisplayNameBox.Text;
        editor.BaseUrl = BaseUrlBox.Text;
        editor.Model = ModelBox.Text;
    }

    private void RefreshFromEditor()
    {
        refreshing = true;
        try
        {
            ModePicker.SelectedIndex = (int)editor.SelectedMode;
            DisplayNameBox.Text = editor.DisplayName;
            BaseUrlBox.Text = editor.BaseUrl;
            ModelBox.Text = editor.Model;
            ApiKeyStateText.Text = editor.ApiKeyState;
            DisplayNameBox.IsEnabled = editor.IsRemote;
            BaseUrlBox.IsEnabled = editor.IsBaseUrlEditable;
            ModelBox.IsEnabled = editor.IsRemote;
            ApiKeyBox.IsEnabled = editor.IsRemote;
            TestConnectionButton.IsEnabled = editor.IsRemote;
            DeleteProfileButton.IsEnabled = editor.CanDelete;
            StatusText.Text = editor.Status;
        }
        finally
        {
            refreshing = false;
        }
    }

    private void AnnounceStatus(string message)
    {
        StatusText.Text = message;
        SettingsStatus.RaiseLiveRegionChanged();
    }
}

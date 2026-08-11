using System.ComponentModel;
using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using Descartes.CertaintyLab.ThoughtCompanion.Security;

namespace Descartes.CertaintyLab.ThoughtCompanion.Settings;

public sealed class AiSettingsConsistencyException : Exception
{
    public AiSettingsConsistencyException(Exception operationFailure, Exception compensationFailure)
        : base(
            "本地设置操作失败，且凭据恢复未完成。",
            new AggregateException(operationFailure, compensationFailure))
    {
    }
}

public static class AiSettingsErrorMessages
{
    public const string CredentialStateUnknownWarning =
        "Windows 凭据状态暂时无法确认；请检查当前账户权限后重试。";

    public static bool IsCredentialBoundaryException(Exception exception) =>
        exception is Win32Exception or SecurityException or CryptographicException;

    public static string For(Exception exception) => exception switch
    {
        AiSettingsConsistencyException => "本地设置未完成，且凭据状态可能不一致。请关闭设置窗口后重试；如问题持续，请重新保存或删除该配置。",
        Win32Exception or SecurityException or CryptographicException => "Windows 凭据暂时无法访问。请检查当前账户权限后重试。",
        UriFormatException => "请输入有效的绝对 HTTPS 基础网址。",
        ArgumentException => "请检查显示名称、HTTPS 基础网址和模型。",
        UnauthorizedAccessException => "没有权限保存本地设置或凭据。",
        IOException => "本地设置暂时无法写入。",
        _ => "操作未完成。",
    };
}

public sealed class AiSettingsEditor
{
    private readonly ICompanionSettingsStore settingsStore;
    private readonly ICredentialStore credentials;
    private readonly ICompanionServiceFactory factory;
    private CompanionSettings settings;
    private CompanionProfile? selectedProfile;

    public AiSettingsEditor(
        ICompanionSettingsStore settingsStore,
        ICredentialStore credentials,
        ICompanionServiceFactory factory)
    {
        this.settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
        this.credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
        this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
        settings = settingsStore.Load().Settings;
        selectedProfile = settings.Profiles.Single(profile => profile.Id == settings.ActiveProfileId);
        if (LoadFields(selectedProfile))
        {
            Status = AiSettingsErrorMessages.CredentialStateUnknownWarning;
        }
    }

    public IReadOnlyList<string> ModeLabels { get; } =
        ["离线演示", "DeepSeek", "OpenAI", "自定义兼容供应商"];

    public CompanionProviderKind SelectedMode { get; private set; }
    public string DisplayName { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string ApiKeyState { get; private set; } = "未保存";
    public string Status { get; private set; } = "配置变更只对新打开的学习窗口生效。";
    public bool CanDelete => selectedProfile is not null && selectedProfile.Kind != CompanionProviderKind.OfflineDemo;
    public bool IsRemote => SelectedMode != CompanionProviderKind.OfflineDemo;
    public bool IsBaseUrlEditable => SelectedMode == CompanionProviderKind.CustomOpenAiCompatible;

    public void SelectMode(CompanionProviderKind mode)
    {
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        selectedProfile = settings.Profiles.FirstOrDefault(profile => profile.Kind == mode);
        if (selectedProfile is not null)
        {
            Status = LoadFields(selectedProfile)
                ? AiSettingsErrorMessages.CredentialStateUnknownWarning
                : "配置变更只对新打开的学习窗口生效。";
            return;
        }

        SelectedMode = mode;
        DisplayName = mode == CompanionProviderKind.CustomOpenAiCompatible ? "自定义兼容供应商" : mode.ToString();
        BaseUrl = string.Empty;
        Model = string.Empty;
        ApiKeyState = "未保存";
        Status = "配置变更只对新打开的学习窗口生效。";
    }

    public void Save(char[]? credentialReplacement)
    {
        try
        {
            CompanionProfile profile = BuildProfile();
            CompanionProfile[] profiles = selectedProfile is null
                ? settings.Profiles.Append(profile).ToArray()
                : settings.Profiles.Select(item => item.Id == selectedProfile.Id ? profile : item).ToArray();
            var updated = new CompanionSettings(profile.Id, profiles);

            if (profile.CredentialTarget is not null && credentialReplacement is { Length: > 0 })
            {
                ReplaceCredentialAndSettings(profile.CredentialTarget, credentialReplacement, updated);
            }
            else
            {
                settingsStore.Save(updated);
            }

            settings = updated;
            selectedProfile = profile;
            bool credentialStateUnknown = LoadFields(profile);
            Status = credentialStateUnknown
                ? $"设置已保存；{AiSettingsErrorMessages.CredentialStateUnknownWarning} 配置变更只对新打开的学习窗口生效。"
                : "设置已保存；配置变更只对新打开的学习窗口生效。";
        }
        finally
        {
            if (credentialReplacement is not null)
            {
                CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(credentialReplacement.AsSpan()));
            }
        }
    }

    public async Task<CompanionConnectionTestResult> TestConnectionAsync(CancellationToken cancellationToken)
    {
        CompanionProfile profile = BuildProfile();
        if (profile.Kind == CompanionProviderKind.OfflineDemo)
        {
            var local = new CompanionConnectionTestResult(true, CompanionFailureKind.None, "离线演示无需网络连接。", null);
            Status = local.UserMessage;
            return local;
        }

        CompanionConnectionTestResult result = await factory.TestConnectionAsync(profile, cancellationToken);
        Status = result.UserMessage;
        return result;
    }

    public bool DeleteSelectedProfile(bool confirmed)
    {
        if (!confirmed || selectedProfile is null || selectedProfile.Kind == CompanionProviderKind.OfflineDemo)
        {
            return false;
        }

        CompanionProfile removed = selectedProfile;
        CompanionProfile fallback = settings.Profiles.First(profile => profile.Kind == CompanionProviderKind.OfflineDemo);
        bool removedActiveProfile = settings.ActiveProfileId == removed.Id;
        CompanionProfile[] remaining = settings.Profiles.Where(profile => profile.Id != removed.Id).ToArray();
        var updated = new CompanionSettings(
            removedActiveProfile ? fallback.Id : settings.ActiveProfileId,
            remaining);
        using SensitiveBuffer? original = removed.CredentialTarget is null ? null : credentials.Read(removed.CredentialTarget);
        bool deleted = removed.CredentialTarget is not null && credentials.Delete(removed.CredentialTarget);
        try
        {
            settingsStore.Save(updated);
        }
        catch (Exception operationFailure)
        {
            try
            {
                if (deleted && original is not null && removed.CredentialTarget is not null)
                {
                    credentials.Write(removed.CredentialTarget, original);
                }
            }
            catch (Exception compensationFailure)
            {
                throw new AiSettingsConsistencyException(operationFailure, compensationFailure);
            }
            throw;
        }

        settings = updated;
        selectedProfile = settings.Profiles.Single(profile => profile.Id == settings.ActiveProfileId);
        bool credentialStateUnknown = LoadFields(selectedProfile);
        string committedStatus = removedActiveProfile
            ? "配置及其专属凭据已删除；离线演示已激活。"
            : $"配置及其专属凭据已删除；当前活动配置仍为 {selectedProfile.DisplayName}。";
        Status = credentialStateUnknown
            ? $"{committedStatus} {AiSettingsErrorMessages.CredentialStateUnknownWarning}"
            : committedStatus;
        return true;
    }

    private CompanionProfile BuildProfile()
    {
        if (SelectedMode == CompanionProviderKind.OfflineDemo)
        {
            CompanionProfile offline = settings.Profiles.Single(profile => profile.Kind == CompanionProviderKind.OfflineDemo);
            return offline;
        }

        Guid id = selectedProfile?.Id ?? Guid.NewGuid();
        Uri baseUrl = SelectedMode switch
        {
            CompanionProviderKind.DeepSeek => CompanionProfile.DeepSeekBaseUrl,
            CompanionProviderKind.OpenAI => CompanionProfile.OpenAiBaseUrl,
            _ => new Uri(BaseUrl, UriKind.Absolute),
        };
        return new CompanionProfile(
            id,
            SelectedMode,
            DisplayName,
            baseUrl,
            Model,
            CompanionCredentialTargets.ForProfile(id));
    }

    private void ReplaceCredentialAndSettings(string target, char[] replacement, CompanionSettings updated)
    {
        using SensitiveBuffer? original = credentials.Read(target);
        using SensitiveBuffer value = SensitiveBuffer.CopyFrom(replacement);
        credentials.Write(target, value);
        try
        {
            settingsStore.Save(updated);
        }
        catch (Exception operationFailure)
        {
            try
            {
                if (original is null)
                {
                    credentials.Delete(target);
                }
                else
                {
                    credentials.Write(target, original);
                }
            }
            catch (Exception compensationFailure)
            {
                throw new AiSettingsConsistencyException(operationFailure, compensationFailure);
            }
            throw;
        }
    }

    private bool LoadFields(CompanionProfile profile)
    {
        SelectedMode = profile.Kind;
        DisplayName = profile.DisplayName;
        BaseUrl = profile.BaseUrl?.AbsoluteUri ?? string.Empty;
        Model = profile.Model;
        if (profile.CredentialTarget is null)
        {
            ApiKeyState = "未保存";
            return false;
        }

        try
        {
            ApiKeyState = credentials.Exists(profile.CredentialTarget) ? "已保存" : "未保存";
            return false;
        }
        catch (Exception exception) when (AiSettingsErrorMessages.IsCredentialBoundaryException(exception))
        {
            ApiKeyState = "状态未知";
            return true;
        }
    }
}

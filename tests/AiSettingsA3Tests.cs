using System.ComponentModel;
using System.Security;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;
using Descartes.CertaintyLab.ThoughtCompanion;
using Descartes.CertaintyLab.ThoughtCompanion.Security;
using Descartes.CertaintyLab.ThoughtCompanion.Settings;

namespace Descartes.CertaintyLab;

internal static class AiSettingsA3Tests
{
    internal static async Task<IReadOnlyList<string>> RunAsync(string candidateRoot)
    {
        var failures = new List<string>();
        string appRoot = Path.Combine(candidateRoot, "application", "Descartes.CertaintyLab");
        CheckUiStructure(appRoot, failures);
        CheckModeSwitchClearsPendingReplacement(failures);
        await CheckSettingsBehaviorAsync(failures);
        await CheckCompositionAndPresentationAsync(appRoot, failures);
        return failures;
    }

    private static void CheckUiStructure(string appRoot, List<string> failures)
    {
        string homeXaml = File.ReadAllText(Path.Combine(appRoot, "ExperienceCatalogWindow.xaml"));
        string homeCode = File.ReadAllText(Path.Combine(appRoot, "ExperienceCatalogWindow.xaml.cs"));
        string settingsXaml = File.ReadAllText(Path.Combine(appRoot, "AiSettingsWindow.xaml"));
        string settingsCode = File.ReadAllText(Path.Combine(appRoot, "AiSettingsWindow.xaml.cs"));
        string routeXaml = File.ReadAllText(Path.Combine(appRoot, "LearningRouteWindow.xaml"));
        string presentationCode = File.ReadAllText(Path.Combine(
            appRoot, "ThoughtCompanion", "CompanionPresentation.cs"));
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XElement home = XDocument.Parse(homeXaml).Root!;
        XElement settings = XDocument.Parse(settingsXaml).Root!;

        XElement? settingsButton = home.Descendants(presentation + "Button")
            .SingleOrDefault(element => element.Attributes().Any(attribute =>
                attribute.Name.LocalName == "Name" && attribute.Value == "OpenAiSettingsButton"));
        XElement? homeTabs = home.Descendants(presentation + "TabControl").SingleOrDefault();
        Check(settingsButton is not null && homeTabs is not null && settingsButton.Parent != homeTabs,
            "AI 设置入口必须位于四个主页内容标签之外", failures);
        Check(homeCode.Contains("new AiSettingsWindow", StringComparison.Ordinal),
            "AI 设置入口必须打开原生 WPF 设置窗口", failures);

        string[] expectedModes = ["离线演示", "DeepSeek", "OpenAI", "自定义兼容供应商"];
        string settingsText = settings.ToString(SaveOptions.DisableFormatting);
        Check(expectedModes.All(mode => settingsText.Contains(mode, StringComparison.Ordinal)),
            "设置窗口必须公开四种准确模式", failures);
        Check(settings.Descendants(presentation + "PasswordBox").Count() == 1,
            "API key replacement 必须且只能使用 PasswordBox", failures);
        Check(settingsCode.Contains("BaseUrlBox.IsEnabled = editor.IsBaseUrlEditable", StringComparison.Ordinal),
            "DeepSeek/OpenAI preset 基础网址必须在 UI 中锁定，仅自定义模式可编辑", failures);
        Check(settingsText.Contains("AutomationProperties.LiveSetting=\"Polite\"", StringComparison.Ordinal) &&
              settingsText.Contains("TabIndex=", StringComparison.Ordinal) &&
              settingsText.Contains("新打开的学习窗口", StringComparison.Ordinal),
            "设置窗口必须提供 polite 状态、确定 Tab 顺序和新窗口生效提示", failures);
        Check(settingsText.Contains("测试连接", StringComparison.Ordinal) &&
              settingsText.Contains("保存", StringComparison.Ordinal) &&
              settingsText.Contains("删除配置", StringComparison.Ordinal),
            "设置窗口必须提供显式保存、测试连接和删除操作", failures);
        Check(Regex.IsMatch(
                settingsCode,
                "OnModeChanged[\\s\\S]*?ApiKeyBox\\.Clear\\(\\);[\\s\\S]*?editor\\.SelectMode",
                RegexOptions.CultureInvariant),
            "mode/profile 切换必须在更换 target 前立即清空未保存 PasswordBox replacement", failures);
        Check(MethodContains(settingsCode, "OnModeChanged", "OnSave", "IsCredentialBoundaryException") &&
              MethodContains(settingsCode, "OnSave", "OnTestConnection", "IsCredentialBoundaryException") &&
              MethodContains(settingsCode, "OnTestConnection", "OnDeleteProfile", "IsCredentialBoundaryException") &&
              MethodContains(settingsCode, "OnDeleteProfile", "OnClose", "IsCredentialBoundaryException"),
            "模式切换、保存、测试和删除必须在窗口边界收敛 Windows 凭据异常", failures);
        Check(MethodContains(homeCode, "OnOpenAiSettings", "OnLearningRoutesListKeyDown", "IsCredentialBoundaryException"),
            "打开设置必须在主页窗口边界收敛 Windows 凭据异常", failures);

        Check(presentationCode.Contains("离线演示（模板回答，不是网络模型）", StringComparison.Ordinal) &&
              routeXaml.Contains("x:Name=\"CompanionProfileBadge\"", StringComparison.Ordinal) &&
              routeXaml.Contains("Text=\"{Binding ProfileBadgeText}\"", StringComparison.Ordinal),
            "同行者必须显示明确的 profile/provider badge 与离线模板标签", failures);
        Check(routeXaml.Contains("x:Name=\"CompanionAnswer\"", StringComparison.Ordinal) &&
              routeXaml.Contains("<ScrollViewer", StringComparison.Ordinal) &&
              routeXaml.Contains("VerticalScrollBarVisibility=\"Auto\"", StringComparison.Ordinal),
            "回答区域必须可见、可聚焦且可滚动", failures);
    }

    private static async Task CheckSettingsBehaviorAsync(List<string> failures)
    {
        var settingsStore = new FakeSettingsStore(CompanionSettings.Default);
        var credentials = new FakeCredentialStore();
        var factory = new FakeFactory();
        var editor = new AiSettingsEditor(settingsStore, credentials, factory);

        Check(editor.ModeLabels.SequenceEqual(
                new[] { "离线演示", "DeepSeek", "OpenAI", "自定义兼容供应商" },
                StringComparer.Ordinal),
            "editor mode labels must be exact and ordered", failures);
        Check(credentials.ReadCount == 0 && editor.ApiKeyState == "未保存" &&
              typeof(AiSettingsEditor).GetProperties().Where(property =>
                  property.Name.Contains("ApiKey", StringComparison.Ordinal)).All(property =>
                      property.Name == nameof(AiSettingsEditor.ApiKeyState)),
            "opening settings must not read or echo an existing credential", failures);

        editor.SelectMode(CompanionProviderKind.OpenAI);
        Check(!editor.IsBaseUrlEditable,
            "OpenAI preset base URL must not be editable", failures);
        editor.DisplayName = "OpenAI 主配置";
        editor.BaseUrl = "https://attacker.example/v1/";
        editor.Model = "gpt-test";
        char[] replacement = "fixture-replacement".ToCharArray();
        editor.Save(replacement);
        Check(factory.TestCount == 0 && factory.CreateCount == 0,
            "Save must never create a provider or test a connection", failures);
        Check(settingsStore.SaveCount == 1 && credentials.WriteCount == 1 &&
              settingsStore.Current.Profiles.Single(profile => profile.Id == settingsStore.Current.ActiveProfileId).DisplayName == "OpenAI 主配置" &&
              settingsStore.Current.Profiles.Single(profile => profile.Id == settingsStore.Current.ActiveProfileId).BaseUrl ==
                  new Uri("https://api.openai.com/v1/"),
            "Save must atomically persist the active non-secret profile, canonical preset endpoint, and replacement target", failures);
        Check(replacement.All(character => character == '\0'),
            "credential replacement buffer must be cleared after save", failures);

        string openAiTarget = settingsStore.Current.Profiles.Single(profile =>
            profile.Kind == CompanionProviderKind.OpenAI).CredentialTarget!;
        credentials.Set(openAiTarget, "original-fixture");
        settingsStore.ThrowOnSave = true;
        char[] rejectedReplacement = "replacement-that-must-roll-back".ToCharArray();
        try
        {
            editor.Save(rejectedReplacement);
            failures.Add("AI settings A3: atomic save failure must escape to the UI boundary");
        }
        catch (IOException)
        {
        }
        finally
        {
            settingsStore.ThrowOnSave = false;
        }
        Check(credentials.Get(openAiTarget) == "original-fixture" &&
              rejectedReplacement.All(character => character == '\0'),
            "failed non-secret persistence must restore the prior credential and clear replacement memory", failures);

        await editor.TestConnectionAsync(CancellationToken.None);
        Check(factory.TestCount == 1,
            "only explicit TestConnectionAsync may invoke the A2 probe", failures);

        editor.SelectMode(CompanionProviderKind.CustomOpenAiCompatible);
        Check(editor.IsBaseUrlEditable,
            "custom provider base URL must remain editable", failures);
        editor.DisplayName = "兼容服务";
        editor.BaseUrl = "https://provider.example.test/v1/";
        editor.Model = "model-x";
        editor.Save("custom-fixture".ToCharArray());
        Guid customId = settingsStore.Current.ActiveProfileId;
        Check(settingsStore.Current.Profiles.Single(profile => profile.Id == customId).CredentialTarget ==
              CompanionCredentialTargets.ForProfile(customId),
            "new custom profile must use the A1 isolated credential target", failures);
        Check(!editor.DeleteSelectedProfile(confirmed: false) &&
              settingsStore.Current.Profiles.Any(profile => profile.Id == customId),
            "profile deletion must require explicit confirmation", failures);
        Check(editor.DeleteSelectedProfile(confirmed: true) &&
              !settingsStore.Current.Profiles.Any(profile => profile.Id == customId) &&
              credentials.DeletedTargets.Contains(CompanionCredentialTargets.ForProfile(customId)),
            "confirmed deletion must remove only the selected profile credential target", failures);

        CheckDeletionKeepsActiveProfileAligned(failures);
        CheckCredentialFailureBoundaries(failures);
        CheckCredentialStateObservationFailures(failures);
    }

    private static void CheckModeSwitchClearsPendingReplacement(List<string> failures)
    {
        Exception? threadFailure = null;
        string? failureMessage = null;
        var thread = new Thread(() =>
        {
            try
            {
                var settingsStore = new FakeSettingsStore(CompanionSettings.Default);
                var credentials = new FakeCredentialStore();
                var runtime = new CompanionApplicationRuntime(settingsStore, credentials, new FakeFactory());
                var window = new AiSettingsWindow(runtime);

                void SwitchWithPendingReplacement(int fromIndex, int toIndex, string replacement)
                {
                    window.ModePicker.SelectedIndex = fromIndex;
                    window.ApiKeyBox.Password = replacement;
                    window.ModePicker.SelectedIndex = toIndex;
                    if (window.ApiKeyBox.Password.Length != 0)
                    {
                        failureMessage = $"mode round-trip {fromIndex}->{toIndex} retained a pending replacement";
                    }
                    window.SaveButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }

                SwitchWithPendingReplacement(2, 1, "openai-to-deepseek-fixture");
                SwitchWithPendingReplacement(2, 0, "remote-to-offline-fixture");
                window.ModePicker.SelectedIndex = 2;
                if (window.ApiKeyBox.Password.Length != 0)
                {
                    failureMessage = "offline-to-remote round-trip restored a prior replacement";
                }
                SwitchWithPendingReplacement(3, 1, "custom-to-builtin-fixture");

                bool anyCredentialWritten = credentials.WriteCount != 0 ||
                    settingsStore.Current.Profiles
                        .Where(profile => profile.CredentialTarget is not null)
                        .Any(profile => credentials.Get(profile.CredentialTarget!) is not null);
                if (anyCredentialWritten)
                {
                    failureMessage = "a replacement from the previous mode reached the newly selected target";
                }
                window.Close();
            }
            catch (Exception exception)
            {
                threadFailure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Check(threadFailure is null && failureMessage is null,
            failureMessage ?? $"mode-switch PasswordBox regression failed: {threadFailure?.GetType().Name}", failures);
    }

    private static void CheckModeSwitchCredentialStateFailure(List<string> failures, Exception failure)
    {
        Exception? threadFailure = null;
        string? failureMessage = null;
        var thread = new Thread(() =>
        {
            try
            {
                var settingsStore = new FakeSettingsStore(CompanionSettings.Default);
                var credentials = new FakeCredentialStore();
                var runtime = new CompanionApplicationRuntime(settingsStore, credentials, new FakeFactory());
                var window = new AiSettingsWindow(runtime);
                credentials.ThrowOnExists = failure;

                window.ModePicker.SelectedIndex = (int)CompanionProviderKind.DeepSeek;
                if (window.ModePicker.SelectedIndex != (int)CompanionProviderKind.DeepSeek ||
                    window.ApiKeyStateText.Text != "状态未知" ||
                    !window.StatusText.Text.Contains("暂时无法确认", StringComparison.Ordinal) ||
                    window.StatusText.Text.Contains("fixture-secret-must-not-leak", StringComparison.Ordinal))
                {
                    failureMessage = $"mode switch did not expose an aligned non-secret unknown state for {failure.GetType().Name}";
                }
                window.Close();
            }
            catch (Exception exception)
            {
                threadFailure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Check(threadFailure is null && failureMessage is null,
            failureMessage ?? $"mode-switch credential boundary escaped: {threadFailure?.GetType().Name}", failures);
    }

    private static async Task CheckCompositionAndPresentationAsync(string appRoot, List<string> failures)
    {
        CompanionProfile offline = CompanionSettings.Default.Profiles.Single(profile => profile.Kind == CompanionProviderKind.OfflineDemo);
        CompanionProfile remote = CompanionSettings.Default.Profiles.Single(profile => profile.Kind == CompanionProviderKind.DeepSeek);
        var settingsStore = new FakeSettingsStore(new CompanionSettings(remote.Id, CompanionSettings.Default.Profiles));
        var factory = new FakeFactory();
        var runtime = new CompanionApplicationRuntime(settingsStore, new FakeCredentialStore(), factory);
        CompanionWindowContext first = runtime.CreateWindowContext();
        settingsStore.Current = new CompanionSettings(offline.Id, CompanionSettings.Default.Profiles);
        CompanionWindowContext second = runtime.CreateWindowContext();
        Check(factory.CreatedProfiles.SequenceEqual(new[] { remote.Id, offline.Id }),
            "each newly opened learning window must resolve the current active profile through A2 factory", failures);
        Check(first.BadgeText.Contains("DeepSeek", StringComparison.Ordinal) &&
              first.BadgeText.Contains(remote.Model, StringComparison.Ordinal) &&
              second.BadgeText == "离线演示（模板回答，不是网络模型）",
            "window badges must distinguish remote profile/model and Offline Demo", failures);

        string contentRoot = Path.Combine(appRoot, "Content");
        LearningRouteCatalogItem routeRegistration = LearningRouteRegistry.Load(contentRoot).Routes[0];
        using FileStream stream = File.OpenRead(Path.Combine(contentRoot, routeRegistration.FileName));
        LearningPack pack = LearningPack.Load(stream);
        LessonDefinition lesson = pack.GetLesson(pack.GetRoute(routeRegistration.RouteId).LessonIds[0]);
        var remoteService = new RecordingCompanionService(success: true);
        var consent = new CompanionConsentSession();
        var prompt = new FakeConsentPrompt(false, true);
        var viewModel = new CompanionViewModel(remoteService, CompanionPresentation.ForProfile(remote), consent, prompt);
        viewModel.SetLesson(pack, lesson);
        viewModel.UserText = "保留这份草稿";
        await viewModel.SendAsync();
        Check(remoteService.SendCount == 0 && viewModel.UserText == "保留这份草稿" &&
              viewModel.Status.Contains("未发送", StringComparison.Ordinal),
            "declining first-send consent must make no service call and preserve the draft", failures);
        await viewModel.SendAsync();
        Check(remoteService.SendCount == 1 && prompt.ConfirmCount == 2 &&
              viewModel.Status == "回答完成" && viewModel.AccessibleStatus == "思想同行者回答完成。",
            "accepting consent must send once and expose the explicit completed/live state", failures);
        viewModel.UserText = "第二次发送";
        await viewModel.SendAsync();
        Check(remoteService.SendCount == 2 && prompt.ConfirmCount == 2,
            "accepted remote consent must be session-only and prompt only before the first remote submission", failures);

        var failingService = new RecordingCompanionService(success: false);
        var failing = new CompanionViewModel(
            failingService,
            CompanionPresentation.ForProfile(remote),
            new CompanionConsentSession(preapproved: true),
            new FakeConsentPrompt(true));
        failing.SetLesson(pack, lesson);
        failing.UserText = "失败后仍保留";
        await failing.SendAsync();
        Check(failing.UserText == "失败后仍保留" && !failing.HasResponse &&
              failing.Status.Contains("供应商失败", StringComparison.Ordinal) && failingService.SendCount == 1,
            "provider failure must preserve the draft, show a specific failure, and never fake-fallback", failures);

        var deferredService = new DeferredRecordingCompanionService();
        var answering = new CompanionViewModel(
            deferredService,
            CompanionPresentation.ForProfile(remote),
            new CompanionConsentSession(preapproved: true),
            new FakeConsentPrompt(true));
        answering.SetLesson(pack, lesson);
        answering.UserText = "观察中间状态";
        Task pending = answering.SendAsync();
        Check(answering.IsBusy && answering.Status == "正在回答" &&
              answering.AccessibleStatus == "思想同行者正在回答。",
            "remote presentation must expose the explicit answering state", failures);
        deferredService.Complete(new CompanionOperationResult(
            null, CompanionFailureKind.ProviderUnavailable, "供应商失败，请稍后重试。", null));
        await pending;
    }

    private static void Check(bool condition, string message, List<string> failures)
    {
        if (!condition) failures.Add("AI settings A3: " + message);
    }

    private static bool MethodContains(string source, string methodName, string nextMethodName, string value)
    {
        int start = source.IndexOf(methodName, StringComparison.Ordinal);
        int end = source.IndexOf(nextMethodName, start + methodName.Length, StringComparison.Ordinal);
        return start >= 0 && end > start && source.AsSpan(start, end - start).Contains(value, StringComparison.Ordinal);
    }

    private static void CheckDeletionKeepsActiveProfileAligned(List<string> failures)
    {
        CompanionProfile offline = CompanionSettings.Default.Profiles.Single(profile => profile.Kind == CompanionProviderKind.OfflineDemo);
        CompanionProfile deepSeek = CompanionSettings.Default.Profiles.Single(profile => profile.Kind == CompanionProviderKind.DeepSeek);
        CompanionProfile openAi = CompanionSettings.Default.Profiles.Single(profile => profile.Kind == CompanionProviderKind.OpenAI);

        var nonActiveStore = new FakeSettingsStore(new CompanionSettings(
            deepSeek.Id,
            CompanionSettings.Default.Profiles));
        var nonActiveCredentials = new FakeCredentialStore();
        nonActiveCredentials.Set(openAi.CredentialTarget!, "non-active-fixture");
        var nonActiveFactory = new FakeFactory();
        var nonActiveEditor = new AiSettingsEditor(nonActiveStore, nonActiveCredentials, nonActiveFactory);
        nonActiveEditor.SelectMode(CompanionProviderKind.OpenAI);
        Check(nonActiveEditor.DeleteSelectedProfile(confirmed: true) &&
              nonActiveStore.Current.ActiveProfileId == deepSeek.Id &&
              nonActiveEditor.SelectedMode == CompanionProviderKind.DeepSeek &&
              nonActiveEditor.Status.Contains("DeepSeek", StringComparison.Ordinal) &&
              !nonActiveEditor.Status.Contains("离线演示已激活", StringComparison.Ordinal),
            "deleting a non-active profile must retain and display the true active profile", failures);
        var nonActiveRuntime = new CompanionApplicationRuntime(
            nonActiveStore, nonActiveCredentials, nonActiveFactory);
        _ = nonActiveRuntime.CreateWindowContext();
        Check(nonActiveFactory.CreatedProfiles.LastOrDefault() == deepSeek.Id,
            "the next learning window must use the retained active profile after non-active deletion", failures);

        var activeStore = new FakeSettingsStore(new CompanionSettings(
            deepSeek.Id,
            CompanionSettings.Default.Profiles));
        var activeCredentials = new FakeCredentialStore();
        activeCredentials.Set(deepSeek.CredentialTarget!, "active-fixture");
        var activeFactory = new FakeFactory();
        var activeEditor = new AiSettingsEditor(activeStore, activeCredentials, activeFactory);
        Check(activeEditor.DeleteSelectedProfile(confirmed: true) &&
              activeStore.Current.ActiveProfileId == offline.Id &&
              activeEditor.SelectedMode == CompanionProviderKind.OfflineDemo &&
              activeEditor.Status.Contains("离线演示已激活", StringComparison.Ordinal),
            "deleting the active profile must explicitly activate and display Offline Demo", failures);
        var activeRuntime = new CompanionApplicationRuntime(activeStore, activeCredentials, activeFactory);
        _ = activeRuntime.CreateWindowContext();
        Check(activeFactory.CreatedProfiles.LastOrDefault() == offline.Id,
            "the next learning window must use Offline Demo after active-profile deletion", failures);
    }

    private static void CheckCredentialFailureBoundaries(List<string> failures)
    {
        const string secretMarker = "fixture-secret-must-not-leak";
        Exception[] nativeFailures =
        [
            new Win32Exception(5, secretMarker),
            new SecurityException(secretMarker),
            new CryptographicException(secretMarker),
        ];
        foreach (Exception failure in nativeFailures)
        {
            string message = AiSettingsErrorMessages.For(failure);
            Check(AiSettingsErrorMessages.IsCredentialBoundaryException(failure) &&
                  !message.Contains(secretMarker, StringComparison.Ordinal) &&
                  message.Contains("凭据", StringComparison.Ordinal),
                $"{failure.GetType().Name} must map to a non-secret credential message", failures);
        }

        var saveStore = new FakeSettingsStore(CompanionSettings.Default) { ThrowOnSave = true };
        var saveCredentials = new FakeCredentialStore { ThrowOnDelete = new SecurityException(secretMarker) };
        var saveEditor = new AiSettingsEditor(saveStore, saveCredentials, new FakeFactory());
        saveEditor.SelectMode(CompanionProviderKind.CustomOpenAiCompatible);
        saveEditor.DisplayName = "补偿测试";
        saveEditor.BaseUrl = "https://provider.example.test/v1/";
        saveEditor.Model = "model-x";
        Exception? saveFailure = null;
        try
        {
            saveEditor.Save("replacement-fixture".ToCharArray());
        }
        catch (Exception exception)
        {
            saveFailure = exception;
        }
        Check(saveFailure is AiSettingsConsistencyException &&
              !AiSettingsErrorMessages.For(saveFailure).Contains(secretMarker, StringComparison.Ordinal) &&
              AiSettingsErrorMessages.For(saveFailure).Contains("可能不一致", StringComparison.Ordinal),
            "save compensation failure must be distinguishable and mapped without secret text", failures);

        CompanionProfile openAi = CompanionSettings.Default.Profiles.Single(profile => profile.Kind == CompanionProviderKind.OpenAI);
        var deleteStore = new FakeSettingsStore(new CompanionSettings(openAi.Id, CompanionSettings.Default.Profiles))
        {
            ThrowOnSave = true,
        };
        var deleteCredentials = new FakeCredentialStore { ThrowOnWrite = new Win32Exception(5, secretMarker) };
        deleteCredentials.Set(openAi.CredentialTarget!, "original-fixture");
        var deleteEditor = new AiSettingsEditor(deleteStore, deleteCredentials, new FakeFactory());
        Exception? deleteFailure = null;
        try
        {
            deleteEditor.DeleteSelectedProfile(confirmed: true);
        }
        catch (Exception exception)
        {
            deleteFailure = exception;
        }
        Check(deleteFailure is AiSettingsConsistencyException &&
              !AiSettingsErrorMessages.For(deleteFailure).Contains(secretMarker, StringComparison.Ordinal),
            "delete compensation failure must be distinguishable and mapped without secret text", failures);
    }

    private static void CheckCredentialStateObservationFailures(List<string> failures)
    {
        const string secretMarker = "fixture-secret-must-not-leak";
        Exception[] observationFailures =
        [
            new Win32Exception(5, secretMarker),
            new SecurityException(secretMarker),
            new CryptographicException(secretMarker),
        ];
        foreach (Exception failure in observationFailures)
        {
            CheckModeSwitchCredentialStateFailure(failures, failure);
        }

        var writeFailureStore = new FakeSettingsStore(CompanionSettings.Default);
        var writeFailureCredentials = new FakeCredentialStore
        {
            ThrowOnWrite = new Win32Exception(5, secretMarker),
        };
        var writeFailureEditor = new AiSettingsEditor(
            writeFailureStore, writeFailureCredentials, new FakeFactory());
        writeFailureEditor.SelectMode(CompanionProviderKind.CustomOpenAiCompatible);
        writeFailureEditor.DisplayName = "写失败测试";
        writeFailureEditor.BaseUrl = "https://provider.example.test/v1/";
        writeFailureEditor.Model = "model-x";
        Exception? writeFailure = null;
        try
        {
            writeFailureEditor.Save("replacement-fixture".ToCharArray());
        }
        catch (Exception exception)
        {
            writeFailure = exception;
        }
        Check(writeFailure is Win32Exception && writeFailureStore.SaveCount == 0 &&
              writeFailureStore.Current.ActiveProfileId == CompanionSettings.Default.ActiveProfileId,
            "credential Write failure must escape without committing settings", failures);

        CompanionProfile primaryDeleteProfile = CompanionSettings.Default.Profiles.Single(profile =>
            profile.Kind == CompanionProviderKind.OpenAI);
        var primaryDeleteStore = new FakeSettingsStore(new CompanionSettings(
            primaryDeleteProfile.Id,
            CompanionSettings.Default.Profiles));
        var primaryDeleteCredentials = new FakeCredentialStore
        {
            ThrowOnDelete = new SecurityException(secretMarker),
        };
        primaryDeleteCredentials.Set(primaryDeleteProfile.CredentialTarget!, "delete-fixture");
        var primaryDeleteEditor = new AiSettingsEditor(
            primaryDeleteStore, primaryDeleteCredentials, new FakeFactory());
        Exception? primaryDeleteFailure = null;
        try
        {
            primaryDeleteEditor.DeleteSelectedProfile(confirmed: true);
        }
        catch (Exception exception)
        {
            primaryDeleteFailure = exception;
        }
        Check(primaryDeleteFailure is SecurityException && primaryDeleteStore.SaveCount == 0 &&
              primaryDeleteStore.Current.Profiles.Any(profile => profile.Id == primaryDeleteProfile.Id),
            "credential Delete failure must escape without committing settings", failures);

        foreach (Exception failure in observationFailures)
        {
            CheckPostCommitSaveCredentialStateFailure(failures, failure, secretMarker);
            CheckPostCommitDeleteCredentialStateFailure(failures, failure, secretMarker);
        }

        CompanionProfile offline = CompanionSettings.Default.Profiles.Single(profile => profile.Kind == CompanionProviderKind.OfflineDemo);
        CompanionProfile deepSeek = CompanionSettings.Default.Profiles.Single(profile => profile.Kind == CompanionProviderKind.DeepSeek);

        var activeStore = new FakeSettingsStore(new CompanionSettings(
            deepSeek.Id,
            CompanionSettings.Default.Profiles));
        var activeCredentials = new FakeCredentialStore();
        activeCredentials.Set(deepSeek.CredentialTarget!, "active-fixture");
        var activeFactory = new FakeFactory();
        var activeEditor = new AiSettingsEditor(activeStore, activeCredentials, activeFactory);
        activeCredentials.ThrowOnExists = new CryptographicException(secretMarker);
        Exception? activeFailure = null;
        bool activeDeleted = false;
        try
        {
            activeDeleted = activeEditor.DeleteSelectedProfile(confirmed: true);
        }
        catch (Exception exception)
        {
            activeFailure = exception;
        }
        var activeRuntime = new CompanionApplicationRuntime(activeStore, activeCredentials, activeFactory);
        _ = activeRuntime.CreateWindowContext();
        Check(activeFailure is null && activeDeleted &&
              activeStore.Current.ActiveProfileId == offline.Id &&
              !activeStore.Current.Profiles.Any(profile => profile.Id == deepSeek.Id) &&
              activeEditor.SelectedMode == CompanionProviderKind.OfflineDemo &&
              activeEditor.ApiKeyState == "未保存" &&
              activeEditor.Status.Contains("离线演示已激活", StringComparison.Ordinal) &&
              activeFactory.CreatedProfiles.LastOrDefault() == offline.Id,
            "post-commit active Delete refresh must rebuild Offline Demo without querying a deleted target", failures);
    }

    private static void CheckPostCommitSaveCredentialStateFailure(
        List<string> failures,
        Exception observationFailure,
        string secretMarker)
    {
        var settingsStore = new FakeSettingsStore(CompanionSettings.Default);
        var credentials = new FakeCredentialStore();
        var editor = new AiSettingsEditor(settingsStore, credentials, new FakeFactory());
        editor.SelectMode(CompanionProviderKind.CustomOpenAiCompatible);
        editor.DisplayName = "状态未知保存测试";
        editor.BaseUrl = "https://provider.example.test/v1/";
        editor.Model = "model-x";
        credentials.ThrowOnExists = observationFailure;
        Exception? saveFailure = null;
        try
        {
            editor.Save("replacement-fixture".ToCharArray());
        }
        catch (Exception exception)
        {
            saveFailure = exception;
        }

        Check(saveFailure is null && settingsStore.SaveCount == 1 &&
              settingsStore.Current.ActiveProfileId != CompanionSettings.Default.ActiveProfileId &&
              editor.ApiKeyState == "状态未知" &&
              editor.Status.Contains("设置已保存", StringComparison.Ordinal) &&
              editor.Status.Contains("暂时无法确认", StringComparison.Ordinal) &&
              !editor.Status.Contains(secretMarker, StringComparison.Ordinal),
            $"post-commit Save Exists {observationFailure.GetType().Name} must preserve success and expose a non-secret unknown state",
            failures);
    }

    private static void CheckPostCommitDeleteCredentialStateFailure(
        List<string> failures,
        Exception observationFailure,
        string secretMarker)
    {
        CompanionProfile deepSeek = CompanionSettings.Default.Profiles.Single(profile =>
            profile.Kind == CompanionProviderKind.DeepSeek);
        CompanionProfile openAi = CompanionSettings.Default.Profiles.Single(profile =>
            profile.Kind == CompanionProviderKind.OpenAI);
        var settingsStore = new FakeSettingsStore(new CompanionSettings(
            deepSeek.Id,
            CompanionSettings.Default.Profiles));
        var credentials = new FakeCredentialStore();
        credentials.Set(openAi.CredentialTarget!, "non-active-fixture");
        var factory = new FakeFactory();
        var editor = new AiSettingsEditor(settingsStore, credentials, factory);
        editor.SelectMode(CompanionProviderKind.OpenAI);
        credentials.ThrowOnExists = observationFailure;
        Exception? deleteFailure = null;
        bool deleted = false;
        try
        {
            deleted = editor.DeleteSelectedProfile(confirmed: true);
        }
        catch (Exception exception)
        {
            deleteFailure = exception;
        }

        var runtime = new CompanionApplicationRuntime(settingsStore, credentials, factory);
        _ = runtime.CreateWindowContext();
        Check(deleteFailure is null && deleted && settingsStore.SaveCount == 1 &&
              settingsStore.Current.ActiveProfileId == deepSeek.Id &&
              !settingsStore.Current.Profiles.Any(profile => profile.Id == openAi.Id) &&
              editor.SelectedMode == CompanionProviderKind.DeepSeek &&
              editor.ApiKeyState == "状态未知" &&
              editor.Status.Contains("已删除", StringComparison.Ordinal) &&
              editor.Status.Contains("暂时无法确认", StringComparison.Ordinal) &&
              !editor.Status.Contains(secretMarker, StringComparison.Ordinal) &&
              factory.CreatedProfiles.LastOrDefault() == deepSeek.Id,
            $"post-commit non-active Delete Exists {observationFailure.GetType().Name} must rebuild from persisted active settings",
            failures);
    }

    private sealed class FakeSettingsStore(CompanionSettings settings) : ICompanionSettingsStore
    {
        internal CompanionSettings Current { get; set; } = settings;
        internal int SaveCount { get; private set; }
        internal bool ThrowOnSave { get; set; }
        public CompanionSettingsLoadResult Load() => new(Current, null);
        public void Save(CompanionSettings value)
        {
            SaveCount++;
            if (ThrowOnSave) throw new IOException("simulated settings replacement failure");
            Current = value;
        }
    }

    private sealed class FakeCredentialStore : ICredentialStore
    {
        private readonly Dictionary<string, char[]> values = new(StringComparer.Ordinal);
        internal int ReadCount { get; private set; }
        internal int WriteCount { get; private set; }
        internal HashSet<string> DeletedTargets { get; } = new(StringComparer.Ordinal);
        internal Exception? ThrowOnWrite { get; set; }
        internal Exception? ThrowOnDelete { get; set; }
        internal Exception? ThrowOnExists { get; set; }
        internal void Set(string target, string value) => values[target] = value.ToCharArray();
        internal string? Get(string target) => values.TryGetValue(target, out char[]? value) ? new string(value) : null;
        public bool Exists(string targetName)
        {
            if (ThrowOnExists is not null) throw ThrowOnExists;
            return values.ContainsKey(targetName);
        }
        public SensitiveBuffer? Read(string targetName)
        {
            ReadCount++;
            return values.TryGetValue(targetName, out char[]? value) ? SensitiveBuffer.CopyFrom(value) : null;
        }
        public void Write(string targetName, SensitiveBuffer value)
        {
            if (ThrowOnWrite is not null) throw ThrowOnWrite;
            WriteCount++;
            values[targetName] = value.Span.ToArray();
        }
        public bool Delete(string targetName)
        {
            if (ThrowOnDelete is not null) throw ThrowOnDelete;
            DeletedTargets.Add(targetName);
            return values.Remove(targetName);
        }
    }

    private sealed class FakeFactory : ICompanionServiceFactory
    {
        internal int CreateCount { get; private set; }
        internal int TestCount { get; private set; }
        internal List<Guid> CreatedProfiles { get; } = [];
        public ICompanionService Create(CompanionProfile profile)
        {
            CreateCount++;
            CreatedProfiles.Add(profile.Id);
            return new RecordingCompanionService(success: true);
        }
        public Task<CompanionConnectionTestResult> TestConnectionAsync(CompanionProfile profile, CancellationToken cancellationToken)
        {
            TestCount++;
            return Task.FromResult(new CompanionConnectionTestResult(true, CompanionFailureKind.None, "连接成功。", null));
        }
    }

    private sealed class RecordingCompanionService(bool success) : ICompanionService
    {
        internal int SendCount { get; private set; }
        public Task<CompanionOperationResult> SendAsync(CompanionDraft draft, CancellationToken cancellationToken)
        {
            SendCount++;
            return Task.FromResult(success
                ? new CompanionOperationResult(new CompanionAnswer("听见", "问题", "关系", CompanionBasisLabel.AI提问, []), CompanionFailureKind.None, string.Empty, null)
                : new CompanionOperationResult(null, CompanionFailureKind.ProviderUnavailable, "供应商失败，请稍后重试。", null));
        }
    }

    private sealed class DeferredRecordingCompanionService : ICompanionService
    {
        private readonly TaskCompletionSource<CompanionOperationResult> completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<CompanionOperationResult> SendAsync(CompanionDraft draft, CancellationToken cancellationToken) =>
            completion.Task;
        internal void Complete(CompanionOperationResult result) => completion.SetResult(result);
    }

    private sealed class FakeConsentPrompt(params bool[] answers) : ICompanionConsentPrompt
    {
        private readonly Queue<bool> answers = new(answers);
        internal int ConfirmCount { get; private set; }
        public bool Confirm(CompanionConsentRequest request)
        {
            ConfirmCount++;
            return answers.Dequeue();
        }
    }
}

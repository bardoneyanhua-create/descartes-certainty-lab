using System.Net.Http;
using Descartes.CertaintyLab.ThoughtCompanion.Security;
using Descartes.CertaintyLab.ThoughtCompanion.Settings;

namespace Descartes.CertaintyLab.ThoughtCompanion;

public sealed record CompanionPresentation(
    CompanionProfile Profile,
    string BadgeText,
    bool IsRemote)
{
    public static CompanionPresentation ForProfile(CompanionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return profile.Kind == CompanionProviderKind.OfflineDemo
            ? new(profile, "离线演示（模板回答，不是网络模型）", false)
            : new(profile, $"{ProviderLabel(profile.Kind)} · {profile.DisplayName} · {profile.Model}", true);
    }

    private static string ProviderLabel(CompanionProviderKind kind) => kind switch
    {
        CompanionProviderKind.DeepSeek => "DeepSeek",
        CompanionProviderKind.OpenAI => "OpenAI",
        CompanionProviderKind.CustomOpenAiCompatible => "自定义兼容供应商",
        _ => "离线演示",
    };
}

public sealed record CompanionConsentRequest(string ProviderDisplayName, string DisclosureText);

public interface ICompanionConsentPrompt
{
    bool Confirm(CompanionConsentRequest request);
}

public sealed class CompanionConsentSession
{
    private bool approved;

    public CompanionConsentSession(bool preapproved = false) => approved = preapproved;

    public bool EnsureApproved(CompanionProfile profile, ICompanionConsentPrompt prompt)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(prompt);
        if (approved)
        {
            return true;
        }

        bool accepted = prompt.Confirm(new(
            profile.DisplayName,
            $"你的文本和当前文章上下文将发送给“{profile.DisplayName}”。同意仅在本次应用会话中有效，不会保存。"));
        if (accepted)
        {
            approved = true;
        }

        return accepted;
    }
}

public sealed record CompanionWindowContext(
    ICompanionService Service,
    CompanionPresentation Presentation,
    CompanionConsentSession ConsentSession)
{
    public string BadgeText => Presentation.BadgeText;
}

public sealed class CompanionApplicationRuntime : IDisposable
{
    private static readonly Lazy<CompanionApplicationRuntime> Production = new(CreateProduction);
    private readonly ICompanionSettingsStore settingsStore;
    private readonly ICredentialStore credentials;
    private readonly ICompanionServiceFactory factory;
    private readonly CompanionConsentSession consentSession = new();
    private readonly HttpClient? ownedClient;

    public CompanionApplicationRuntime(
        ICompanionSettingsStore settingsStore,
        ICredentialStore credentials,
        ICompanionServiceFactory factory)
        : this(settingsStore, credentials, factory, null)
    {
    }

    private CompanionApplicationRuntime(
        ICompanionSettingsStore settingsStore,
        ICredentialStore credentials,
        ICompanionServiceFactory factory,
        HttpClient? ownedClient)
    {
        this.settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
        this.credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
        this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
        this.ownedClient = ownedClient;
    }

    public static CompanionApplicationRuntime Current => Production.Value;

    public AiSettingsEditor CreateSettingsEditor() => new(settingsStore, credentials, factory);

    public CompanionWindowContext CreateWindowContext()
    {
        CompanionSettings settings = settingsStore.Load().Settings;
        CompanionProfile active = settings.Profiles.Single(profile => profile.Id == settings.ActiveProfileId);
        return new(factory.Create(active), CompanionPresentation.ForProfile(active), consentSession);
    }

    public void Dispose() => ownedClient?.Dispose();

    private static CompanionApplicationRuntime CreateProduction()
    {
        var client = new HttpClient();
        var credentials = new WindowsCredentialStore();
        var factory = ThoughtCompanionComposition.CreateServiceFactory(
            client,
            credentials,
            new CompanionBudgetOptions(20_000, 40_000, 1, "CNY", 0, 0, 0),
            new FileCompanionAuditSink(),
            TimeProvider.System,
            TimeSpan.FromSeconds(30),
            512);
        return new(new JsonCompanionSettingsStore(), credentials, factory, client);
    }
}

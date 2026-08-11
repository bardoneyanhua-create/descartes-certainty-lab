using System.Collections.ObjectModel;
using System.Net;
using Descartes.CertaintyLab.ThoughtCompanion.Security;

namespace Descartes.CertaintyLab.ThoughtCompanion.Settings;

public enum CompanionProviderKind
{
    OfflineDemo,
    DeepSeek,
    OpenAI,
    CustomOpenAiCompatible,
}

public sealed record CompanionProfile
{
    public static Uri DeepSeekBaseUrl { get; } = new("https://api.deepseek.com/");
    public static Uri OpenAiBaseUrl { get; } = new("https://api.openai.com/v1/");

    public CompanionProfile(
        Guid id,
        CompanionProviderKind kind,
        string displayName,
        Uri? baseUrl,
        string model,
        string? credentialTarget)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Profile ID must not be empty.", nameof(id));
        }

        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Profile display name must not be blank.", nameof(displayName));
        }

        if (kind == CompanionProviderKind.OfflineDemo)
        {
            if (baseUrl is not null || !string.IsNullOrEmpty(model) || credentialTarget is not null)
            {
                throw new ArgumentException("Offline Demo cannot have remote-provider configuration.");
            }
        }
        else
        {
            ValidateRemoteBaseUrl(baseUrl);
            Uri? canonicalBaseUrl = kind switch
            {
                CompanionProviderKind.DeepSeek => DeepSeekBaseUrl,
                CompanionProviderKind.OpenAI => OpenAiBaseUrl,
                _ => null,
            };
            if (canonicalBaseUrl is not null && baseUrl != canonicalBaseUrl)
            {
                throw new ArgumentException($"{kind} must use its canonical base URL.", nameof(baseUrl));
            }

            if (string.IsNullOrWhiteSpace(model))
            {
                throw new ArgumentException("Profile model must not be blank.", nameof(model));
            }

            string expectedTarget = CompanionCredentialTargets.ForProfile(id);
            if (!string.Equals(credentialTarget, expectedTarget, StringComparison.Ordinal))
            {
                throw new ArgumentException("Profile credential target is not application-owned.", nameof(credentialTarget));
            }
        }

        Id = id;
        Kind = kind;
        DisplayName = displayName.Trim();
        BaseUrl = baseUrl;
        Model = model?.Trim() ?? string.Empty;
        CredentialTarget = credentialTarget;
    }

    public Guid Id { get; }
    public CompanionProviderKind Kind { get; }
    public string DisplayName { get; }
    public Uri? BaseUrl { get; }
    public string Model { get; }
    public string? CredentialTarget { get; }

    private static void ValidateRemoteBaseUrl(Uri? baseUrl)
    {
        if (baseUrl is null || !baseUrl.IsAbsoluteUri ||
            !string.Equals(baseUrl.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Provider base URL must be an absolute HTTPS URL.", nameof(baseUrl));
        }

        if (!string.IsNullOrEmpty(baseUrl.UserInfo) ||
            baseUrl.OriginalString.IndexOfAny(['?', '#']) >= 0)
        {
            throw new ArgumentException("Provider base URL cannot contain userinfo, a query, or a fragment.", nameof(baseUrl));
        }

        string hostWithoutRootDot = baseUrl.IdnHost.TrimEnd('.');
        if (hostWithoutRootDot.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            hostWithoutRootDot.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase) ||
            (IPAddress.TryParse(baseUrl.IdnHost, out IPAddress? address) && IsPrivateOrLoopback(address)))
        {
            throw new ArgumentException(
                "Provider base URL cannot use a localhost name or a loopback/private literal host.",
                nameof(baseUrl));
        }
    }

    private static bool IsPrivateOrLoopback(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
        {
            return true;
        }

        byte[] bytes = address.GetAddressBytes();
        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return bytes[0] == 10 ||
                   bytes[0] == 127 ||
                   (bytes[0] == 169 && bytes[1] == 254) ||
                   (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) ||
                   (bytes[0] == 192 && bytes[1] == 168);
        }

        return address.IsIPv6LinkLocal ||
               address.IsIPv6SiteLocal ||
               (bytes[0] & 0xfe) == 0xfc;
    }
}

public sealed record CompanionSettings
{
    private static readonly Guid OfflineDemoId = Guid.Parse("47f93aa7-160b-45eb-b76a-0982870a3da8");
    private static readonly Guid DeepSeekId = Guid.Parse("6a304240-d679-4c5b-87ad-b995f6875022");
    private static readonly Guid OpenAiId = Guid.Parse("c149fe78-7b18-4c55-9057-7253700b922d");

    public CompanionSettings(Guid activeProfileId, IEnumerable<CompanionProfile> profiles)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        CompanionProfile[] snapshot = profiles.ToArray();
        if (snapshot.Length == 0 || snapshot.Any(profile => profile is null))
        {
            throw new ArgumentException("At least one non-null profile is required.", nameof(profiles));
        }

        if (snapshot.Select(profile => profile.Id).Distinct().Count() != snapshot.Length)
        {
            throw new ArgumentException("Profile IDs must be unique.", nameof(profiles));
        }

        CompanionProfile[] offlineProfiles = snapshot
            .Where(profile => profile.Kind == CompanionProviderKind.OfflineDemo)
            .ToArray();
        if (offlineProfiles.Length != 1 || !IsCanonicalOfflineDemo(offlineProfiles[0]))
        {
            throw new ArgumentException(
                "Settings must contain exactly one canonical Offline Demo profile.",
                nameof(profiles));
        }

        if (snapshot.Count(profile => profile.Kind == CompanionProviderKind.DeepSeek) > 1 ||
            snapshot.Count(profile => profile.Kind == CompanionProviderKind.OpenAI) > 1)
        {
            throw new ArgumentException("Built-in provider presets must be unique by kind.", nameof(profiles));
        }

        if (!snapshot.Any(profile => profile.Id == activeProfileId))
        {
            throw new ArgumentException("Active profile must exist in the profile collection.", nameof(activeProfileId));
        }

        ActiveProfileId = activeProfileId;
        Profiles = Array.AsReadOnly(snapshot);
    }

    private static bool IsCanonicalOfflineDemo(CompanionProfile profile) =>
        profile.Id == OfflineDemoId &&
        profile.DisplayName == "Offline Demo" &&
        profile.BaseUrl is null &&
        profile.Model.Length == 0 &&
        profile.CredentialTarget is null;

    public Guid ActiveProfileId { get; }
    public IReadOnlyList<CompanionProfile> Profiles { get; }

    public static CompanionSettings Default
    {
        get
        {
            var offline = new CompanionProfile(
                OfflineDemoId,
                CompanionProviderKind.OfflineDemo,
                "Offline Demo",
                null,
                string.Empty,
                null);
            var deepSeek = new CompanionProfile(
                DeepSeekId,
                CompanionProviderKind.DeepSeek,
                "DeepSeek",
                CompanionProfile.DeepSeekBaseUrl,
                "deepseek-chat",
                CompanionCredentialTargets.ForProfile(DeepSeekId));
            var openAi = new CompanionProfile(
                OpenAiId,
                CompanionProviderKind.OpenAI,
                "OpenAI",
                CompanionProfile.OpenAiBaseUrl,
                "gpt-5-mini",
                CompanionCredentialTargets.ForProfile(OpenAiId));
            return new CompanionSettings(offline.Id, [offline, deepSeek, openAi]);
        }
    }
}

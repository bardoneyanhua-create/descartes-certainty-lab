using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Descartes.CertaintyLab.ThoughtCompanion.Settings;

public sealed record CompanionSettingsLoadResult(CompanionSettings Settings, string? Diagnostic);

public interface ICompanionSettingsStore
{
    CompanionSettingsLoadResult Load();
    void Save(CompanionSettings settings);
}

internal interface ICompanionSettingsFileOperations
{
    void MoveReplace(string sourcePath, string destinationPath);
    bool Exists(string path);
    void Delete(string path);
}

public sealed class JsonCompanionSettingsStore : ICompanionSettingsStore
{
    private const string FileName = "companion-settings.json";
    private readonly string filePath;
    private readonly ICompanionSettingsFileOperations fileOperations;

    public JsonCompanionSettingsStore()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PhilosophyVault",
            "Descartes.CertaintyLab",
            FileName))
    {
    }

    public JsonCompanionSettingsStore(string filePath)
        : this(filePath, new CompanionSettingsFileOperations())
    {
    }

    internal JsonCompanionSettingsStore(
        string filePath,
        ICompanionSettingsFileOperations fileOperations)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("Settings path must not be blank.", nameof(filePath));
        }

        this.filePath = Path.GetFullPath(filePath);
        this.fileOperations = fileOperations ?? throw new ArgumentNullException(nameof(fileOperations));
    }

    public CompanionSettingsLoadResult Load()
    {
        if (!File.Exists(filePath))
        {
            return new(CompanionSettings.Default, "AI settings file is missing; Offline Demo is active.");
        }

        try
        {
            using FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            SettingsDocument document = JsonSerializer.Deserialize<SettingsDocument>(stream, JsonOptions)
                ?? throw new InvalidDataException("Settings document is empty.");
            CompanionProfile[] profiles = document.Profiles
                ?.Select(profile => profile?.ToProfile() ??
                    throw new InvalidDataException("Settings profile cannot be null."))
                .ToArray()
                ?? throw new InvalidDataException("Settings profiles are missing.");
            return new(new CompanionSettings(document.ActiveProfileId, profiles), null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                          JsonException or InvalidDataException or ArgumentException or
                                          FormatException or NotSupportedException or OverflowException)
        {
            return new(CompanionSettings.Default, "AI settings could not be loaded; Offline Demo is active.");
        }
    }

    public void Save(CompanionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        string directory = Path.GetDirectoryName(filePath)!;
        Directory.CreateDirectory(directory);
        string temporaryPath = Path.Combine(directory, $".{Path.GetFileName(filePath)}.{Guid.NewGuid():N}.tmp");
        Exception? saveFailure = null;
        try
        {
            var document = new SettingsDocument(
                settings.ActiveProfileId,
                settings.Profiles.Select(ProfileDocument.FromProfile).ToArray());
            using (var stream = new FileStream(
                       temporaryPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       4096,
                       FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(stream, document, JsonOptions);
                stream.Flush(flushToDisk: true);
            }

            fileOperations.MoveReplace(temporaryPath, filePath);
        }
        catch (Exception exception)
        {
            saveFailure = exception;
            throw;
        }
        finally
        {
            if (saveFailure is null)
            {
                DeleteTemporaryFileIfPresent(temporaryPath);
            }
            else
            {
                try
                {
                    DeleteTemporaryFileIfPresent(temporaryPath);
                }
                catch
                {
                    // Preserve the original serialization, flush, or replacement failure.
                }
            }
        }
    }

    private void DeleteTemporaryFileIfPresent(string temporaryPath)
    {
        if (fileOperations.Exists(temporaryPath))
        {
            fileOperations.Delete(temporaryPath);
        }
    }

    private static JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private sealed record SettingsDocument(Guid ActiveProfileId, ProfileDocument[]? Profiles);

    private sealed record ProfileDocument(
        Guid Id,
        CompanionProviderKind Kind,
        string? DisplayName,
        string? BaseUrl,
        string? Model,
        string? CredentialTarget)
    {
        internal CompanionProfile ToProfile() => new(
            Id,
            Kind,
            DisplayName ?? throw new InvalidDataException("Profile display name is missing."),
            BaseUrl is null ? null : new Uri(BaseUrl, UriKind.Absolute),
            Model ?? throw new InvalidDataException("Profile model is missing."),
            CredentialTarget);

        internal static ProfileDocument FromProfile(CompanionProfile profile) => new(
            profile.Id,
            profile.Kind,
            profile.DisplayName,
            profile.BaseUrl?.AbsoluteUri,
            profile.Model,
            profile.CredentialTarget);
    }
}

internal sealed class CompanionSettingsFileOperations : ICompanionSettingsFileOperations
{
    public void MoveReplace(string sourcePath, string destinationPath) =>
        File.Move(sourcePath, destinationPath, overwrite: true);

    public bool Exists(string path) => File.Exists(path);

    public void Delete(string path) => File.Delete(path);
}

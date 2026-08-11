using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;

namespace Descartes.CertaintyLab;

public sealed record ProgressLoadResult(
    LearningProgress Progress,
    string? Diagnostic);

public sealed class LearningProgressStore
{
    private readonly string directory;
    private readonly IReadOnlyDictionary<string, string>
        currentAbilitySignatures;
    private readonly JsonSerializerOptions jsonOptions;

    public LearningProgressStore()
        : this(Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "PhilosophyVault",
            "learning-progress"))
    {
    }

    public LearningProgressStore(
        string directory,
        IReadOnlyDictionary<string, string>? currentAbilitySignatures = null)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ArgumentException(
                "进度目录不能为空。",
                nameof(directory));
        }

        this.directory = Path.GetFullPath(directory);
        this.currentAbilitySignatures =
            currentAbilitySignatures ??
            new Dictionary<string, string>(StringComparer.Ordinal);
        jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            WriteIndented = true,
        };
        jsonOptions.Converters.Add(
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    }

    public ProgressLoadResult Load(
        string routeId,
        string routeVersion)
    {
        ValidateRouteIdentity(routeId, routeVersion);
        string path = GetPath(routeId);
        if (!File.Exists(path))
        {
            return new ProgressLoadResult(
                LearningProgress.Empty(routeId, routeVersion),
                null);
        }

        try
        {
            LearningProgress stored =
                JsonSerializer.Deserialize<LearningProgress>(
                    File.ReadAllText(path, Encoding.UTF8),
                    jsonOptions) ??
                throw new InvalidDataException("进度文件为空。");
            ValidateStoredProgress(stored);
            if (!string.Equals(
                    stored.RouteId,
                    routeId,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "进度文件的路线身份不匹配。");
            }

            LearningProgress migrated = stored with
            {
                RouteVersion = routeVersion,
            };
            foreach ((string nodeId, NodeMastery mastery) in stored.Nodes)
            {
                if (currentAbilitySignatures.TryGetValue(
                        nodeId,
                        out string? currentSignature) &&
                    !string.Equals(
                        mastery.AbilitySignature,
                        currentSignature,
                        StringComparison.Ordinal))
                {
                    migrated = migrated.With(mastery with
                    {
                        State = mastery.State == MasteryState.Verified
                            ? MasteryState.Review
                            : mastery.State,
                        AbilitySignature = currentSignature,
                        UpdatedAt = DateTimeOffset.UtcNow,
                    });
                }
            }

            return new ProgressLoadResult(migrated, null);
        }
        catch (Exception exception) when (
            exception is JsonException or
            IOException or
            UnauthorizedAccessException or
            InvalidDataException)
        {
            return new ProgressLoadResult(
                LearningProgress.Empty(routeId, routeVersion),
                $"学习进度无法读取，已使用空进度：{exception.Message}");
        }
    }

    public void Save(LearningProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);
        ValidateRouteIdentity(progress.RouteId, progress.RouteVersion);
        Directory.CreateDirectory(directory);
        string targetPath = GetPath(progress.RouteId);
        string temporaryPath = targetPath + ".tmp";
        string json = JsonSerializer.Serialize(progress, jsonOptions);

        try
        {
            using (var stream = new FileStream(
                       temporaryPath,
                       FileMode.Create,
                       FileAccess.Write,
                       FileShare.None,
                       4096,
                       FileOptions.WriteThrough))
            using (var writer = new StreamWriter(
                       stream,
                       new UTF8Encoding(false)))
            {
                writer.Write(json);
                writer.WriteLine();
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(targetPath))
            {
                File.Replace(
                    temporaryPath,
                    targetPath,
                    destinationBackupFileName: null,
                    ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(temporaryPath, targetPath);
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public static string CreateAbilitySignature(
        IEnumerable<string> abilityIds)
    {
        ArgumentNullException.ThrowIfNull(abilityIds);
        string canonical = string.Join(
            "\n",
            abilityIds.Order(StringComparer.Ordinal));
        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private string GetPath(string routeId) =>
        Path.Combine(directory, routeId + ".json");

    private static void ValidateStoredProgress(
        LearningProgress stored)
    {
        if (string.IsNullOrWhiteSpace(stored.RouteId) ||
            string.IsNullOrWhiteSpace(stored.RouteVersion) ||
            stored.Nodes is null)
        {
            throw new InvalidDataException(
                "进度文件缺少必需字段。");
        }

        foreach ((string nodeId, NodeMastery mastery) in stored.Nodes)
        {
            if (string.IsNullOrWhiteSpace(nodeId) ||
                mastery is null ||
                string.IsNullOrWhiteSpace(mastery.NodeId) ||
                !string.Equals(
                    mastery.NodeId,
                    nodeId,
                    StringComparison.Ordinal) ||
                mastery.PassedCheckIds is null ||
                mastery.PassedCheckIds.Any(string.IsNullOrWhiteSpace) ||
                mastery.AbilitySignature is null ||
                mastery.TargetMisconceptionCount < 0 ||
                !Enum.IsDefined(mastery.State))
            {
                throw new InvalidDataException(
                    "进度文件包含损坏的节点记录。");
            }
        }
    }

    private static void ValidateRouteIdentity(
        string routeId,
        string routeVersion)
    {
        if (string.IsNullOrWhiteSpace(routeId) ||
            routeId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException("路线 ID 不是安全文件名。", nameof(routeId));
        }

        if (string.IsNullOrWhiteSpace(routeVersion))
        {
            throw new ArgumentException(
                "路线版本不能为空。",
                nameof(routeVersion));
        }
    }
}

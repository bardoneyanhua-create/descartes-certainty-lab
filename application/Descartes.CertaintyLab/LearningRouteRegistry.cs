using System.Collections.ObjectModel;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace Descartes.CertaintyLab;

public sealed record LearningRouteCatalogItem(
    string RouteId,
    string FileName,
    string Sha256,
    string Title,
    string Summary)
{
    public string ActionText => $"系统学习：{Title}";

    public string AutomationName =>
        $"{ActionText}。{Summary}。所有章节都可以直接打开。";
}

public sealed class LearningRouteRegistry
{
    private readonly IReadOnlyDictionary<string, LearningRouteCatalogItem> routesById;

    private LearningRouteRegistry(IReadOnlyList<LearningRouteCatalogItem> routes)
    {
        Routes = routes;
        routesById = routes.ToDictionary(route => route.RouteId, StringComparer.Ordinal);
    }

    public IReadOnlyList<LearningRouteCatalogItem> Routes { get; }

    public static LearningRouteRegistry Load(string contentDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentDirectory);
        string registryPath = Path.Combine(contentDirectory, "learning-route-registry.json");
        using FileStream registryStream = File.OpenRead(registryPath);
        RegistryFile? source = JsonSerializer.Deserialize<RegistryFile>(
            registryStream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (source is null || source.SchemaVersion != "1.0" ||
            source.Entries is null || source.RouteCount != source.Entries.Count)
        {
            throw new InvalidDataException("学习路线注册表不完整。");
        }

        var routes = new List<LearningRouteCatalogItem>(source.Entries.Count);
        var routeIds = new HashSet<string>(StringComparer.Ordinal);
        var fileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (RegistryEntry entry in source.Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.RouteId) ||
                string.IsNullOrWhiteSpace(entry.FileName) ||
                string.IsNullOrWhiteSpace(entry.Sha256) ||
                !routeIds.Add(entry.RouteId) || !fileNames.Add(entry.FileName))
            {
                throw new InvalidDataException("学习路线注册表包含空值或重复项。");
            }

            string packPath = Path.GetFullPath(Path.Combine(contentDirectory, entry.FileName));
            string expectedRoot = Path.GetFullPath(contentDirectory) + Path.DirectorySeparatorChar;
            if (!packPath.StartsWith(expectedRoot, StringComparison.OrdinalIgnoreCase) ||
                !File.Exists(packPath))
            {
                throw new InvalidDataException($"学习路线文件不可用：{entry.FileName}");
            }

            string actualSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(packPath)));
            if (!string.Equals(actualSha256, entry.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"学习路线文件身份不匹配：{entry.FileName}");
            }

            (string title, string summary) = ReadRoutePresentation(packPath, entry.RouteId);
            routes.Add(new LearningRouteCatalogItem(
                entry.RouteId, entry.FileName, actualSha256, title, summary));
        }

        return new LearningRouteRegistry(
            new ReadOnlyCollection<LearningRouteCatalogItem>(routes));
    }

    public bool TryResolve(string routeId, out LearningRouteCatalogItem? route) =>
        routesById.TryGetValue(routeId, out route);

    public LearningRouteCatalogItem Resolve(string routeId) =>
        routesById.TryGetValue(routeId, out LearningRouteCatalogItem? route)
            ? route
            : throw new KeyNotFoundException($"未知的哲学学习路线“{routeId}”。");

    private static (string Title, string Summary) ReadRoutePresentation(
        string packPath,
        string routeId)
    {
        using FileStream stream = File.OpenRead(packPath);
        using JsonDocument document = JsonDocument.Parse(stream);
        if (!document.RootElement.TryGetProperty("routes", out JsonElement routes) ||
            routes.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException($"学习包缺少路线集合：{Path.GetFileName(packPath)}");
        }

        foreach (JsonElement route in routes.EnumerateArray())
        {
            if (!route.TryGetProperty("id", out JsonElement id) ||
                !string.Equals(id.GetString(), routeId, StringComparison.Ordinal))
            {
                continue;
            }

            string? title = route.TryGetProperty("title", out JsonElement titleValue)
                ? titleValue.GetString() : null;
            string? summary = route.TryGetProperty("summary", out JsonElement summaryValue)
                ? summaryValue.GetString() : null;
            if (string.IsNullOrWhiteSpace(title))
            {
                throw new InvalidDataException($"学习路线缺少标题：{routeId}");
            }

            return (
                title.Trim(),
                string.IsNullOrWhiteSpace(summary)
                    ? "沿固定路线连续阅读、练习并保存本地进度"
                    : summary.Trim());
        }

        throw new InvalidDataException($"注册路线未出现在学习包中：{routeId}");
    }

    private sealed class RegistryFile
    {
        public string? SchemaVersion { get; init; }
        public int RouteCount { get; init; }
        public List<RegistryEntry>? Entries { get; init; }
    }

    private sealed class RegistryEntry
    {
        public string? RouteId { get; init; }
        public string? FileName { get; init; }
        public string? Sha256 { get; init; }
    }
}

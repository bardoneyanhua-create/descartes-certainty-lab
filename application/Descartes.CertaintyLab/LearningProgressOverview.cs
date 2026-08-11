using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;

namespace Descartes.CertaintyLab;

public sealed record LearningProgressRouteItem(
    string RouteId,
    string RouteTitle,
    string LessonId,
    string LessonTitle,
    MasteryState State,
    DateTimeOffset UpdatedAt)
{
    public string ActionText => $"继续学习：{RouteTitle}";

    public string DetailText =>
        $"最近章节：{LessonTitle}。{StateText}。";

    public string AutomationName =>
        $"{ActionText}。{DetailText}按回车继续精确章节。";

    private string StateText => State switch
    {
        MasteryState.Verified => "已验证理解",
        MasteryState.Review => "待复习",
        MasteryState.Read => "进行中",
        _ => "未开始",
    };
}

public sealed class LearningProgressOverview
{
    private LearningProgressOverview(
        IReadOnlyList<LearningProgressRouteItem> items,
        string? diagnostic)
    {
        Items = items;
        Diagnostic = diagnostic;
    }

    public IReadOnlyList<LearningProgressRouteItem> Items { get; }

    public string? Diagnostic { get; }

    public static LearningProgressOverview Load(
        string contentDirectory,
        string progressDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(progressDirectory);

        LearningRouteRegistry registry =
            LearningRouteRegistry.Load(contentDirectory);
        var items = new List<LearningProgressRouteItem>();
        var diagnostics = new List<string>();

        foreach (LearningRouteCatalogItem registration in registry.Routes)
        {
            try
            {
                string packPath = Path.Combine(
                    contentDirectory,
                    registration.FileName);
                LearningPack pack;
                using (FileStream stream = File.OpenRead(packPath))
                {
                    pack = LearningPack.Load(stream);
                }

                LearningRouteDefinition route =
                    pack.GetRoute(registration.RouteId);
                IReadOnlyDictionary<string, string> signatures =
                    pack.Nodes.ToDictionary(
                        node => node.Id,
                        node => LearningProgressStore.CreateAbilitySignature(
                            node.AbilityIds),
                        StringComparer.Ordinal);
                var store = new LearningProgressStore(
                    progressDirectory,
                    signatures);
                ProgressLoadResult load = store.Load(
                    route.Id,
                    route.Version);
                if (!string.IsNullOrWhiteSpace(load.Diagnostic))
                {
                    diagnostics.Add(
                        $"{registration.Title}：{load.Diagnostic}");
                }

                NodeMastery? recent = load.Progress.Nodes.Values
                    .Where(node =>
                        node.State != MasteryState.NotStarted &&
                        node.UpdatedAt != DateTimeOffset.MinValue)
                    .OrderByDescending(node => node.UpdatedAt)
                    .ThenBy(node => node.NodeId, StringComparer.Ordinal)
                    .FirstOrDefault();
                if (recent is null)
                {
                    continue;
                }

                LessonDefinition? lesson = route.LessonIds
                    .Select(pack.GetLesson)
                    .FirstOrDefault(candidate =>
                        candidate.NodeIds.Contains(
                            recent.NodeId,
                            StringComparer.Ordinal));
                if (lesson is null)
                {
                    diagnostics.Add(
                        $"{registration.Title}：最近进度无法对应当前章节，已忽略。");
                    continue;
                }

                items.Add(new LearningProgressRouteItem(
                    route.Id,
                    registration.Title,
                    lesson.Id,
                    lesson.Title,
                    recent.State,
                    recent.UpdatedAt));
            }
            catch (Exception exception) when (
                exception is IOException or
                UnauthorizedAccessException or
                InvalidDataException or
                JsonException)
            {
                diagnostics.Add(
                    $"{registration.Title}：进度索引无法读取，已忽略。{exception.Message}");
            }
        }

        return new LearningProgressOverview(
            new ReadOnlyCollection<LearningProgressRouteItem>(items
                .OrderByDescending(item => item.UpdatedAt)
                .ThenBy(item => item.RouteTitle, StringComparer.CurrentCulture)
                .ToList()),
            diagnostics.Count == 0
                ? null
                : string.Join(" ", diagnostics));
    }
}

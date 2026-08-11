using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Descartes.CertaintyLab;

public enum KnowledgeCategory
{
    All,
    Person,
    Question,
    Work,
    Theme,
    Comparison,
    Learning
}

public sealed record KnowledgeEntry(
    string Id,
    int Order,
    KnowledgeCategory Category,
    string Title,
    string OriginalName,
    string Summary,
    string ReaderState,
    int RecordCount,
    IReadOnlyList<string> Keywords,
    string? ExperienceId,
    string? LearningRouteId,
    string? InclusionStatus,
    string? SourceConfidence,
    string? ReviewNote,
    IReadOnlyList<KnowledgeReviewOnlyFragment> ReviewOnlyFragments,
    KnowledgeProfile? Profile);

public sealed record KnowledgeReviewOnlyFragment(
    string Text,
    string BatchId,
    IReadOnlyList<string> SourcePaths,
    string InclusionStatus)
{
    public bool IsLoadBearing => false;
}

public sealed record KnowledgeProfile(
    string Positioning,
    string Interpretation,
    string LifeAndThought,
    IReadOnlyList<string> Questions,
    IReadOnlyList<string> KeyIdeas,
    IReadOnlyList<string> Cautions,
    IReadOnlyList<string> Works,
    string RelationText,
    string BoundaryNote,
    string SourceNote,
    string ExperienceTitle);

public sealed record KnowledgeBrowseItem(
    KnowledgeEntry Entry,
    KnowledgeCategory Category,
    string Title,
    string Description);

public sealed record KnowledgeSearchResult(
    KnowledgeEntry Entry,
    KnowledgeCategory BrowseCategory,
    string DisplayTitle,
    string DisplaySummary,
    string MatchReason,
    int Score);

public sealed class KnowledgeCatalog
{
    private const string FriendlyLoadError =
        "知识目录现在无法完整打开。你仍可以返回思想体验。";

    private readonly IReadOnlyDictionary<string, KnowledgeEntry> _entriesById;
    private readonly IReadOnlyDictionary<KnowledgeCategory, IReadOnlyList<KnowledgeBrowseItem>>
        _browseItems;

    private KnowledgeCatalog(
        string readerMessage,
        IReadOnlyList<KnowledgeEntry> entries)
    {
        ReaderMessage = readerMessage;
        AllEntries = entries;
        _entriesById = entries.ToDictionary(
            entry => entry.Id,
            StringComparer.Ordinal);
        _browseItems = BuildBrowseItems(entries);
    }

    public string ReaderMessage { get; }

    public IReadOnlyList<KnowledgeEntry> AllEntries { get; }

    public static KnowledgeCatalog CreateUnavailable() =>
        new(
            FriendlyLoadError,
            Array.Empty<KnowledgeEntry>());

    public static KnowledgeCatalog Load(string path)
    {
        try
        {
            string json = File.ReadAllText(path);
            ReaderCatalogFile? file = JsonSerializer.Deserialize<ReaderCatalogFile>(
                json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            if (file is null ||
                file.SchemaVersion != "1.0" ||
                file.CatalogNotice is null ||
                string.IsNullOrWhiteSpace(file.CatalogNotice.ReaderMessage) ||
                file.Entries is null ||
                file.Entries.Count == 0)
            {
                throw new InvalidDataException();
            }

            var entries = new List<KnowledgeEntry>(file.Entries.Count);
            for (int index = 0; index < file.Entries.Count; index++)
            {
                ReaderEntryFile source = file.Entries[index];
                int expectedOrder = index + 1;
                if (source.Order != expectedOrder ||
                    string.IsNullOrWhiteSpace(source.Id) ||
                    string.IsNullOrWhiteSpace(source.Title) ||
                    string.IsNullOrWhiteSpace(source.Summary) ||
                    !TryParseCategory(source.Category, out KnowledgeCategory category))
                {
                    throw new InvalidDataException();
                }

                entries.Add(new KnowledgeEntry(
                    source.Id,
                    source.Order,
                    category,
                    source.Title.Trim(),
                    source.OriginalName?.Trim() ?? string.Empty,
                    source.Summary.Trim(),
                    source.ReaderState?.Trim() ?? "细节仍需核对",
                    source.RecordCount,
                    new ReadOnlyCollection<string>(
                        (source.Keywords ?? [])
                            .Where(value => !string.IsNullOrWhiteSpace(value))
                            .Select(value => value.Trim())
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList()),
                    string.IsNullOrWhiteSpace(source.ExperienceId)
                        ? null
                        : source.ExperienceId.Trim(),
                    string.IsNullOrWhiteSpace(source.LearningRouteId)
                        ? null
                        : source.LearningRouteId.Trim(),
                    string.IsNullOrWhiteSpace(source.InclusionStatus)
                        ? null
                        : source.InclusionStatus.Trim(),
                    string.IsNullOrWhiteSpace(source.SourceConfidence)
                        ? null
                        : source.SourceConfidence.Trim(),
                    string.IsNullOrWhiteSpace(source.ReviewNote)
                        ? null
                        : source.ReviewNote.Trim(),
                    new ReadOnlyCollection<KnowledgeReviewOnlyFragment>(
                        (source.ReviewOnlyFragments ?? [])
                            .Select(fragment => new KnowledgeReviewOnlyFragment(
                                fragment.Text?.Trim() ?? string.Empty,
                                fragment.BatchId?.Trim() ?? string.Empty,
                                new ReadOnlyCollection<string>(
                                    (fragment.SourcePaths ?? [])
                                        .Where(value => !string.IsNullOrWhiteSpace(value))
                                        .Select(value => value.Trim())
                                        .Distinct(StringComparer.Ordinal)
                                        .ToList()),
                                fragment.InclusionStatus?.Trim() ?? string.Empty))
                            .ToList()),
                    source.Profile is null
                        ? null
                        : new KnowledgeProfile(
                            source.Profile.Positioning?.Trim() ?? string.Empty,
                            source.Profile.Interpretation?.Trim() ?? string.Empty,
                            source.Profile.LifeAndThought?.Trim() ?? string.Empty,
                            new ReadOnlyCollection<string>(
                                (source.Profile.Questions ?? [])
                                    .Where(value => !string.IsNullOrWhiteSpace(value))
                                    .Select(value => value.Trim())
                                    .ToList()),
                            new ReadOnlyCollection<string>(
                                (source.Profile.KeyIdeas ?? [])
                                    .Where(value => !string.IsNullOrWhiteSpace(value))
                                    .Select(value => value.Trim())
                                    .ToList()),
                            new ReadOnlyCollection<string>(
                                (source.Profile.Cautions ?? [])
                                    .Where(value => !string.IsNullOrWhiteSpace(value))
                                    .Select(value => value.Trim())
                                    .ToList()),
                            new ReadOnlyCollection<string>(
                                (source.Profile.Works ?? [])
                                    .Where(value => !string.IsNullOrWhiteSpace(value))
                                    .Select(value => value.Trim())
                                    .ToList()),
                            source.Profile.RelationText?.Trim() ?? string.Empty,
                            source.Profile.BoundaryNote?.Trim() ?? string.Empty,
                            source.Profile.SourceNote?.Trim() ?? string.Empty,
                            source.Profile.ExperienceTitle?.Trim() ?? string.Empty)));
            }

            if (entries.Select(entry => entry.Id)
                .Distinct(StringComparer.Ordinal)
                .Count() != entries.Count)
            {
                throw new InvalidDataException();
            }

            string[] mappedRouteIds = entries
                .Where(entry => entry.LearningRouteId is not null)
                .Select(entry => entry.LearningRouteId!)
                .ToArray();
            if (mappedRouteIds.Distinct(StringComparer.Ordinal).Count() != mappedRouteIds.Length)
            {
                throw new InvalidDataException();
            }

            const string reviewOnlyStatus = "UNVERIFIED_REVIEW_ONLY_NON_LOAD_BEARING";
            if (entries.Any(entry => entry.ReviewOnlyFragments.Any(fragment =>
                    string.IsNullOrWhiteSpace(fragment.Text) ||
                    string.IsNullOrWhiteSpace(fragment.BatchId) ||
                    fragment.SourcePaths.Count == 0 ||
                    fragment.InclusionStatus != reviewOnlyStatus)) ||
                entries.Any(entry => entry.ReviewOnlyFragments.Count > 0 &&
                    (entry.InclusionStatus != reviewOnlyStatus ||
                     string.IsNullOrWhiteSpace(entry.SourceConfidence) ||
                     string.IsNullOrWhiteSpace(entry.ReviewNote))))
            {
                throw new InvalidDataException();
            }

            return new KnowledgeCatalog(
                file.CatalogNotice.ReaderMessage.Trim(),
                new ReadOnlyCollection<KnowledgeEntry>(entries));
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            JsonException or
            InvalidDataException)
        {
            throw new InvalidDataException(FriendlyLoadError);
        }
    }

    public KnowledgeEntry GetById(string id)
    {
        if (_entriesById.TryGetValue(id, out KnowledgeEntry? entry))
        {
            return entry;
        }

        throw new KeyNotFoundException("没有找到这条知识线索。");
    }

    public IReadOnlyList<KnowledgeBrowseItem> Browse(KnowledgeCategory category)
    {
        if (category == KnowledgeCategory.All)
        {
            return AllEntries
                .Select(entry => new KnowledgeBrowseItem(
                    entry,
                    KnowledgeCategory.All,
                    entry.Title,
                    entry.Summary))
                .ToList()
                .AsReadOnly();
        }

        return _browseItems.TryGetValue(
            category,
            out IReadOnlyList<KnowledgeBrowseItem>? items)
                ? items
                : Array.Empty<KnowledgeBrowseItem>();
    }

    public IReadOnlyList<KnowledgeSearchResult> Search(
        string? query,
        KnowledgeCategory category = KnowledgeCategory.All)
    {
        string normalizedQuery = Normalize(query);
        IReadOnlyList<KnowledgeBrowseItem> candidates = Browse(category);

        if (normalizedQuery.Length == 0)
        {
            return candidates
                .Select(item => new KnowledgeSearchResult(
                    item.Entry,
                    item.Category,
                    item.Title,
                    item.Description,
                    item.Description,
                    0))
                .ToList()
                .AsReadOnly();
        }

        var matches = new List<KnowledgeSearchResult>();
        foreach (KnowledgeBrowseItem item in candidates)
        {
            KnowledgeEntry entry = item.Entry;
            var searchable = new List<(string Value, string Reason, int Weight)>
            {
                (
                    item.Title,
                    $"{CategoryLabel(item.Category)}“{item.Title}”与搜索内容直接相关",
                    100),
                (
                    item.Description,
                    $"{CategoryLabel(item.Category)}“{item.Title}”回应了这次搜索",
                    65),
                (
                    entry.Title,
                    $"这条线索来自{entry.Title}的知识正文",
                    75),
                (
                    entry.OriginalName,
                    $"名称与{entry.Title}相关",
                    70)
            };
            if (category == KnowledgeCategory.All)
            {
                searchable.Add((entry.Summary, $"内容说明回应了{entry.Title}这条线索", 50));
                searchable.AddRange(entry.Keywords.Select(
                    keyword => (
                        keyword,
                        $"资料中包含与“{query?.Trim()}”相近的思想线索",
                        70)));
                if (entry.Profile is not null)
                {
                    searchable.AddRange(entry.Profile.Questions.Select(
                        question => (
                            question,
                            $"{entry.Title}追问了与搜索内容相关的问题",
                            85)));
                    searchable.AddRange(entry.Profile.KeyIdeas.Select(
                        idea => (
                            idea,
                            $"{entry.Title}的思想线索回应了这次搜索",
                            80)));
                    searchable.AddRange(entry.Profile.Works.Select(
                        work => (
                            work,
                            $"{entry.Title}的相关作品与搜索内容相符",
                            90)));
                }
            }

            (string Value, string Reason, int Weight)? best = searchable
                .Select(item => (
                    item.Value,
                    item.Reason,
                    Weight: MatchWeight(
                        normalizedQuery,
                        Normalize(item.Value),
                        item.Weight)))
                .Where(item => item.Weight >= 0)
                .OrderByDescending(item => item.Weight)
                .ThenBy(item => item.Value.Length)
                .Cast<(string Value, string Reason, int Weight)?>()
                .FirstOrDefault();
            if (best is not null)
            {
                matches.Add(new KnowledgeSearchResult(
                    entry,
                    item.Category,
                    item.Title,
                    item.Description,
                    best.Value.Reason,
                    best.Value.Weight));
            }
        }

        return matches
            .OrderByDescending(match => match.Score)
            .ThenBy(match => match.Entry.Order)
            .ToList()
            .AsReadOnly();
    }

    private static IReadOnlyDictionary<
        KnowledgeCategory,
        IReadOnlyList<KnowledgeBrowseItem>> BuildBrowseItems(
            IReadOnlyList<KnowledgeEntry> entries)
    {
        var people = new List<KnowledgeBrowseItem>();
        var questions = new List<KnowledgeBrowseItem>();
        var works = new List<KnowledgeBrowseItem>();
        var themes = new List<KnowledgeBrowseItem>();

        foreach (KnowledgeEntry entry in entries)
        {
            if (entry.Category == KnowledgeCategory.Person)
            {
                people.Add(new KnowledgeBrowseItem(
                    entry,
                    KnowledgeCategory.Person,
                    entry.Title,
                    entry.Summary));
            }

            KnowledgeProfile? profile = entry.Profile;
            if (profile is null)
            {
                continue;
            }

            IEnumerable<string> questionCandidates = profile.Questions.Concat(
                profile.KeyIdeas.Where(IsQuestion));
            questions.AddRange(CreateItems(
                entry,
                KnowledgeCategory.Question,
                questionCandidates,
                value => IsNaturalChinese(value) && IsQuestion(value),
                $"这是{entry.Title}持续追问的一个问题。"));

            works.AddRange(CreateItems(
                entry,
                KnowledgeCategory.Work,
                profile.Works,
                IsReadableWorkTitle,
                $"这部作品与{entry.Title}的思想线索相连。"));

            themes.AddRange(CreateItems(
                entry,
                KnowledgeCategory.Theme,
                profile.KeyIdeas,
                value =>
                    IsNaturalChinese(value) &&
                    !IsQuestion(value) &&
                    value.Length <= 120,
                $"这是理解{entry.Title}时值得继续展开的一条思想。"));
        }

        return new Dictionary<KnowledgeCategory, IReadOnlyList<KnowledgeBrowseItem>>
        {
            [KnowledgeCategory.Person] = DistinctItems(people),
            [KnowledgeCategory.Question] = DistinctItems(questions),
            [KnowledgeCategory.Work] = DistinctItems(works),
            [KnowledgeCategory.Theme] = DistinctItems(themes)
        };
    }

    private static IEnumerable<KnowledgeBrowseItem> CreateItems(
        KnowledgeEntry entry,
        KnowledgeCategory category,
        IEnumerable<string> values,
        Func<string, bool> include,
        string description) =>
        values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Where(include)
            .Select(value => new KnowledgeBrowseItem(
                entry,
                category,
                value,
                description));

    private static IReadOnlyList<KnowledgeBrowseItem> DistinctItems(
        IEnumerable<KnowledgeBrowseItem> items) =>
        items
            .GroupBy(
                item => Normalize(item.Title),
                StringComparer.Ordinal)
            .Select(group => group
                .OrderBy(item => item.Entry.Order)
                .First())
            .OrderBy(item => item.Entry.Order)
            .ThenBy(item => item.Title, StringComparer.CurrentCulture)
            .ToList()
            .AsReadOnly();

    private static bool IsQuestion(string value) =>
        value.TrimEnd().EndsWith('？') ||
        value.TrimEnd().EndsWith('?');

    private static bool IsNaturalChinese(string value) =>
        value.Any(character => character is >= '\u3400' and <= '\u9fff') &&
        !value.Contains("候选命题", StringComparison.Ordinal) &&
        !value.Contains("迁移前", StringComparison.Ordinal) &&
        !value.Contains("not-reviewed", StringComparison.OrdinalIgnoreCase);

    private static bool IsReadableWorkTitle(string value)
    {
        string title = value.Trim();
        if (title.Length is < 2 or > 120 ||
            title.Contains("publication component", StringComparison.OrdinalIgnoreCase) ||
            title.Contains("event record", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !Regex.IsMatch(
            title,
            "^[A-Z]{1,8}-[A-Z0-9-]+$",
            RegexOptions.CultureInvariant);
    }

    private static string CategoryLabel(KnowledgeCategory category) =>
        category switch
        {
            KnowledgeCategory.Person => "人物",
            KnowledgeCategory.Question => "问题",
            KnowledgeCategory.Work => "作品",
            KnowledgeCategory.Theme => "思想主题",
            _ => "知识线索"
        };

    private static bool IsRelated(string query, string value)
    {
        if (value.Length == 0)
        {
            return false;
        }

        return query.Contains(value, StringComparison.OrdinalIgnoreCase) ||
               value.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private static int MatchWeight(string query, string value, int baseWeight)
    {
        if (!IsRelated(query, value))
        {
            return -1;
        }

        if (query.Equals(value, StringComparison.OrdinalIgnoreCase))
        {
            return baseWeight + 40;
        }

        if (value.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return baseWeight + 20;
        }

        return baseWeight + Math.Min(15, value.Length);
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return new string(value
            .Where(character =>
                !char.IsWhiteSpace(character) &&
                !char.IsPunctuation(character))
            .ToArray())
            .ToLowerInvariant();
    }

    private static bool TryParseCategory(
        string? value,
        out KnowledgeCategory category)
    {
        category = value switch
        {
            "person" => KnowledgeCategory.Person,
            "question" => KnowledgeCategory.Question,
            "comparison" => KnowledgeCategory.Comparison,
            "learning" => KnowledgeCategory.Learning,
            _ => KnowledgeCategory.All
        };
        return category != KnowledgeCategory.All;
    }

    private sealed class ReaderCatalogFile
    {
        public string? SchemaVersion { get; init; }
        public ReaderNoticeFile? CatalogNotice { get; init; }
        public List<ReaderEntryFile>? Entries { get; init; }
    }

    private sealed class ReaderNoticeFile
    {
        public string? ReaderMessage { get; init; }
    }

    private sealed class ReaderEntryFile
    {
        public string? Id { get; init; }
        public int Order { get; init; }
        public string? Category { get; init; }
        public string? Title { get; init; }
        public string? OriginalName { get; init; }
        public string? Summary { get; init; }
        public string? ReaderState { get; init; }
        public int RecordCount { get; init; }
        public List<string>? Keywords { get; init; }
        public string? ExperienceId { get; init; }
        public string? LearningRouteId { get; init; }
        public string? InclusionStatus { get; init; }
        public string? SourceConfidence { get; init; }
        public string? ReviewNote { get; init; }
        public List<ReaderReviewOnlyFragmentFile>? ReviewOnlyFragments { get; init; }
        public ReaderProfileFile? Profile { get; init; }
    }

    private sealed class ReaderReviewOnlyFragmentFile
    {
        public string? Text { get; init; }
        public string? BatchId { get; init; }
        public List<string>? SourcePaths { get; init; }
        public string? InclusionStatus { get; init; }
    }

    private sealed class ReaderProfileFile
    {
        public string? Positioning { get; init; }
        public string? Interpretation { get; init; }
        public string? LifeAndThought { get; init; }
        public List<string>? Questions { get; init; }
        public List<string>? KeyIdeas { get; init; }
        public List<string>? Cautions { get; init; }
        public List<string>? Works { get; init; }
        public string? RelationText { get; init; }
        public string? BoundaryNote { get; init; }
        public string? SourceNote { get; init; }
        public string? ExperienceTitle { get; init; }
    }
}

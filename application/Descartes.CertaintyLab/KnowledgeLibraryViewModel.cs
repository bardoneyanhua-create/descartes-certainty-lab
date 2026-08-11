using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace Descartes.CertaintyLab;

public sealed class KnowledgeLibraryViewModel : INotifyPropertyChanged
{
    private readonly KnowledgeCatalog catalog;
    private string query = string.Empty;
    private KnowledgeCategoryChoice selectedCategory;
    private IReadOnlyList<KnowledgeResultViewModel> results = [];
    private KnowledgeResultViewModel? selectedResult;
    private KnowledgeDetailViewModel detail = KnowledgeDetailViewModel.Empty;

    public KnowledgeLibraryViewModel(KnowledgeCatalog catalog)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        Categories =
        [
            new(KnowledgeCategory.All, "全部线索"),
            new(KnowledgeCategory.Person, "人物"),
            new(KnowledgeCategory.Question, "问题"),
            new(KnowledgeCategory.Work, "作品"),
            new(KnowledgeCategory.Theme, "思想主题")
        ];
        selectedCategory = Categories[0];
        SearchCommand = new RelayCommand(_ => RunSearch());
        ShowAllCommand = new RelayCommand(_ => ShowAll());
        RunSearch();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Title => "知识库";

    public string PageIntroduction =>
        "从一个问题开始，也可以沿人物、作品和思想之间的联系慢慢探索。";

    public string CatalogNotice => catalog.ReaderMessage;

    public IReadOnlyList<KnowledgeCategoryChoice> Categories { get; }

    public ICommand SearchCommand { get; }

    public ICommand ShowAllCommand { get; }

    public string Query
    {
        get => query;
        set
        {
            if (query == value)
            {
                return;
            }

            query = value ?? string.Empty;
            OnPropertyChanged();
        }
    }

    public KnowledgeCategoryChoice SelectedCategory
    {
        get => selectedCategory;
        set
        {
            if (value is null || ReferenceEquals(selectedCategory, value))
            {
                return;
            }

            selectedCategory = value;
            OnPropertyChanged();
            RunSearch();
        }
    }

    public IReadOnlyList<KnowledgeResultViewModel> Results
    {
        get => results;
        private set
        {
            results = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShowEmptyState));
            OnPropertyChanged(nameof(HasResults));
        }
    }

    public KnowledgeResultViewModel? SelectedResult
    {
        get => selectedResult;
        set
        {
            if (ReferenceEquals(selectedResult, value))
            {
                return;
            }

            selectedResult = value;
            Detail = value is null
                ? KnowledgeDetailViewModel.Empty
                : KnowledgeDetailViewModel.From(value.Entry);
            OnPropertyChanged();
        }
    }

    public KnowledgeDetailViewModel Detail
    {
        get => detail;
        private set
        {
            detail = value;
            OnPropertyChanged();
        }
    }

    public string ResultSummary { get; private set; } = string.Empty;

    public bool ShowEmptyState => Results.Count == 0;

    public bool HasResults => Results.Count > 0;

    public string EmptyStateText =>
        "暂时没有找到与这句话直接相关的内容。可以换一种说法，或浏览全部线索。";

    public void SelectById(string id)
    {
        KnowledgeEntry entry = catalog.GetById(id);
        KnowledgeResultViewModel? existing =
            Results.FirstOrDefault(result => result.Entry.Id == id);
        SelectedResult = existing ??
            new KnowledgeResultViewModel(
                entry,
                KnowledgeCategory.All,
                entry.Title,
                entry.Summary,
                "从完整目录打开这条线索");
    }

    private void RunSearch()
    {
        IReadOnlyList<KnowledgeSearchResult> matches =
            catalog.Search(Query, SelectedCategory.Category);
        Results = matches
            .Select(match => new KnowledgeResultViewModel(
                match.Entry,
                match.BrowseCategory,
                match.DisplayTitle,
                match.DisplaySummary,
                match.MatchReason))
            .ToList()
            .AsReadOnly();

        ResultSummary =
            string.IsNullOrWhiteSpace(Query) &&
            SelectedCategory.Category == KnowledgeCategory.All
                ? catalog.AllEntries.Count == 0
                    ? "知识目录现在无法完整打开。"
                    : $"完整目录共有 {catalog.AllEntries.Count} 条线索。"
                : Results.Count == 0
                    ? "暂时没有找到直接相关的线索。"
                    : $"找到 {Results.Count} 条相关线索。";
        OnPropertyChanged(nameof(ResultSummary));
        SelectedResult = Results.FirstOrDefault();
    }

    private void ShowAll()
    {
        bool queryChanged = query.Length > 0;
        bool categoryChanged = !ReferenceEquals(selectedCategory, Categories[0]);
        query = string.Empty;
        selectedCategory = Categories[0];
        if (queryChanged)
        {
            OnPropertyChanged(nameof(Query));
        }
        if (categoryChanged)
        {
            OnPropertyChanged(nameof(SelectedCategory));
        }
        RunSearch();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed record KnowledgeCategoryChoice(
    KnowledgeCategory Category,
    string Label)
{
    public override string ToString() => Label;
}

public sealed record KnowledgeResultViewModel(
    KnowledgeEntry Entry,
    KnowledgeCategory BrowseCategory,
    string Title,
    string Summary,
    string WhyItAppears)
{
    public string AutomationName =>
        $"{Title}。{WhyItAppears}。按回车阅读{Entry.Title}的完整正文。";
}

public sealed record KnowledgeDetailViewModel(
    string Id,
    string Title,
    string OriginalName,
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
    string? ExperienceId,
    string? LearningRouteId,
    string ExperienceTitle,
    string? InclusionStatus,
    string? SourceConfidence,
    string? ReviewNote,
    IReadOnlyList<KnowledgeReviewOnlyFragment> ReviewOnlyFragments)
{
    public static KnowledgeDetailViewModel Empty { get; } = new(
        string.Empty,
        "请选择一条线索",
        string.Empty,
        "左侧选择后，这里会直接展开阅读，不会再弹出一层资料卡。",
        string.Empty,
        string.Empty,
        [],
        [],
        [],
        [],
        string.Empty,
        string.Empty,
        string.Empty,
        null,
        null,
        string.Empty,
        null,
        null,
        null,
        []);

    public bool ShowExperience => ExperienceId is not null;

    public bool ShowSystemLearning => LearningRouteId is not null;

    public bool ShowLifeAndThought => !string.IsNullOrWhiteSpace(LifeAndThought);

    public bool ShowWorks => Works.Count > 0;

    public string ReadingText
    {
        get
        {
            var sections = new List<string>
            {
                Title,
                OriginalName,
                Positioning
            };

            if (Questions.Count > 0)
            {
                sections.Add(
                    "核心问题\n" +
                    string.Join("\n", Questions.Select(question => $"• {question}")));
            }

            if (KeyIdeas.Count > 0)
            {
                sections.Add(
                    "关键思想\n" +
                    string.Join("\n", KeyIdeas.Select(idea => $"• {idea}")));
            }
            else if (!string.IsNullOrWhiteSpace(Interpretation))
            {
                sections.Add($"关键思想\n{Interpretation}");
            }

            if (Cautions.Count > 0)
            {
                sections.Add(
                    "争议与容易误解之处\n" +
                    string.Join("\n", Cautions.Select(caution => $"• {caution}")));
            }

            if (ShowLifeAndThought)
            {
                sections.Add($"人生与思想\n{LifeAndThought}");
            }

            if (ShowWorks)
            {
                sections.Add(
                    "相关作品\n" +
                    string.Join("\n", Works.Select(work => $"• {work}")));
            }

            if (!string.IsNullOrWhiteSpace(RelationText))
            {
                sections.Add($"思想怎样相连\n{RelationText}");
            }

            if (!string.IsNullOrWhiteSpace(BoundaryNote))
            {
                sections.Add($"阅读说明\n{BoundaryNote}");
            }

            if (!string.IsNullOrWhiteSpace(SourceNote))
            {
                sections.Add(SourceNote);
            }

            if (ReviewOnlyFragments.Count > 0)
            {
                sections.Add(
                    $"复审隔离材料（{InclusionStatus}）\n" +
                    $"来源置信：{SourceConfidence}\n" +
                    $"复审说明：{ReviewNote}\n" +
                    string.Join("\n", ReviewOnlyFragments.Select(fragment =>
                        $"• {fragment.Text}（{fragment.BatchId}；{string.Join(", ", fragment.SourcePaths)}）")));
            }

            return string.Join(
                Environment.NewLine + Environment.NewLine,
                sections.Where(section => !string.IsNullOrWhiteSpace(section)));
        }
    }

    public string ExperienceActionText =>
        ShowExperience
            ? $"亲自走一遍：{ExperienceTitle}"
            : string.Empty;

    public string SystemLearningActionText =>
        ShowSystemLearning
            ? $"系统学习：{Title}"
            : string.Empty;

    public static KnowledgeDetailViewModel From(KnowledgeEntry entry)
    {
        KnowledgeProfile? profile = entry.Profile;
        return profile is null
            ? CreateGeneric(entry)
            : new KnowledgeDetailViewModel(
                entry.Id,
                entry.Title,
                entry.OriginalName,
                profile.Positioning,
                profile.Interpretation,
                profile.LifeAndThought,
                profile.Questions,
                profile.KeyIdeas,
                profile.Cautions,
                profile.Works,
                profile.RelationText,
                profile.BoundaryNote,
                profile.SourceNote,
                entry.ExperienceId,
                entry.LearningRouteId,
                profile.ExperienceTitle,
                entry.InclusionStatus,
                entry.SourceConfidence,
                entry.ReviewNote,
                entry.ReviewOnlyFragments);
    }

    private static KnowledgeDetailViewModel CreateGeneric(KnowledgeEntry entry) =>
        new(
            entry.Id,
            entry.Title,
            entry.OriginalName,
            string.Empty,
            entry.Summary,
            string.Empty,
            [],
            [entry.Summary],
            [],
            [],
            string.Empty,
            "这条资料尚未生成完整的学习正文。",
            string.Empty,
            entry.ExperienceId,
            entry.LearningRouteId,
            string.Empty,
            entry.InclusionStatus,
            entry.SourceConfidence,
            entry.ReviewNote,
            entry.ReviewOnlyFragments);
}

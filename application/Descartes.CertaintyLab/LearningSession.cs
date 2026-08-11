namespace Descartes.CertaintyLab;

public enum MasteryState
{
    NotStarted,
    Read,
    Verified,
    Review,
}

public enum LearningStage
{
    Route,
    Lesson,
    Checking,
    Feedback,
}

public sealed record NodeMastery(
    string NodeId,
    MasteryState State,
    IReadOnlyList<string> PassedCheckIds,
    int TargetMisconceptionCount,
    DateTimeOffset UpdatedAt,
    string AbilitySignature = "");

public sealed record LearningProgress(
    string RouteId,
    string RouteVersion,
    IReadOnlyDictionary<string, NodeMastery> Nodes)
{
    public static LearningProgress Empty(
        string routeId,
        string routeVersion) =>
        new(
            routeId,
            routeVersion,
            new Dictionary<string, NodeMastery>(StringComparer.Ordinal));

    public MasteryState For(string nodeId) =>
        Nodes.TryGetValue(nodeId, out NodeMastery? mastery)
            ? mastery.State
            : MasteryState.NotStarted;

    public NodeMastery DetailFor(string nodeId) =>
        Nodes.TryGetValue(nodeId, out NodeMastery? mastery)
            ? mastery
            : new NodeMastery(
                nodeId,
                MasteryState.NotStarted,
                [],
                0,
                DateTimeOffset.MinValue,
                "");

    public LearningProgress With(NodeMastery mastery)
    {
        ArgumentNullException.ThrowIfNull(mastery);
        var updated = new Dictionary<string, NodeMastery>(
            Nodes,
            StringComparer.Ordinal)
        {
            [mastery.NodeId] = mastery,
        };
        return this with { Nodes = updated };
    }
}

public sealed class LearningSession
{
    private readonly LearningPack pack;
    private readonly LearningRouteDefinition route;
    private readonly HashSet<string> attemptPassedCheckIds =
        new(StringComparer.Ordinal);
    private LessonDefinition? currentLesson;
    private int currentCheckIndex = -1;

    private LearningSession(
        LearningPack pack,
        LearningRouteDefinition route,
        LearningProgress progress)
    {
        this.pack = pack;
        this.route = route;
        Progress = progress;
    }

    public LearningStage Stage { get; private set; } = LearningStage.Route;

    public LearningProgress Progress { get; private set; }

    public LessonDefinition? CurrentLesson => currentLesson;

    public KnowledgeCheckDefinition? CurrentCheck =>
        currentLesson is not null &&
        currentCheckIndex >= 0 &&
        currentCheckIndex < currentLesson.CheckIds.Count
            ? pack.GetCheck(currentLesson.CheckIds[currentCheckIndex])
            : null;

    public CheckOptionDefinition? LastAnswer { get; private set; }

    public bool LastAnswerPassed { get; private set; }

    public static LearningSession Start(
        LearningPack pack,
        string routeId,
        LearningProgress progress)
    {
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentNullException.ThrowIfNull(progress);
        LearningRouteDefinition route = pack.GetRoute(routeId);
        if (!string.Equals(
                progress.RouteId,
                route.Id,
                StringComparison.Ordinal) ||
            !string.Equals(
                progress.RouteVersion,
                route.Version,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "学习进度与当前路线身份或版本不一致。");
        }

        return new LearningSession(pack, route, progress);
    }

    public void OpenLesson(string lessonId)
    {
        RequireStage(LearningStage.Route);
        if (!route.LessonIds.Contains(lessonId, StringComparer.Ordinal))
        {
            throw new KeyNotFoundException(
                $"章节“{lessonId}”不属于当前学习路线。");
        }

        currentLesson = pack.GetLesson(lessonId);
        foreach (string nodeId in currentLesson.NodeIds)
        {
            NodeMastery current = Progress.DetailFor(nodeId);
            if (current.State == MasteryState.NotStarted)
            {
                Progress = Progress.With(current with
                {
                    State = MasteryState.Read,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    AbilitySignature =
                        LearningProgressStore.CreateAbilitySignature(
                            pack.GetNode(nodeId).AbilityIds),
                });
            }
        }

        currentCheckIndex = -1;
        LastAnswer = null;
        Stage = LearningStage.Lesson;
    }

    public void BeginChecks()
    {
        RequireStage(LearningStage.Lesson);
        if (currentLesson is null)
        {
            throw new InvalidOperationException("当前没有已打开的章节。");
        }

        attemptPassedCheckIds.Clear();
        currentCheckIndex = 0;
        LastAnswer = null;
        LastAnswerPassed = false;
        Stage = LearningStage.Checking;
    }

    public void Answer(string optionId)
    {
        RequireStage(LearningStage.Checking);
        KnowledgeCheckDefinition check =
            CurrentCheck ??
            throw new InvalidOperationException("当前没有理解检查。");
        CheckOptionDefinition option = check.Options.SingleOrDefault(
            candidate => string.Equals(
                candidate.Id,
                optionId,
                StringComparison.Ordinal)) ??
            throw new KeyNotFoundException($"未知选项“{optionId}”。");

        bool isControversy = check.TargetNodeIds
            .Select(pack.GetNode)
            .Any(node => node.Kind == KnowledgeNodeKind.Controversy);
        LastAnswerPassed = option.IsCorrect || isControversy;
        LastAnswer = option;
        if (LastAnswerPassed)
        {
            attemptPassedCheckIds.Add(check.Id);
        }

        foreach (string nodeId in check.TargetNodeIds)
        {
            NodeMastery current = Progress.DetailFor(nodeId);
            var passed = current.PassedCheckIds.ToHashSet(
                StringComparer.Ordinal);
            int misconceptionCount = current.TargetMisconceptionCount;
            MasteryState state = current.State == MasteryState.NotStarted
                ? MasteryState.Read
                : current.State;
            if (LastAnswerPassed)
            {
                passed.Add(check.Id);
            }
            else
            {
                misconceptionCount++;
                if (misconceptionCount >= 2)
                {
                    state = MasteryState.Review;
                }
            }

            Progress = Progress.With(new NodeMastery(
                nodeId,
                state,
                passed.Order(StringComparer.Ordinal).ToArray(),
                misconceptionCount,
                DateTimeOffset.UtcNow,
                LearningProgressStore.CreateAbilitySignature(
                    pack.GetNode(nodeId).AbilityIds)));
        }

        Stage = LearningStage.Feedback;
    }

    public void Continue()
    {
        RequireStage(LearningStage.Feedback);
        if (currentLesson is null)
        {
            throw new InvalidOperationException("当前没有已打开的章节。");
        }

        if (currentCheckIndex + 1 < currentLesson.CheckIds.Count)
        {
            currentCheckIndex++;
            LastAnswer = null;
            LastAnswerPassed = false;
            Stage = LearningStage.Checking;
            return;
        }

        bool allPassed = currentLesson.CheckIds.All(
            attemptPassedCheckIds.Contains);
        if (allPassed)
        {
            foreach (string nodeId in currentLesson.NodeIds)
            {
                NodeMastery current = Progress.DetailFor(nodeId);
                Progress = Progress.With(current with
                {
                    State = MasteryState.Verified,
                    TargetMisconceptionCount = 0,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    AbilitySignature =
                        LearningProgressStore.CreateAbilitySignature(
                            pack.GetNode(nodeId).AbilityIds),
                });
            }
        }

        currentCheckIndex = -1;
        LastAnswer = null;
        LastAnswerPassed = false;
        Stage = LearningStage.Lesson;
    }

    public void ReturnToRoute()
    {
        if (Stage is not (
                LearningStage.Lesson or
                LearningStage.Checking or
                LearningStage.Feedback))
        {
            throw new InvalidOperationException(
                $"当前阶段“{Stage}”不能返回路线。");
        }

        currentLesson = null;
        currentCheckIndex = -1;
        attemptPassedCheckIds.Clear();
        LastAnswer = null;
        LastAnswerPassed = false;
        Stage = LearningStage.Route;
    }

    public void CancelChecks()
    {
        if (Stage is not (
                LearningStage.Checking or
                LearningStage.Feedback))
        {
            throw new InvalidOperationException(
                $"当前阶段“{Stage}”没有可取消的理解检查。");
        }

        currentCheckIndex = -1;
        attemptPassedCheckIds.Clear();
        LastAnswer = null;
        LastAnswerPassed = false;
        Stage = LearningStage.Lesson;
    }

    private void RequireStage(LearningStage expected)
    {
        if (Stage != expected)
        {
            throw new InvalidOperationException(
                $"当前阶段“{Stage}”不能执行需要“{expected}”的操作。");
        }
    }
}

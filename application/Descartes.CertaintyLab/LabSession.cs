namespace Descartes.CertaintyLab;

public enum LabStage
{
    Opening,
    ChoosingReason,
    ReasonTested,
    EvidenceQuestioned,
    Reflection,
    Complete,
}

public sealed record LabSnapshot(
    LabStage Stage,
    string? SelectedReasonId,
    string OwnReasonText,
    string SceneText,
    string ThoughtTraceText,
    string LatestDiscovery,
    string CompletionText);

public sealed class LabSession
{
    private readonly ExperiencePack pack;
    private LabStage stage;
    private string? selectedReasonId;
    private string ownReasonText = string.Empty;
    private string sceneText;
    private string thoughtTraceText = string.Empty;
    private string latestDiscovery;

    private LabSession(ExperiencePack pack)
    {
        this.pack = pack;
        stage = LabStage.Opening;
        sceneText = pack.Opening;
        latestDiscovery = string.Empty;
    }

    public LabSnapshot Snapshot => CreateSnapshot();

    public static LabSession Start(ExperiencePack pack)
    {
        ArgumentNullException.ThrowIfNull(pack);
        return new LabSession(pack);
    }

    public LabSnapshot Begin()
    {
        RequireStage(LabStage.Opening);
        stage = LabStage.ChoosingReason;
        sceneText = pack.ReasonPrompt;
        latestDiscovery = string.Empty;
        return CreateSnapshot();
    }

    public LabSnapshot ChooseReason(string reasonId)
    {
        RequireStage(LabStage.ChoosingReason);
        ReasonDefinition reason = pack.Reasons.SingleOrDefault(
            candidate => candidate.Id == reasonId)
            ?? throw new ArgumentException(
                $"Unknown reason '{reasonId}'.",
                nameof(reasonId));

        selectedReasonId = reason.Id;
        stage = LabStage.ReasonTested;
        sceneText = $"你先依赖的是：{reason.Text}。";
        thoughtTraceText = reason.ThoughtTraceText;
        latestDiscovery = reason.TestResult;
        return CreateSnapshot();
    }

    public LabSnapshot UseOwnReason(string? reasonText)
    {
        RequireStage(LabStage.ChoosingReason);
        ownReasonText = (reasonText ?? string.Empty).Trim();
        selectedReasonId = "own-reason";
        stage = LabStage.ReasonTested;
        sceneText = ownReasonText.Length == 0
            ? pack.OwnReasonPrivateSceneText
            : $"你想到的是：{ownReasonText}";
        thoughtTraceText = ownReasonText.Length == 0
            ? "你保留了自己的办法。现在只用一个问题检验它：它是否也可能完整地出现在梦里？"
            : $"你暂时依靠：{ownReasonText}。现在只检验它是否也可能完整地出现在梦里。";
        latestDiscovery = pack.OwnReasonTestResult;
        return CreateSnapshot();
    }

    public LabSnapshot QuestionWholeExperience()
    {
        RequireStage(LabStage.ReasonTested);
        stage = LabStage.EvidenceQuestioned;
        sceneText = pack.WholeExperienceText;
        thoughtTraceText = pack.WholeExperienceThoughtTraceText;
        latestDiscovery = pack.WholeExperienceDiscovery;
        return CreateSnapshot();
    }

    public LabSnapshot AskWhatRemains()
    {
        RequireStage(LabStage.EvidenceQuestioned);
        stage = LabStage.Reflection;
        sceneText = pack.ReflectionText;
        thoughtTraceText = pack.ReflectionThoughtTraceText;
        latestDiscovery = string.Empty;
        return CreateSnapshot();
    }

    public LabSnapshot DoubtThinking()
    {
        RequireStage(LabStage.Reflection);
        stage = LabStage.Complete;
        sceneText = pack.CompletionIdentityText;
        thoughtTraceText = string.Empty;
        latestDiscovery = pack.ReflexiveDiscovery;
        return CreateSnapshot();
    }

    public LabSnapshot Restart()
    {
        selectedReasonId = null;
        ownReasonText = string.Empty;
        stage = LabStage.Opening;
        sceneText = pack.Opening;
        thoughtTraceText = string.Empty;
        latestDiscovery = string.Empty;
        return CreateSnapshot();
    }

    private void RequireStage(LabStage required)
    {
        if (stage != required)
        {
            throw new InvalidOperationException(
                $"This action requires stage {required}, but the current stage is {stage}.");
        }
    }

    private LabSnapshot CreateSnapshot()
    {
        return new LabSnapshot(
            stage,
            selectedReasonId,
            ownReasonText,
            sceneText,
            thoughtTraceText,
            latestDiscovery,
            stage == LabStage.Complete ? pack.CompletionText : string.Empty);
    }
}

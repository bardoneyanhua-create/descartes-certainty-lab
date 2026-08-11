namespace Descartes.CertaintyLab;

public enum ArendtStage
{
    Opening,
    ReplyArrives,
    BoundaryFound,
    Complete,
}

public sealed record ArendtSnapshot(
    ArendtStage Stage,
    string SceneText,
    string DiscoveryText,
    string SelectedStartingPointId,
    string OwnThought,
    string PersonalPathText,
    string CompletionIdentityText,
    string CompletionText);

public sealed class ArendtSession
{
    private readonly ArendtExperiencePack pack;
    private ArendtSnapshot snapshot;

    private ArendtSession(ArendtExperiencePack pack)
    {
        this.pack = pack;
        snapshot = OpeningSnapshot();
    }

    public ArendtSnapshot Snapshot => snapshot;

    public static ArendtSession Start(ArendtExperiencePack pack) =>
        new(pack ?? throw new ArgumentNullException(nameof(pack)));

    public ArendtSnapshot ChooseStartingPoint(string id)
    {
        EnsureStage(ArendtStage.Opening);
        ArendtStartingPointDefinition startingPoint =
            pack.StartingPoints.SingleOrDefault(item =>
                string.Equals(item.Id, id, StringComparison.Ordinal))
            ?? throw new ArgumentOutOfRangeException(
                nameof(id),
                id,
                "Unknown starting point.");

        snapshot = new ArendtSnapshot(
            ArendtStage.ReplyArrives,
            pack.ReplySceneText,
            startingPoint.Consequence,
            startingPoint.Id,
            string.Empty,
            startingPoint.PathSummary,
            string.Empty,
            string.Empty);
        return snapshot;
    }

    public ArendtSnapshot UseOwnThought(string thought)
    {
        EnsureStage(ArendtStage.Opening);
        string normalized = (thought ?? string.Empty).Trim();
        string path = normalized.Length == 0
            ? "你没有急着接受现成答案，而是先观察制度究竟在哪一步停止回应。"
            : $"你带着自己的问题继续：“{normalized}”";

        snapshot = new ArendtSnapshot(
            ArendtStage.ReplyArrives,
            pack.ReplySceneText,
            pack.OwnThoughtReplyText,
            "own-thought",
            normalized,
            path,
            string.Empty,
            string.Empty);
        return snapshot;
    }

    public ArendtSnapshot ExamineReply()
    {
        EnsureStage(ArendtStage.ReplyArrives);
        snapshot = snapshot with
        {
            Stage = ArendtStage.BoundaryFound,
            SceneText = pack.BoundarySceneText,
            DiscoveryText = pack.BoundaryDiscovery,
        };
        return snapshot;
    }

    public ArendtSnapshot RevealThinker()
    {
        EnsureStage(ArendtStage.BoundaryFound);
        snapshot = snapshot with
        {
            Stage = ArendtStage.Complete,
            SceneText = string.Empty,
            DiscoveryText = string.Empty,
            CompletionIdentityText = pack.CompletionIdentityText,
            CompletionText = pack.CompletionText,
        };
        return snapshot;
    }

    public ArendtSnapshot Restart()
    {
        snapshot = OpeningSnapshot();
        return snapshot;
    }

    private ArendtSnapshot OpeningSnapshot() =>
        new(
            ArendtStage.Opening,
            pack.Opening,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty);

    private void EnsureStage(ArendtStage expected)
    {
        if (snapshot.Stage != expected)
        {
            throw new InvalidOperationException(
                $"Expected stage {expected}, but the experience is at {snapshot.Stage}.");
        }
    }
}


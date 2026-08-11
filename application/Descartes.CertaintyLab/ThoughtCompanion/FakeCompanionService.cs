namespace Descartes.CertaintyLab.ThoughtCompanion;

public sealed class FakeCompanionService : ICompanionService
{
    private static readonly TimeSpan ResponseDelay =
        TimeSpan.FromMilliseconds(150);

    public async Task<CompanionOperationResult> SendAsync(
        CompanionDraft draft,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        cancellationToken.ThrowIfCancellationRequested();
        await Task.Delay(ResponseDelay, cancellationToken);

        CompanionClaim claim = draft.CurrentPageClaims.FirstOrDefault(item =>
                item.Evidence.Count > 0) ??
            throw new ArgumentException(
                "本地演示需要当前文章中的来源。",
                nameof(draft));
        CompanionEvidence evidence = claim.Evidence[0];
        string question = draft.Action switch
        {
            CompanionAction.ReflectMe =>
                "如果这个前提暂时不成立，你的判断会改变吗？",
            CompanionAction.QuestionMe =>
                "什么经验会让你愿意放下这个判断？",
            CompanionAction.ChallengeMe =>
                "当前文章的怀疑能否同样作用于你给出的根据？",
            CompanionAction.CompareMe =>
                "在当前资料只支持本篇论证时，你愿意先比较哪一种判断标准？",
            _ => throw new ArgumentOutOfRangeException(nameof(draft)),
        };
        var answer = new CompanionAnswer(
            $"你目前在说：“{draft.CurrentTurn}”",
            question,
            $"这与本篇“{claim.Title}”直接相关；当前演示只使用本篇已审核资料。",
            CompanionBasisLabel.AI提问,
            [new CompanionSource(claim.Id, evidence.Id, claim.Voice)]);

        return new CompanionOperationResult(
            answer,
            CompanionFailureKind.None,
            string.Empty,
            new CompanionUsage(0, 0, 0, 0));
    }
}

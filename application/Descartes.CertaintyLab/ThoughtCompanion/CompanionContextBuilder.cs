using System.Collections.Frozen;

namespace Descartes.CertaintyLab.ThoughtCompanion;

public sealed class CompanionContextBuilder
{
    private readonly int maximumClaims;

    public CompanionContextBuilder(int maximumClaims)
    {
        if (maximumClaims is < 1 or > 3)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumClaims));
        }

        this.maximumClaims = maximumClaims;
    }

    public CompanionContext Build(CompanionDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (string.IsNullOrWhiteSpace(draft.CurrentTurn))
        {
            throw new ArgumentException("本轮文字不能为空。", nameof(draft));
        }

        if (draft.ExplicitExcerpt is { UserApprovedForThisRequest: false })
        {
            throw new InvalidOperationException("长期记录没有获得本次发送授权。");
        }

        if (draft.ExplicitExcerpt is { UserApprovedForThisRequest: true } excerpt &&
            string.IsNullOrWhiteSpace(excerpt.Text))
        {
            throw new ArgumentException("已授权的长期记录摘录不能为空。", nameof(draft));
        }

        if (draft.CurrentPageClaims is null)
        {
            throw new ArgumentException("当前页 claim 集合不能为空。", nameof(draft));
        }

        CompanionClaim[] selectedClaims = draft.CurrentPageClaims
            .Take(maximumClaims)
            .ToArray();
        if (selectedClaims.Length == 0 ||
            selectedClaims.Any(claim => claim is null) ||
            selectedClaims.Any(claim => string.IsNullOrWhiteSpace(claim.Id)) ||
            selectedClaims.Select(claim => claim.Id).Distinct(StringComparer.Ordinal).Count() != selectedClaims.Length)
        {
            throw new ArgumentException("当前页必须提供唯一且非空的 claim。", nameof(draft));
        }

        if (selectedClaims.Any(claim =>
                claim.Evidence is null ||
                claim.Evidence.Any(item => item is null)))
        {
            throw new ArgumentException("当前页 evidence 集合和元素不能为空。", nameof(draft));
        }

        CompanionEvidence[] selectedEvidence = selectedClaims
            .SelectMany(claim => claim.Evidence)
            .ToArray();
        if (selectedEvidence.Any(item => string.IsNullOrWhiteSpace(item.Id)) ||
            selectedEvidence.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != selectedEvidence.Length)
        {
            throw new ArgumentException("当前页 evidence ID 必须唯一且非空。", nameof(draft));
        }

        CompanionClaim[] claimSnapshots = selectedClaims
            .Select(SnapshotClaim)
            .ToArray();

        return new CompanionContext(
            Array.AsReadOnly(claimSnapshots),
            draft.CurrentTurn.Trim(),
            draft.ExplicitExcerpt?.Text.Trim(),
            claimSnapshots.Select(claim => claim.Id).ToFrozenSet(StringComparer.Ordinal),
            claimSnapshots.SelectMany(claim => claim.Evidence).Select(item => item.Id).ToFrozenSet(StringComparer.Ordinal));
    }

    private static CompanionClaim SnapshotClaim(CompanionClaim claim)
    {
        CompanionEvidence[] evidenceSnapshots = claim.Evidence
            .Select(evidence => new CompanionEvidence(
                evidence.Id,
                evidence.WorkId,
                evidence.Edition,
                evidence.Locator,
                evidence.LocatorVerified))
            .ToArray();

        return new CompanionClaim(
            claim.Id,
            claim.Title,
            claim.Explanation,
            claim.Voice,
            Array.AsReadOnly(evidenceSnapshots));
    }
}

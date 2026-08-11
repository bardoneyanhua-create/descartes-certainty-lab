namespace Descartes.CertaintyLab.ThoughtCompanion.Security;

public static class CompanionCredentialTargets
{
    private const string Prefix = "PhilosophyVault/Descartes.CertaintyLab/Profiles/";

    public static string ForProfile(Guid profileId)
    {
        if (profileId == Guid.Empty)
        {
            throw new ArgumentException("Profile ID must not be empty.", nameof(profileId));
        }

        return Prefix + profileId.ToString("N");
    }

    internal static bool IsApplicationOwnedProfileTarget(string? targetName)
    {
        if (targetName is null || !targetName.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        ReadOnlySpan<char> suffix = targetName.AsSpan(Prefix.Length);
        return suffix.Length == 32 &&
               Guid.TryParseExact(suffix, "N", out Guid profileId) &&
               profileId != Guid.Empty &&
               string.Equals(targetName, ForProfile(profileId), StringComparison.Ordinal);
    }
}

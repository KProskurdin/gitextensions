using System.Text;
using GitCommands;
using GitCommands.Git;

namespace GitExtensions.Xplat.Core.CommitHistory;

/// <summary>
///  Upstream <c>MessageColumnProvider.TryGetToolTip</c> for the ref label under the pointer (its <c>highlightRef</c>): the
///  label's name in brackets and how it tracks or is tracked, then, with <c>ShowRevisionGridTooltips</c> on, the commit's
///  message tooltip.
/// </summary>
public static class RefLabelTooltips
{
    // Upstream's GitUI TranslatedStrings texts.
    public const string IsLocalBranch = "is a local branch";
    public const string IsRemoteBranch = "is a remote branch";
    public const string IsTag = "is a tag";
    public const string IsTrackedByBranchAheadBehind = "is tracked by [{0}]   {1}";
    public const string IsTrackingRemote = "is tracking [{0}]";
    public const string WasTrackingRemote = "was tracking [{0}], but the remote is gone";

    /// <summary>
    ///  Upstream's text after a local branch's name when git knows its tracked branch: the ahead and behind counts and the
    ///  branch it tracks, or that the tracked branch is gone.
    /// </summary>
    public static string LocalTracking(AheadBehindData data)
    {
        string remoteBranch = data.RemoteRef.StartsWith(GitRefName.RefsRemotesPrefix, StringComparison.Ordinal)
            ? data.RemoteRef[GitRefName.RefsRemotesPrefix.Length..]
            : data.RemoteRef;
        return data.AheadCount == AheadBehindData.Gone
            ? Environment.NewLine + string.Format(WasTrackingRemote, remoteBranch)
            : "   " + data.ToDisplay() + Environment.NewLine + string.Format(IsTrackingRemote, remoteBranch);
    }

    /// <summary>
    ///  Upstream's text after a remote branch's name when a local branch tracks it: that branch and the counts.
    /// </summary>
    public static string RemoteTracking(AheadBehindData data)
        => Environment.NewLine + string.Format(IsTrackedByBranchAheadBehind, data.Branch, data.ToDisplay());

    /// <summary>
    ///  The tooltip of <paramref name="label"/> with the commit's <paramref name="message"/> tooltip after it while
    ///  <paramref name="showTooltips"/> (<c>ShowRevisionGridTooltips</c>) is on; null where upstream shows none (a branch
    ///  without tracking information or a tag while the setting is off).
    /// </summary>
    public static string? For(RefLabel label, string? message, bool showTooltips)
    {
        string? description = label.Tracking ?? label.Kind switch
        {
            RefKind.RemoteBranch => showTooltips ? Environment.NewLine + IsRemoteBranch : null,
            RefKind.Branch => showTooltips ? Environment.NewLine + IsLocalBranch : null,
            RefKind.Tag => showTooltips ? Environment.NewLine + IsTag : null,
            _ => "",
        };
        if (description is null)
        {
            return null;
        }

        StringBuilder text = new StringBuilder().Append('[').Append(label.Name).Append(']').Append(description);
        if (showTooltips)
        {
            text.AppendLine().AppendLine().Append(message);
        }

        return text.ToString();
    }
}

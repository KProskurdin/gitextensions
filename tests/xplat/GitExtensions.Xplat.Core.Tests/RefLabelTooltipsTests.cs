using AwesomeAssertions;
using GitCommands.Git;
using GitExtensions.Xplat.Core.CommitHistory;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class RefLabelTooltipsTests
{
    private const string Message = "subject";

    [Test]
    public void A_local_branch_shows_upstreams_counts_and_the_branch_it_tracks()
    {
        RefLabel label = new("main", RefKind.Branch)
        {
            Tracking = RefLabelTooltips.LocalTracking(new AheadBehindData("main", "refs/remotes/origin/main", "2",
                "1")),
        };

        Text(label, showTooltips: false).Should().Be("[main]   2↑ 1↓\nis tracking [origin/main]");
        Text(label, showTooltips: true).Should().Be("[main]   2↑ 1↓\nis tracking [origin/main]\n\nsubject");
    }

    [Test]
    public void A_local_branch_whose_tracked_branch_is_gone_says_so()
    {
        RefLabel label = new("main", RefKind.Branch)
        {
            Tracking = RefLabelTooltips.LocalTracking(
                new AheadBehindData("main", "refs/remotes/origin/main", AheadBehindData.Gone, "")),
        };

        Text(label, showTooltips: false).Should().Be("[main]\nwas tracking [origin/main], but the remote is gone");
    }

    [Test]
    public void A_remote_branch_shows_the_local_branch_tracking_it()
    {
        RefLabel label = new("origin/main", RefKind.RemoteBranch)
        {
            Tracking = RefLabelTooltips.RemoteTracking(new AheadBehindData("main", "refs/remotes/origin/main", "0",
                "")),
        };

        Text(label, showTooltips: false).Should().Be("[origin/main]\nis tracked by [main]   0↑↓");
    }

    [TestCase(RefKind.Branch, "[topic]\nis a local branch\n\nsubject")]
    [TestCase(RefKind.RemoteBranch, "[topic]\nis a remote branch\n\nsubject")]
    [TestCase(RefKind.Tag, "[topic]\nis a tag\n\nsubject")]
    public void Without_tracking_a_label_says_what_it_is_while_the_grids_tooltips_are_on(RefKind kind, string expected)
    {
        RefLabel label = new("topic", kind);

        Text(label, showTooltips: true).Should().Be(expected);
        RefLabelTooltips.For(label, Message, showTooltips: false).Should().BeNull("upstream shows no tooltip then");
    }

    private static string? Text(RefLabel label, bool showTooltips)
        => RefLabelTooltips.For(label, Message, showTooltips)?.ReplaceLineEndings("\n");
}

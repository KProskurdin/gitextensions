using AwesomeAssertions;
using GitExtensions.Xplat.Core.CommitHistory;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class RevisionFilterTests
{
    [Test]
    public void The_default_should_show_every_branch_but_notes_the_stash_and_agent_refs()
    {
        RevisionFilter.AllBranches.ToRevisionArguments().Should().Be(
            "--exclude=refs/notes/ --exclude=refs/stash --exclude=refs/agents/** --exclude=refs/sessions/** --exclude=refs/copilot/checkpoints/** --all");
        RevisionFilter.AllBranches.IsNarrowed.Should().BeFalse();
    }

    [Test]
    public void Current_branch_only_should_read_from_HEAD()
    {
        new RevisionFilter(CurrentBranchOnly: true).ToRevisionArguments().Should().Be("HEAD");
    }

    [Test]
    public void Filters_should_come_in_upstream_order_and_ignore_case()
    {
        RevisionFilter filter = new(CurrentBranchOnly: true, Author: "ann", Message: "fix \"x\"", Since: new DateTime(2026, 1, 2),
            NoMerges: true, FirstParent: true);

        filter.ToRevisionArguments().Should().Be(
            "--since=\"2026-01-02 00:00:00\" --no-merges --author=\"ann\" --regexp-ignore-case --grep=\"fix x\" --first-parent HEAD");
        filter.IsNarrowed.Should().BeTrue();
    }

    [Test]
    public void A_path_alone_narrows_the_list()
    {
        new RevisionFilter(PathFilter: "src").IsNarrowed.Should().BeTrue();
    }
}

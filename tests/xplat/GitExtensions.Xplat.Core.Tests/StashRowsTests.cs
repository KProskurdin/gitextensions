using AwesomeAssertions;
using GitExtensions.Xplat.Core.CommitHistory;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class StashRowsTests
{
    [Test]
    public void Insert_should_put_a_stash_before_its_parent_and_drop_its_index_parent()
    {
        CommitRow head = Row("head", "base");
        CommitRow parent = Row("base");
        CommitRow stash = Row("s1", "base", "index") with { ReflogSelector = "refs/stash@{1}" };

        IReadOnlyList<CommitRow> rows = StashRows.Insert([head, parent], [stash], new Dictionary<string, CommitRow>());

        rows.Select(row => row.Hash).Should().Equal("head", "s1", "base");
        rows[1].ParentHashes.Should().Equal("base");
        rows[1].StashLabel.Should().Be("stash@{1}");
    }

    [Test]
    public void Insert_should_mark_a_stash_the_log_lists_and_add_an_untracked_commit_after_its_stash()
    {
        CommitRow listed = Row("s0", "base", "index");
        CommitRow parent = Row("base");
        CommitRow older = Row("s1", "base", "index", "untracked") with { ReflogSelector = "refs/stash@{1}" };
        CommitRow untracked = Row("untracked");

        IReadOnlyList<CommitRow> rows = StashRows.Insert([listed, parent],
            [Row("s0", "base", "index") with { ReflogSelector = "refs/stash@{0}" }, older],
            new Dictionary<string, CommitRow> { ["s1"] = untracked });

        rows.Select(row => row.Hash).Should().Equal("s0", "s1", "untracked", "base");
        rows[0].ReflogSelector.Should().Be("refs/stash@{0}");
        rows[0].ParentHashes.Should().Equal(["base", "index"], "the log's own row keeps its parents");
        rows[1].ParentHashes.Should().Equal("base", "untracked");
    }

    [Test]
    public void Insert_should_leave_out_a_stash_whose_parent_is_not_listed()
        => StashRows.Insert([Row("head")], [Row("s1", "elsewhere") with { ReflogSelector = "refs/stash@{0}" }],
                new Dictionary<string, CommitRow>())
            .Select(row => row.Hash).Should().Equal("head");

    [Test]
    public void UntrackedCommits_should_take_the_third_parents_up_to_the_limit()
        => StashRows.UntrackedCommits([Row("s0", "b", "i", "u0"), Row("s1", "b", "i"), Row("s2", "b", "i", "u2")], 1)
            .Should().BeEquivalentTo(new Dictionary<string, string> { ["s0"] = "u0" });

    [Test]
    public void ToRevisionArguments_should_list_the_latest_stash_when_stashes_are_shown()
    {
        RevisionFilter.AllBranches.ToRevisionArguments(showStashes: true).Should().NotContain("--exclude=refs/stash ")
            .And.Contain("--all");
        RevisionFilter.AllBranches.ToRevisionArguments().Should().Contain("--exclude=refs/stash ");
        new RevisionFilter(CurrentBranchOnly: true).ToRevisionArguments(showStashes: true).Should()
            .Be("--glob=refs/stas[h] HEAD");
    }

    private static CommitRow Row(string hash, params string[] parents) => new(hash, hash, "subject " + hash, "a", "", parents);
}

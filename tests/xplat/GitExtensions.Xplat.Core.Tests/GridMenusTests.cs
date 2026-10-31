using AwesomeAssertions;
using GitExtensions.Xplat.Core.CommitHistory;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class GridMenusTests
{
    private const string Hash = "1234567890123456789012345678901234567890";

    [Test]
    public void For_should_leave_the_current_branch_out_of_checkout_and_delete_and_separate_remote_branches()
    {
        GridRefMenus menus = GridMenus.For(Row(
            new RefLabel("main", RefKind.Branch) { IsCurrent = true, Upstream = "origin/main" },
            new RefLabel("topic", RefKind.Branch),
            new RefLabel("origin/main", RefKind.RemoteBranch),
            new RefLabel("origin/topic", RefKind.RemoteBranch),
            new RefLabel("v1", RefKind.Tag)), "main", new HashSet<string>());

        Names(menus.Checkout).Should().Equal("topic", null, "origin/main", "origin/topic");
        Names(menus.Delete).Should().Equal("topic", null, "origin/main", "origin/topic");
        Names(menus.Rename).Should().Equal("main", "topic");
        Names(menus.Push).Should().Equal("main", "topic");
        menus.IsHeadOfCurrentBranch.Should().BeTrue();
        Names(menus.DeleteTags).Should().Equal("v1");
        Names(menus.SelectInLeftPanel).Should().Equal("v1", "main", "topic", "origin/main", "origin/topic");
    }

    [Test]
    public void For_should_merge_tags_and_branches_but_not_the_tracked_remote_or_the_current_branch()
    {
        GridRefMenus menus = GridMenus.For(Row(
            new RefLabel("main", RefKind.Branch) { IsCurrent = true, Upstream = "origin/main" },
            new RefLabel("topic", RefKind.Branch),
            new RefLabel("origin/main", RefKind.RemoteBranch),
            new RefLabel("v1", RefKind.Tag)), "main", new HashSet<string>());

        Names(menus.Merge).Should().Equal("v1", "topic");
        menus.RebaseOnTopOf.Should().BeNull("the current branch points at the commit");
    }

    [Test]
    public void For_should_merge_the_commit_itself_when_no_ref_points_at_it()
    {
        GridRefMenus menus = GridMenus.For(Row(), "main", new HashSet<string>());

        menus.Merge.Should().ContainSingle().Which.Target.Should().Be(Hash);
        menus.RebaseOnTopOf.Should().Be(Hash);
        menus.Checkout.Should().BeEmpty();
    }

    [Test]
    public void For_should_merge_an_ambiguous_name_by_its_full_name()
    {
        GridRefMenus menus = GridMenus.For(Row(
            new RefLabel("release", RefKind.Branch),
            new RefLabel("release", RefKind.Tag)), "main", new HashSet<string> { "release" });

        menus.Merge.Select(item => item.Target).Should().Equal("refs/tags/release", "refs/heads/release");
    }

    [Test]
    public void CopyItems_should_number_the_refs_and_add_the_commit_values_with_access_keys()
    {
        DateTime date = new(2026, 10, 31, 12, 0, 0);
        CommitRow row = Row(new RefLabel("my_branch", RefKind.Branch), new RefLabel("v1", RefKind.Tag)) with
        {
            Body = "subject\n\nbody", Author = "Ann", AuthorEmail = "ann@example.com",
            AuthorDateValue = date, CommitDateValue = date,
        };

        IReadOnlyList<GridCopyItem?> items = GridMenus.CopyItems(row, "Branches", "Tags");

        items.Select(item => item?.Text).Should().Equal(
            "Branches", "_1:   my__branch", null,
            "Tags", "_2:   v1", null,
            $"_Commit hash:   {Hash}",
            "_Message:   subject",
            "_Author:   Ann <ann@example.com>",
            $"_Date:   {date}");
        items[1]!.Value.Should().Be("my_branch");
        items[0]!.Value.Should().BeNull("a caption copies nothing");
        items[7]!.Value.Should().Be("subject\n\nbody");
    }

    private static CommitRow Row(params RefLabel[] labels)
        => new(Hash, Hash[..8], "subject", "author", "date", [], [.. labels.Select(label => label.Name)], labels);

    private static IEnumerable<string?> Names(IEnumerable<GridMenuRef?> refs) => refs.Select(item => item?.Name);
}

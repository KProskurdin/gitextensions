using AwesomeAssertions;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class DeleteBranchDialogTests
{
    private static readonly string _root = Path.Combine(Path.GetTempPath(), "repo");
    private static readonly string _linked = Path.Combine(Path.GetTempPath(), "linked");

    [Test]
    public void Selection_should_split_on_spaces_and_set_aside_the_names_that_are_no_branches()
    {
        (IReadOnlyList<string> branches, IReadOnlyList<string> unknown) =
            DeleteBranchDialog.Selection(" one  three two ", ["one", "two"]);

        branches.Should().Equal("one", "two");
        unknown.Should().Equal("three");
        DeleteBranchDialog.SelectionText(branches).Should().Be("one two");
    }

    [Test]
    public void ClassifyWorktreeBranches_should_sort_the_other_worktrees_branches()
    {
        WorktreeInfo[] worktrees =
        [
            new(_root, "repo", IsMain: true, IsDeleted: false, Branch: "main"),
            new(_linked, "linked", IsMain: false, IsDeleted: false, Branch: "feature"),
            new(Path.Combine(Path.GetTempPath(), "gone"), "gone", IsMain: false, IsDeleted: true, Branch: "old"),
            new(Path.Combine(Path.GetTempPath(), "here"), "here", IsMain: false, IsDeleted: false, Branch: "current"),
        ];

        WorktreeBranches result = DeleteBranchDialog.ClassifyWorktreeBranches(
            ["main", "feature", "old", "current", "free"], worktrees, Path.Combine(Path.GetTempPath(), "here") + "/");

        result.HasDeletedWorktrees.Should().BeTrue();
        result.MainWorktreeBranches.Select(item => item.Branch).Should().Equal("main");
        result.LinkedWorktreeBranches.Select(item => item.Branch).Should().Equal("feature");
        DeleteBranchDialog.ClassifyWorktreeBranches(["free"], worktrees, _root).IsEmpty.Should().BeTrue();
    }
}

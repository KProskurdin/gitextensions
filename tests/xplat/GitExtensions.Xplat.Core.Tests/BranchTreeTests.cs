using AwesomeAssertions;
using GitExtensions.Xplat.Core.Repository;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class BranchTreeTests
{
    [Test]
    public void Build_should_group_local_branches_in_folders_and_remote_branches_by_remote()
    {
        IReadOnlyList<BranchTreeNode> roots = BranchTree.Build(
        [
            new BranchInfo("main", IsRemote: false, IsCurrent: true),
            new BranchInfo("feature/login", IsRemote: false, IsCurrent: false),
            new BranchInfo("feature/ui/menu", IsRemote: false, IsCurrent: false),
            new BranchInfo("origin/main", IsRemote: true, IsCurrent: false),
            new BranchInfo("origin/feature/login", IsRemote: true, IsCurrent: false),
            new BranchInfo("fork/main", IsRemote: true, IsCurrent: false),
        ]);

        roots.Select(root => root.Name).Should().Equal("Branches", "Remotes");
        BranchTreeNode local = roots[0];
        local.Children.Select(node => node.Display).Should().Equal("* main", "feature (2)");
        local.Children[1].Children.Select(node => node.Name).Should().Equal("login", "ui");
        local.Children[1].Children[0].Branch!.Name.Should().Be("feature/login");
        roots[1].Children.Select(node => node.Display).Should().Equal("origin (2)", "fork (1)");
        roots[1].Children[0].Children[1].Children.Single().Branch!.Name.Should().Be("origin/feature/login");
    }

    [Test]
    public void Build_should_leave_out_the_remote_group_when_there_are_no_remote_branches()
    {
        BranchTree.Build([new BranchInfo("main", IsRemote: false, IsCurrent: true)])
            .Should().ContainSingle().Which.Name.Should().Be("Branches");
    }

    [Test]
    public void Build_should_expand_the_folder_of_the_checked_out_branch()
    {
        IReadOnlyList<BranchTreeNode> roots = BranchTree.Build(
        [
            new BranchInfo("origin/team/x", IsRemote: true, IsCurrent: false),
            new BranchInfo("team/x", IsRemote: false, IsCurrent: true),
        ]);

        roots[0].Children.Single().IsExpanded.Should().BeTrue();
        roots[1].Children.Single().Children.Single().IsExpanded.Should().BeFalse("remote folders start collapsed");
        roots[0].Descendants().Count(node => node.Branch is not null).Should().Be(1);
    }
}

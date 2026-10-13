using AwesomeAssertions;
using GitExtensions.Xplat.Core.Repository;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class RepoObjectsTreeTests
{
    private const string RepositoryPath = "/work/repo";

    [Test]
    public void Build_should_show_the_trees_in_the_given_order_with_upstreams_root_images()
    {
        IReadOnlyList<RepoObjectNode> roots = RepoObjectsTree.Build(Objects(), RepoObjectsTree.Roots, text => text);

        roots.Select(root => (root.Text, root.Image, root.IsExpanded)).Should().Equal(
            ("Branches", "BranchLocalRoot", true),
            ("Remotes", "BranchRemoteRoot", true),
            ("Worktrees", "WorkTree", true),
            ("Tags", "TagHorizontal", false),
            ("Submodules", "FolderSubmodule", true),
            ("Stashes", "Stash", false));
        RepoObjectsTree.Build(Objects(), [RepoObjectKind.StashesRoot, RepoObjectKind.BranchesRoot], text => $"<{text}>")
            .Select(root => root.Text).Should().Equal("<Stashes>", "<Branches>");
    }

    [Test]
    public void Build_should_show_branches_with_their_images_ahead_behind_and_the_current_one_bold()
    {
        IReadOnlyList<RepoObjectNode> roots = RepoObjectsTree.Build(Objects(), RepoObjectsTree.Roots, text => text);

        RepoObjectNode branches = roots[0];
        branches.Children.Select(node => (node.Kind, node.Text, node.Image, node.IsBold)).Should().Equal(
            (RepoObjectKind.LocalBranch, "main (1↑)", "BranchLocal", true),
            (RepoObjectKind.Folder, "feature", "BranchFolder", false));
        branches.Children[1].Item.Should().Be("feature");
        branches.Children[1].Children.Single().Branch!.Name.Should().Be("feature/x");

        RepoObjectNode remotes = roots[1];
        remotes.Children.Select(node => (node.Kind, node.Text, node.Image)).Should().Equal(
            (RepoObjectKind.Remote, "origin", "GitHub"),
            (RepoObjectKind.Remote, "upstream", "Remote"));
        remotes.Children[0].ToolTip.Should().Be("https://github.com/me/repo.git");
        remotes.Children[0].Children.Single().Should().Match<RepoObjectNode>(node =>
            node.Kind == RepoObjectKind.RemoteBranch && node.Text == "main (1↓)" && node.Image == "BranchRemote");
        remotes.Children[1].Children.Should().BeEmpty("a remote without branches is listed too, as upstream's");
    }

    [Test]
    public void Build_should_list_worktrees_tags_in_folders_submodules_and_stashes()
    {
        IReadOnlyList<RepoObjectNode> roots = RepoObjectsTree.Build(Objects(), RepoObjectsTree.Roots, text => text);

        roots[2].Children.Select(node => (node.Text, node.IsBold)).Should().Equal(("repo", true), ("other", false));
        roots[3].Children.Select(node => (node.Kind, node.Text)).Should().Equal(
            (RepoObjectKind.Tag, "v1"), (RepoObjectKind.Folder, "release"));
        roots[3].Children[1].Children.Single().Item.Should().Be("release/2.0");
        roots[4].Children.Select(node => (node.Text, node.Image)).Should().Equal(
            ("lib", "FolderSubmodule"), ("ext (changed)", "FileStatusModified"));
        roots[5].Children.Single().Text.Should().Be("stash@{0}: WIP on main");
    }

    [Test]
    public void KeepExpansion_should_carry_the_expanded_state_over_to_the_same_nodes()
    {
        IReadOnlyList<RepoObjectNode> before = RepoObjectsTree.Build(Objects(), RepoObjectsTree.Roots, text => text);
        before[0].IsExpanded = false;
        before[3].IsExpanded = true;
        IReadOnlyList<RepoObjectNode> after = RepoObjectsTree.Build(Objects(), RepoObjectsTree.Roots, text => text);

        RepoObjectsTree.KeepExpansion(before, after);

        after[0].IsExpanded.Should().BeFalse();
        after[3].IsExpanded.Should().BeTrue();
        after[1].IsExpanded.Should().BeTrue();
    }

    [Test]
    public void FindNext_should_find_the_next_matching_node_ignoring_case_and_wrap_around()
    {
        IReadOnlyList<RepoObjectNode> roots = RepoObjectsTree.Build(Objects(), RepoObjectsTree.Roots, text => text);

        RepoObjectNode first = RepoObjectsTree.FindNext(roots, "MAIN", current: null)!;
        first.Branch!.Name.Should().Be("main");
        RepoObjectNode second = RepoObjectsTree.FindNext(roots, "main", first)!;
        second.Branch!.Name.Should().Be("origin/main");
        RepoObjectsTree.FindNext(roots, "main", RepoObjectsTree.FindNext(roots, "main", second)).Should().Be(first);
        RepoObjectsTree.FindNext(roots, "nothing", current: null).Should().BeNull();
        RepoObjectsTree.PathTo(roots, second).Select(node => node.Name).Should().Equal("Remotes", "origin", "main");
    }

    private static RepoObjects Objects() => new(
        [
            new BranchInfo("main", IsRemote: false, IsCurrent: true),
            new BranchInfo("feature/x", IsRemote: false, IsCurrent: false),
            new BranchInfo("origin/main", IsRemote: true, IsCurrent: false),
        ],
        ["origin", "upstream"],
        ["v1", "release/2.0"],
        [new StashInfo("stash@{0}", "WIP on main")],
        [new SubmoduleInfo("lib", IsInitialized: true, IsUpToDate: true), new SubmoduleInfo("ext", IsInitialized: true, IsUpToDate: false)],
        [
            new WorktreeInfo(RepositoryPath, "repo", IsMain: true, IsDeleted: false),
            new WorktreeInfo("/work/other", "other", IsMain: false, IsDeleted: false),
            new WorktreeInfo("/work/gone", "gone", IsMain: false, IsDeleted: true),
        ],
        new Dictionary<string, string> { ["main"] = "1↑", ["origin/main"] = "1↓" },
        new Dictionary<string, string> { ["origin"] = "https://github.com/me/repo.git", ["upstream"] = "/srv/repo.git" },
        RepositoryPath);
}

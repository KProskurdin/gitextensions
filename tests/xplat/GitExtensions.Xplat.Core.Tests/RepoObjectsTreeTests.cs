using AwesomeAssertions;
using GitExtensions.Xplat.Core.CommitHistory;
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
    public void Build_should_list_inactive_remotes_last_in_upstreams_inactive_folder()
    {
        RepoObjects objects = Objects() with
        {
            InactiveRemotes = ["old"],
            RemoteUrls = new Dictionary<string, string> { ["origin"] = "https://github.com/me/repo.git", ["old"] = "/srv/old.git" },
        };

        RepoObjectNode remotes = RepoObjectsTree.Build(objects, RepoObjectsTree.Roots, text => $"<{text}>")[1];

        RepoObjectNode folder = remotes.Children[^1];
        (folder.Kind, folder.Text, folder.Image, folder.IsExpanded).Should()
            .Be((RepoObjectKind.InactiveRemotes, "<[ Inactive ]>", "EyeClosed", false));
        folder.Children.Single().Should().Match<RepoObjectNode>(node =>
            node.Kind == RepoObjectKind.Remote && node.Text == "old" && node.IsInactive && node.ToolTip == "/srv/old.git");
        remotes.Children.Where(node => node.Kind == RepoObjectKind.Remote).Should().OnlyContain(node => !node.IsInactive);
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
    public void PathTo_should_list_the_nodes_from_the_root_down_to_the_node()
    {
        IReadOnlyList<RepoObjectNode> roots = RepoObjectsTree.Build(Objects(), RepoObjectsTree.Roots, text => text);

        RepoObjectsTree.PathTo(roots, BranchNode(roots, "origin/main")).Select(node => node.Name).Should()
            .Equal("Remotes", "origin", "main");
        RepoObjectsTree.PathTo(roots, new RepoObjectNode(RepoObjectKind.Tag, "gone", "TagHorizontal")).Should().BeEmpty();
    }

    [Test]
    public void ShowMerged_should_mark_contained_branches_except_those_at_the_selected_commit()
    {
        IReadOnlyList<RepoObjectNode> roots = RepoObjectsTree.Build(Objects(), RepoObjectsTree.Roots, text => text);
        CommitRow selected = Row("abc", new RefLabel("main", RefKind.Branch));

        RepoObjectsTree.ShowMerged(roots, ["refs/heads/main", "refs/heads/feature/x", "refs/remotes/origin/main"], selected,
            text => $"<{text}>");

        RepoObjectNode main = BranchNode(roots, "main");
        RepoObjectNode feature = BranchNode(roots, "feature/x");
        RepoObjectNode remote = BranchNode(roots, "origin/main");
        main.IsMerged.Should().BeFalse("upstream leaves out the refs of the selected commit itself");
        main.Image.Should().Be("BranchLocal");
        main.ToolTip.Should().BeNull();
        (feature.IsMerged, feature.Image).Should().Be((true, "BranchLocalMerged"));
        feature.ToolTip.Should().Be("<'{0}' is contained in the currently selected commit>".Replace("{0}", "x"),
            "upstream's text names the node, not the full branch name");
        (remote.IsMerged, remote.Image).Should().Be((true, "BranchRemoteMerged"));

        RepoObjectsTree.ShowMerged(roots, [], selected: null, text => text);

        roots.SelectMany(root => root.Descendants()).Should().OnlyContain(node => !node.IsMerged);
        feature.Image.Should().Be("BranchLocal");
        feature.ToolTip.Should().BeNull();
    }

    [Test]
    public void ShowMerged_should_keep_a_nodes_own_tooltip_once_it_is_no_longer_merged()
    {
        RepoObjectNode node = new(RepoObjectKind.LocalBranch, "x", "BranchLocal", new BranchInfo("x", IsRemote: false, IsCurrent: false))
        {
            ToolTip = "own",
        };
        List<string?> changed = [];
        node.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        RepoObjectsTree.ShowMerged([node], ["refs/heads/x"], selected: null, text => text);
        node.ToolTip.Should().Be("'x' is contained in the currently selected commit");
        RepoObjectsTree.ShowMerged([node], [], selected: null, text => text);

        node.ToolTip.Should().Be("own");
        changed.Should().Contain([nameof(RepoObjectNode.IsMerged), nameof(RepoObjectNode.Image), nameof(RepoObjectNode.ToolTip)]);
    }

    [Test]
    public void ShowHidden_should_hide_the_branches_tags_and_stashes_the_grid_has_not_loaded()
    {
        IReadOnlyList<RepoObjectNode> roots = RepoObjectsTree.Build(Objects(), RepoObjectsTree.Roots, text => text);
        CommitRow[] loaded =
        [
            Row("abc", new RefLabel("main", RefKind.Branch), new RefLabel("v1", RefKind.Tag)),
            Row("def", new RefLabel("origin/main", RefKind.RemoteBranch)),
            Row("stash") with { ReflogSelector = "refs/stash@{0}" },
        ];

        RepoObjectsTree.ShowHidden(roots, loaded, text => $"<{text}>");

        List<RepoObjectNode> nodes = [.. roots.SelectMany(root => root.Descendants())];
        RepoObjectNode feature = BranchNode(roots, "feature/x");
        RepoObjectNode release = nodes.Single(node => node.Item as string == "release/2.0");
        (feature.IsHidden, feature.Image).Should().Be((true, "EyeClosed"));
        feature.ToolTip.Should().Be("<'{0}' is not currently visible>".Replace("{0}", "feature/x"),
            "upstream's text names the branch's full path");
        (release.IsHidden, release.Image, release.ToolTip).Should().Be((true, "EyeClosed", null));
        BranchNode(roots, "main").IsHidden.Should().BeFalse();
        BranchNode(roots, "origin/main").IsHidden.Should().BeFalse();
        nodes.Single(node => node.Item as string == "v1").Image.Should().Be("TagHorizontal");
        nodes.Single(node => node.Kind == RepoObjectKind.Stash).IsHidden.Should().BeFalse();
        nodes.Where(node => node.Kind is RepoObjectKind.Folder or RepoObjectKind.Worktree or RepoObjectKind.Submodule
                or RepoObjectKind.Remote || node.IsRoot)
            .Should().OnlyContain(node => !node.IsHidden, "upstream checks the nodes with a revision only");

        RepoObjectsTree.ShowHidden(roots, [], text => text);

        nodes.Single(node => node.Kind == RepoObjectKind.Stash).Image.Should().Be("EyeClosed");
        RepoObjectsTree.ShowHidden(roots, [Row("x", new RefLabel("feature/x", RefKind.Branch))], text => text);
        (feature.IsHidden, feature.Image, feature.ToolTip).Should().Be((false, "BranchLocal", null));
    }

    [Test]
    public void ShowHidden_should_win_over_the_merged_image_and_tooltip()
    {
        IReadOnlyList<RepoObjectNode> roots = RepoObjectsTree.Build(Objects(), RepoObjectsTree.Roots, text => text);
        RepoObjectNode feature = BranchNode(roots, "feature/x");
        RepoObjectsTree.ShowMerged(roots, ["refs/heads/feature/x"], selected: null, text => text);

        RepoObjectsTree.ShowHidden(roots, [], text => text);

        (feature.IsMerged, feature.Image, feature.ToolTip).Should()
            .Be((true, "EyeClosed", "'feature/x' is not currently visible"), "as upstream's BaseBranchLeafNode.ApplyStyle");
    }

    [Test]
    public void MergedBranchesCommit_should_ask_about_the_latest_selected_commit_and_head_for_an_artificial_one()
    {
        RepoObjectsTree.MergedBranchesCommit([]).Should().BeNull();
        RepoObjectsTree.MergedBranchesCommit([Row("abc"), Row("def")]).Should().Be("abc");
        RepoObjectsTree.MergedBranchesCommit([Row(ArtificialCommits.WorkTreeHash)]).Should().Be("HEAD");
        RepoObjectsTree.MergedBranchesCommit([Row(ArtificialCommits.IndexHash)]).Should().Be("HEAD");
    }

    [Test]
    public void KeepsMerged_should_be_true_only_when_the_grid_followed_the_trees_branch_or_tag()
    {
        IReadOnlyList<RepoObjectNode> roots = RepoObjectsTree.Build(Objects(), RepoObjectsTree.Roots, text => text);
        RepoObjectNode main = BranchNode(roots, "main");
        RepoObjectNode tag = roots.SelectMany(root => root.Descendants()).Single(node => node.Item as string == "v1");
        CommitRow atMain = Row("abc", new RefLabel("main", RefKind.Branch), new RefLabel("v1", RefKind.Tag));

        RepoObjectsTree.KeepsMerged([], node: null).Should().BeTrue();
        RepoObjectsTree.KeepsMerged([atMain], main).Should().BeTrue();
        RepoObjectsTree.KeepsMerged([atMain], tag).Should().BeTrue();

        RepoObjectsTree.KeepsMerged([], main).Should().BeFalse("upstream clears the merged branches then");
        RepoObjectsTree.KeepsMerged([atMain], node: null).Should().BeFalse();
        RepoObjectsTree.KeepsMerged([atMain, Row("def")], main).Should().BeFalse();
        RepoObjectsTree.KeepsMerged([Row("def")], main).Should().BeFalse();
        RepoObjectsTree.KeepsMerged([Row("abc", new RefLabel("main", RefKind.RemoteBranch))], main).Should()
            .BeFalse("a remote branch of the same name is another ref");
        RepoObjectsTree.KeepsMerged([atMain], roots[0]).Should().BeFalse("upstream compares the commits of branches and tags only");
    }

    private static CommitRow Row(string hash, params RefLabel[] labels)
        => new(hash, hash, "subject", "author", "date", Labels: labels);

    private static RepoObjectNode BranchNode(IReadOnlyList<RepoObjectNode> roots, string name)
        => roots.SelectMany(root => root.Descendants()).Single(node => node.Branch?.Name == name);

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

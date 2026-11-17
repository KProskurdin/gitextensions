using AwesomeAssertions;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Presentation;
using GitExtensions.Xplat.Core.Repository;
using GitUIPluginInterfaces;
using NSubstitute;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class RepoObjectMenuViewModelTests
{
    private const string RepositoryPath = "/work/repo";

    private RepoObjectNode _branches = null!;
    private RepoObjectNode _main = null!;
    private RepoObjectNode _topic = null!;
    private RepoObjectNode _tags = null!;
    private RepoObjectNode _tag = null!;
    private RepoObjectNode _remotes = null!;
    private RepoObjectNode _remote = null!;
    private RepoObjectNode _worktrees = null!;
    private RepoObjectNode _otherWorktree = null!;
    private IReadOnlyList<RepoObjectNode> _roots = null!;

    [SetUp]
    public void SetUp()
    {
        _branches = new RepoObjectNode(RepoObjectKind.BranchesRoot, "Branches", "BranchLocalRoot", isExpanded: true);
        _main = new RepoObjectNode(RepoObjectKind.LocalBranch, "main", "BranchLocal", new BranchInfo("main", false, IsCurrent: true));
        _topic = new RepoObjectNode(RepoObjectKind.LocalBranch, "topic", "BranchLocal", new BranchInfo("topic", false, false));
        _branches.Children.AddRange([_main, _topic]);
        _remotes = new RepoObjectNode(RepoObjectKind.RemotesRoot, "Remotes", "BranchRemoteRoot");
        _remote = new RepoObjectNode(RepoObjectKind.Remote, "origin", "Remote", "origin");
        _remotes.Children.Add(_remote);
        _tags = new RepoObjectNode(RepoObjectKind.TagsRoot, "Tags", "TagHorizontal");
        _tag = new RepoObjectNode(RepoObjectKind.Tag, "v1", "TagHorizontal", "v1");
        _tags.Children.Add(_tag);
        _worktrees = new RepoObjectNode(RepoObjectKind.WorktreesRoot, "Worktrees", "WorkTree");
        _otherWorktree = new RepoObjectNode(RepoObjectKind.Worktree, "other", "WorkTree",
            new WorktreeInfo("/work/other", "other", IsMain: false, IsDeleted: false));
        _worktrees.Children.Add(_otherWorktree);
        _roots = [_branches, _remotes, _tags, _worktrees];
    }

    [Test]
    public void A_branch_should_get_the_branch_items_and_the_current_one_only_branch_and_rename()
    {
        RepoObjectMenuViewModel menu = New();

        menu.Refresh(Input(_topic)).Should().BeTrue();

        (menu.Checkout.IsOffered, menu.Merge.IsOffered, menu.Delete.IsOffered, menu.RenameBranch.IsOffered).Should()
            .Be((true, true, true, true));
        menu.Checkout.Text.Should().Be("T:" + RepoObjectMenuViewModel.CheckoutBranchText);
        (menu.ManageRemotes.IsVisible, menu.OpenStash.IsVisible, menu.FetchBranch.IsVisible).Should().Be((false, false, false));
        menu.RunScript.IsVisible.Should().BeTrue("the grid shows the branch");

        menu.Refresh(Input(_main));

        (menu.Checkout.IsVisible, menu.Checkout.IsEnabled, menu.Delete.IsEnabled).Should().Be((true, false, false));
        (menu.CreateBranch.IsOffered, menu.RenameBranch.IsOffered).Should().Be((true, true));
    }

    [Test]
    public void A_tag_should_get_the_tag_texts_and_delete_on_the_remote_only_with_remotes()
    {
        RepoObjectMenuViewModel menu = New();

        menu.Refresh(Input(_tag, remoteCount: 0));
        menu.DeleteRemoteTag.IsVisible.Should().BeFalse();
        menu.Refresh(Input(_tag, remoteCount: 1));

        menu.DeleteRemoteTag.IsOffered.Should().BeTrue();
        (menu.Checkout.Text, menu.Rebase.Text, menu.Delete.Text).Should().Be(("T:" + RepoObjectMenuViewModel.CheckoutTagText,
            "T:" + RepoObjectMenuViewModel.RebaseOnTagText, "T:" + RepoObjectMenuViewModel.DeleteTagText));
        menu.Merge.Tip.Should().Be("T:" + RepoObjectMenuViewModel.MergeTagTooltip);
        menu.RenameBranch.IsVisible.Should().BeFalse();
    }

    [Test]
    public void A_remote_should_open_its_address_only_for_a_web_one_and_a_root_should_move_within_the_roots()
    {
        RepoObjectMenuViewModel menu = New();

        menu.Refresh(Input(_remote, hasRemoteWebUrl: false));
        (menu.OpenRemoteUrl.IsVisible, menu.OpenRemoteUrl.IsEnabled, menu.FetchRemote.IsOffered, menu.ActivateRemote.IsVisible)
            .Should().Be((true, false, true, false));

        menu.Refresh(Input(_branches));
        (menu.MoveUp.IsVisible, menu.MoveUp.IsEnabled, menu.MoveDown.IsEnabled).Should().Be((true, false, true));
        (menu.Collapse.IsEnabled, menu.Expand.IsEnabled).Should().Be((true, false), "the branches are expanded");
        menu.Checkout.IsVisible.Should().BeFalse();
    }

    [Test]
    public void Another_worktree_should_be_opened_and_shown_in_its_folder_only_when_it_exists()
    {
        RepoObjectMenuViewModel menu = New(directoryExists: path => path == "/work/other");

        menu.Refresh(Input(_otherWorktree));

        (menu.OpenWorktree.IsOffered, menu.DeleteWorktree.IsOffered, menu.ShowWorktreeInFolder.IsOffered).Should()
            .Be((true, true, true));
        New().Refresh(Input(_otherWorktree)).Should().BeTrue();
    }

    [Test]
    public void Nothing_should_be_offered_while_busy_and_several_marked_nodes_should_get_only_the_shared_items()
    {
        RepoObjectMenuViewModel menu = New();

        menu.Refresh(Input(_topic, isOpen: false)).Should().BeFalse();

        RepoObjectSelection.Click(_roots, _topic, rightButton: false, multiple: true, includingDescendants: false);
        RepoObjectSelection.Click(_roots, _tag, rightButton: false, multiple: true, includingDescendants: false);
        RepoObjectSelection.Selected(_roots).Should().HaveCount(2);
        menu.Refresh(Input(_tag));

        (menu.Checkout.IsVisible, menu.Delete.IsVisible).Should().Be((false, false));
        menu.Filter.IsOffered.Should().BeTrue("the filter takes every marked ref");
        RepoObjectMenuViewModel.Targets(_roots, _tag).Should().Equal(_topic, _tag);
    }

    [Test]
    public async Task A_tag_should_be_checked_out_merged_and_rebased_on_as_a_revision_and_a_branch_through_the_branch_flows()
    {
        (RepoObjectMenuViewModel menu, IRepoObjectWindow window, _) = Attached(_tag);

        menu.RunCheckoutRefCommand.Execute(null);
        menu.RunMergeRefCommand.Execute(null);
        menu.RunRebaseOnRefCommand.Execute(null);
        await window.Received(1).CheckoutRevisionAsync(RepositoryPath, "v1");
        await window.Received(1).MergeBranchAsync(RepositoryPath, "v1");
        await window.Received(1).RebaseOnRevisionAsync(RepositoryPath, "v1");

        (menu, window, _) = Attached(_topic);
        menu.RunCheckoutRefCommand.Execute(null);
        menu.RunRebaseOnRefCommand.Execute(null);
        window.Received(1).CheckoutSelectedBranch();
        window.Received(1).RebaseOnSelectedBranch();
    }

    [Test]
    public void A_remote_should_be_fetched_pruned_and_deactivated_by_its_name()
    {
        (RepoObjectMenuViewModel menu, IRepoObjectWindow window, _) = Attached(_remote);

        menu.RunFetchRemoteCommand.Execute(null);
        menu.RunPruneRemoteCommand.Execute(null);
        menu.RunDeactivateRemoteCommand.Execute(null);
        menu.RunFetchAllRemotesCommand.Execute(null);

        window.Received(1).Fetch("origin", false);
        window.Received(1).Fetch("origin", true);
        window.Received(1).SetRemoteActive("origin", false, false);
        window.Received(1).Fetch("--all", false);
    }

    [Test]
    public async Task Deleting_a_folders_branches_should_leave_out_the_current_one()
    {
        RepoObjectNode folder = new(RepoObjectKind.Folder, "feature", "Folder", "feature");
        folder.Children.AddRange([
            new RepoObjectNode(RepoObjectKind.LocalBranch, "a", "BranchLocal", new BranchInfo("feature/a", false, false)),
            new RepoObjectNode(RepoObjectKind.LocalBranch, "b", "BranchLocal", new BranchInfo("feature/b", false, IsCurrent: true))]);
        _branches.Children.Add(folder);
        (RepoObjectMenuViewModel menu, IRepoObjectWindow window, _) = Attached(folder);

        menu.RunDeleteAllBranchesCommand.Execute(null);
        menu.RunCreateBranchInFolderCommand.Execute(null);

        await window.Received(1).DeleteBranchesAsync(RepositoryPath,
            Arg.Is<IReadOnlyList<string>>(branches => branches.SequenceEqual(new[] { "feature/a" })));
        await window.Received(1).PromptCreateBranchInFolderAsync("feature");
    }

    [Test]
    public void Collapse_and_expand_should_act_on_the_node_with_its_subnodes()
    {
        (RepoObjectMenuViewModel menu, _, _) = Attached(_branches);

        menu.RunCollapseNodesCommand.Execute(null);
        _branches.IsExpanded.Should().BeFalse();
        menu.RunExpandNodesCommand.Execute(null);

        _branches.IsExpanded.Should().BeTrue();
    }

    [Test]
    public void Fetch_branch_should_fetch_the_remote_of_a_remote_branch()
    {
        RepoObjectNode remoteBranch = new(RepoObjectKind.RemoteBranch, "feature", "BranchRemote",
            new BranchInfo("origin/feature/x", IsRemote: true, false));
        _remote.Children.Add(remoteBranch);
        (RepoObjectMenuViewModel menu, _, FakeGitOperations operations) = Attached(remoteBranch);

        menu.RunFetchBranchCommand.Execute(null);

        (operations.NameAt(0), operations.ArgumentsAt(0)).Should().Be(("Fetch", RepositoryPath + " origin prune=False"));
    }

    [Test]
    public void The_sort_menus_should_check_the_current_choice_and_pass_the_one_clicked()
    {
        (RepoObjectMenuViewModel menu, IRepoObjectWindow window, _) = Attached(_branches);

        IReadOnlyList<MenuEntry> sortBy = menu.SortByMenu(GitRefsSortBy.refname);
        IReadOnlyList<MenuEntry> order = menu.SortOrderMenu(GitRefsSortOrder.Descending);

        sortBy.Should().HaveCount(Enum.GetValues<GitRefsSortBy>().Length);
        sortBy.Where(entry => entry.IsChecked == true).Select(entry => entry.CommandParameter).Should()
            .Equal(GitRefsSortBy.refname);
        sortBy.Should().OnlyContain(entry => entry.IsRadio);
        order.Where(entry => entry.IsChecked == true).Select(entry => entry.CommandParameter).Should()
            .Equal(GitRefsSortOrder.Descending);

        sortBy[0].Command!.Execute(sortBy[0].CommandParameter);
        order[0].Command!.Execute(order[0].CommandParameter);
        window.Received(1).SetRefsSortBy(Enum.GetValues<GitRefsSortBy>()[0]);
        window.Received(1).SetRefsSortOrder(Enum.GetValues<GitRefsSortOrder>()[0]);
    }

    private (RepoObjectMenuViewModel Menu, IRepoObjectWindow Window, FakeGitOperations Operations) Attached(
        RepoObjectNode node)
    {
        RepoObjectMenuViewModel menu = New();
        IRepoObjectWindow window = Substitute.For<IRepoObjectWindow>();
        FakeGitOperations operations = new();
        menu.Attach(new RepositoryOperationsViewModel(operations), window);
        menu.Read = () => Input(node) with { RepositoryPath = RepositoryPath };
        return (menu, window, operations);
    }

    private static RepoObjectMenuViewModel New(Func<string, bool>? directoryExists = null)
        => new(text => "T:" + text, directoryExists ?? (_ => false));

    private RepoObjectMenuInput Input(RepoObjectNode node, bool isOpen = true, int remoteCount = 1,
        bool hasRemoteWebUrl = false)
        => new(_roots, node, isOpen, SelectedRevisionCount: 1, remoteCount, hasRemoteWebUrl, IsBareRepository: false,
            GitRefsSortBy.Default);
}

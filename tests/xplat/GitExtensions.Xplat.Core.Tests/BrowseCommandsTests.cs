using AwesomeAssertions;
using GitCommands.Git;
using GitExtensions.Extensibility.Plugins;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Presentation;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Scripts;
using GitExtensions.Xplat.Core.Settings;
using NSubstitute;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

// The browse window's commands (BrowseToolbarViewModel.Commands): which window action they ask for, with which revision,
// and when they apply.
internal sealed class BrowseCommandsTests
{
    private const string RepositoryPath = "/work/repo";

    private FakeCommitHistory _history = null!;
    private CommitListViewModel _commits = null!;
    private FakeRepositoryService _service = null!;
    private RepositoryViewModel _repository = null!;
    private FakeGitOperations _operations = null!;
    private IBrowseWindow _window = null!;
    private InMemoryAppPreferences _preferences = null!;
    private BrowseToolbarViewModel _viewModel = null!;

    [SetUp]
    public void SetUp()
    {
        _history = new FakeCommitHistory();
        _commits = new CommitListViewModel(_history);
        _service = new FakeRepositoryService();
        _repository = new RepositoryViewModel(_service);
        _operations = new FakeGitOperations();
        _window = Substitute.For<IBrowseWindow>();
        _preferences = new InMemoryAppPreferences();
        _viewModel = new BrowseToolbarViewModel(_commits, _repository, new RepositoryOperationsViewModel(_operations),
            new RecentRepositoriesViewModel(new InMemoryRecentRepositoryStore()), _preferences,
            gitAvailable: true) { Window = _window };
    }

    [Test]
    public async Task The_revision_commands_should_take_the_selected_commit_or_HEAD_without_one()
    {
        _viewModel.BrowseFilesCommand.Execute(null);
        _viewModel.CreateTagCommand.Execute(null);
        _window.Received(1).ShowFiles("HEAD");
        await _window.Received(1).CreateTagAsync("HEAD");

        await OpenAsync("aaa1");
        _viewModel.BrowseFilesCommand.Execute(null);
        _viewModel.CheckoutCommitCommand.Execute(null);
        _viewModel.CreateBranchCommand.Execute(null);

        _window.Received(1).ShowFiles("aaa1");
        await _window.Received(1).CheckoutRevisionAsync(RepositoryPath, "aaa1");
        await _window.Received(1).PromptCreateBranchAsync("aaa1");
    }

    [Test]
    public async Task The_repository_commands_should_do_nothing_without_a_repository()
    {
        _viewModel.ResetChangesCommand.Execute(null);
        _viewModel.MergeCommand.Execute(null);
        _viewModel.OpenFolderCommand.Execute(null);
        _viewModel.CheckoutCommitCommand.Execute(null);

        await _window.DidNotReceiveWithAnyArgs().ResetChangesAsync(default!);
        await _window.DidNotReceiveWithAnyArgs().MergeAsync(default!);
        _window.DidNotReceiveWithAnyArgs().OpenFolder(default!);
        await _window.DidNotReceiveWithAnyArgs().CheckoutRevisionAsync(default!, default);

        await OpenAsync("aaa1");
        _viewModel.OpenFolderCommand.Execute(null);
        _window.Received(1).OpenFolder(RepositoryPath);
    }

    [Test]
    public async Task Continue_and_abort_should_run_the_stopped_actions_own_git_command()
    {
        await OpenAsync("aaa1");
        Task state = _repository.RefreshAsync(RepositoryPath);
        _service.Complete(0, new RepositorySnapshot("main", [], [], false, [], [], IsRebasing: true));
        await state;

        _viewModel.ContinueActionCommand.Execute(null);
        _viewModel.AbortActionCommand.Execute(null);
        _viewModel.MoreActionCommand.Execute(null);

        (_operations.NameAt(0), _operations.NameAt(1)).Should().Be(("ContinueRebase", "AbortRebase"));
        await _window.Received(1).ContinueRebaseDialogAsync(RepositoryPath);
    }

    [Test]
    public void The_bisect_marks_should_pass_their_option_and_an_error_should_show_on_the_window()
    {
        _viewModel.MarkBisectCommand.Execute(GitBisectOption.Good);
        _window.Received(1).MarkSelectedForBisect(GitBisectOption.Good);

        _window.ShowReflogAsync().Returns(Task.FromException(new InvalidOperationException("no reflog")));
        _viewModel.ShowReflogCommand.Execute(null);

        _window.Received(1).ShowError("no reflog");
    }

    [Test]
    public async Task The_commands_menus_items_should_pass_upstreams_arguments()
    {
        await OpenAsync("aaa1");

        _viewModel.CompressGitDatabaseCommand.Execute(null);
        _viewModel.RepositorySettingsCommand.Execute(null);
        _viewModel.EditMailMapCommand.Execute(null);
        _viewModel.DeleteTagCommand.Execute(null);

        await _window.Received(1).RunGitWithOutputAsync(RepositoryPath, "gc");
        await _window.Received(1).ShowSettingsPageAsync("GitConfigSettingsPage");
        await _window.Received(1).EditRepositoryFileAsync(RepositoryPath, RepositoryTextFiles.MailMap);
        await _window.Received(1).DeleteTagAsync(RepositoryPath);
    }

    [Test]
    public void The_layout_and_tree_toggles_should_store_the_choice_and_show_it()
    {
        _preferences.CommitInfoPosition = GitCommands.CommitInfoPosition.RightwardFromList;
        bool split = _preferences.ShowSplitViewLayout;
        bool tags = _viewModel.ShowsTagsTree;
        List<string?> changed = [];
        _viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        _viewModel.NextCommitInfoPositionCommand.Execute(null);
        _viewModel.ToggleSplitViewCommand.Execute(null);
        _viewModel.ToggleTreeCommand.Execute(RepoObjectKind.TagsRoot);

        _window.Received(1).SetCommitInfoPosition(GitCommands.CommitInfoPosition.BelowList);
        _preferences.ShowSplitViewLayout.Should().Be(!split);
        _window.Received(1).ShowSplitViewLayout();
        _viewModel.ShowsTagsTree.Should().Be(!tags);
        changed.Should().Contain(nameof(BrowseToolbarViewModel.ShowsTagsTree));
        _window.Received(1).ShowRepoObjects();
    }

    [Test]
    public void The_submodules_button_should_go_to_the_superproject_in_a_submodule_and_else_open_its_menu()
    {
        _viewModel.SubmodulesCommand.Execute(null);
        _window.Received(1).ShowSubmodulesMenu();

        SubmoduleProject super = new("super", "/work/", IsCurrent: false, "", "/work/");
        _viewModel.ShowSubmodules(new SubmoduleStructure(super, [], IsBareRepository: false) { SuperProject = super });
        _viewModel.SubmodulesCommand.Execute(null);

        _window.Received(1).OpenSubmoduleProjectAsync(super);
    }

    [Test]
    public async Task The_worktrees_menu_should_check_the_open_one_disable_a_gone_one_and_end_with_upstreams_items()
    {
        await OpenAsync("aaa1");
        IReadOnlyList<MenuEntry> menu = _viewModel.WorktreesMenu(
        [
            new WorktreeInfo(RepositoryPath, "repo", IsMain: true, IsDeleted: false),
            new WorktreeInfo("/work/gone", "gone", IsMain: false, IsDeleted: true),
            new WorktreeInfo("/work/other", "other", IsMain: false, IsDeleted: false),
        ], RepositoryPath);

        menu.Take(3).Select(entry => (entry.IsChecked, entry.IsEnabled)).Should()
            .Equal((true, false), (null, false), (null, true));
        menu[3].IsSeparator.Should().BeTrue();
        menu.Skip(4).Select(entry => entry.Text).Should().Equal(ManageWorktreeDialog.CreateWorktree,
            ManageWorktreeDialog.PruneWorktrees, ManageWorktreeDialog.ManageWorktrees);

        menu[2].Command!.Execute(menu[2].CommandParameter);
        await _window.Received(1).OpenWorktreeFolderAsync("/work/other");
    }

    [Test]
    public async Task The_branches_menu_should_dim_a_branch_the_grid_does_not_show_and_check_out_the_one_clicked()
    {
        await OpenAsync("aaa1");
        IReadOnlyList<MenuEntry> menu = _viewModel.BranchesMenu([("main", "aaa1"), ("old", "fff9")], "_Checkout branch...",
            checkoutGesture: null);

        menu[0].Text.Should().Be("_Checkout branch...");
        menu.Skip(2).Select(entry => (entry.Text, entry.Image, entry.IsDimmed)).Should()
            .Equal(("main", "Branch", false), ("old", "EyeClosed", true));
        menu[3].Command!.Execute(menu[3].CommandParameter);

        await _window.Received(1).CheckoutNamedBranchAsync(RepositoryPath, "old");
    }

    [Test]
    public void The_submodules_menu_should_say_loading_until_the_structure_is_read()
        => _viewModel.SubmodulesMenu(null).Should().BeEmpty("no repository is open");

    [Test]
    public void The_recent_repositories_should_be_numbered_with_clear_list_and_found_by_the_search()
    {
        IReadOnlyList<MenuEntry> menu = _viewModel.RecentRepositoriesMenu(
            [new RepositoryEntry("/work/alpha"), new RepositoryEntry("/work/beta_x")], withClear: true);

        menu.Where(entry => entry.SearchText is not null).Select(entry => entry.Text).Should()
            .Equal("_1: /work/alpha", "_2: /work/beta__x");
        menu[^1].Text.Should().Be(BrowseToolbarViewModel.ClearListText);
        menu[^2].IsSeparator.Should().BeTrue();

        BrowseToolbarViewModel.FilterRepositories(menu, "BETA");
        menu.Where(entry => entry.SearchText is not null).Select(entry => entry.IsVisible).Should().Equal(false, true);
        menu[^1].IsVisible.Should().BeTrue("only the repositories are filtered");

        menu[0].Command!.Execute(menu[0].CommandParameter);
        _window.Received(1).OpenRecentRepository("/work/alpha");
    }

    [Test]
    public async Task The_grid_ref_submenus_should_run_their_action_on_the_ref_clicked()
    {
        await OpenAsync("aaa1");
        GridMenuRef local = new("main", IsRemote: false);
        GridMenuRef remote = new("origin/main", IsRemote: true);
        GridRefMenuEntries entries = _viewModel.GridRefEntries(new GridRefMenus(
            Checkout: [local, null, remote], Push: [local], Merge: [new GridMenuRef("v1", IsRemote: false, "refs/tags/v1")],
            RebaseOnTopOf: null, Rename: [local], Delete: [local, null, remote],
            DeleteTags: [new GridMenuRef("v1", IsRemote: false)], SelectInLeftPanel: [local], IsHeadOfCurrentBranch: false));

        entries.Checkout.Select(entry => entry.IsSeparator).Should().Equal(false, true, false);
        entries.Checkout.Select(entry => entry.Image).Should().Equal("BranchLocal", null, "BranchRemote");
        entries.SelectInLeftPanel.Should().BeEmpty("the item itself selects a single ref");
        foreach (MenuEntry entry in new[]
                 {
                     entries.Checkout[0], entries.Checkout[2], entries.Push[0], entries.Merge[0], entries.Rename[0],
                     entries.Delete[0], entries.Delete[2], entries.DeleteTags[0],
                 })
        {
            entry.Command!.Execute(entry.CommandParameter);
        }

        await _window.Received(1).CheckoutNamedBranchAsync(RepositoryPath, "main");
        await _window.Received(1).CheckoutRemoteBranchAsync(RepositoryPath, "origin/main");
        await _window.Received(1).PushBranchAsync("main");
        await _window.Received(1).MergeBranchAsync(RepositoryPath, "refs/tags/v1");
        await _window.Received(1).RenameBranchAsync("main");
        await _window.Received(1).DeleteBranchesAsync(RepositoryPath,
            Arg.Is<IReadOnlyList<string>>(branches => branches.SequenceEqual(new[] { "main" })));
        await _window.Received(1).DeleteRemoteBranchAsync(RepositoryPath, "origin/main");
        await _window.Received(1).DeleteNamedTagAsync(RepositoryPath, "v1");
    }

    [Test]
    public void Select_in_left_panel_should_list_several_refs_and_do_nothing_without_one()
    {
        GridRefMenuEntries entries = _viewModel.GridRefEntries(new GridRefMenus([], [], [], RebaseOnTopOf: null, [], [], [],
            SelectInLeftPanel: [new GridMenuRef("v1", IsRemote: false), new GridMenuRef("origin/main", IsRemote: true)],
            IsHeadOfCurrentBranch: false));

        entries.SelectInLeftPanel.Select(entry => entry.Text).Should().Equal("v1", "origin/main");
        entries.SelectInLeftPanel[1].Command!.Execute(entries.SelectInLeftPanel[1].CommandParameter);
        _viewModel.SelectInLeftPanelCommand.Execute(null);

        _window.Received(1).SelectInLeftPanel("origin/main", true);
        _window.ReceivedWithAnyArgs(1).SelectInLeftPanel(default!, default);
    }

    [Test]
    public async Task The_stash_items_should_act_on_the_selected_stash_and_drop_it_only_when_confirmed()
    {
        await OpenRowAsync(new CommitRow("s1", "s1", "subject", "author", "2026-10-03 12:00")
        {
            ReflogSelector = "refs/stash@{1}",
        });
        _window.ConfirmDropStashAsync().Returns(false, true);

        _viewModel.ApplySelectedStashCommand.Execute(null);
        _operations.Complete(0);
        _viewModel.PopSelectedStashCommand.Execute(null);
        _operations.Complete(1);
        _viewModel.DropSelectedStashCommand.Execute(null);
        _viewModel.DropSelectedStashCommand.Execute(null);

        Enumerable.Range(0, _operations.Count).Select(index => $"{_operations.NameAt(index)} {_operations.ArgumentsAt(index)}")
            .Should().Equal($"ApplyStash {RepositoryPath} s1", $"PopStash {RepositoryPath} stash@{{1}}",
                $"DropStash {RepositoryPath} stash@{{1}}");
        await _window.Received(2).ConfirmDropStashAsync();
    }

    [Test]
    public void The_copy_entries_should_copy_their_value_and_show_captions_bold_and_disabled()
    {
        CommitRow row = new("aaa1", "aaa1", "subject", "author", "2026-10-03 12:00",
            Labels: [new RefLabel("main", RefKind.Branch)]);

        IReadOnlyList<MenuEntry> entries = BrowseToolbarViewModel.CopyEntries([row], "Branches", "Tags", keep: null,
            _viewModel.CopyTextCommand);

        entries[0].Should().Match<MenuEntry>(entry => entry.Text == "Branches" && entry.IsBold && !entry.IsEnabled);
        entries[1].Should().Match<MenuEntry>(entry => entry.HasAccessKey && !entry.IsBold && entry.IsEnabled);
        entries[1].Command!.Execute(entries[1].CommandParameter);
        entries[0].Command!.Execute(entries[0].CommandParameter);
        _window.Received(1).CopyTextAsync("main");
        _window.ReceivedWithAnyArgs(1).CopyTextAsync(default!);
    }

    [Test]
    public async Task The_user_scripts_and_plugins_should_run_through_the_window()
    {
        ScriptDefinition script = new() { Name = "lint" };
        IGitPlugin plugin = Substitute.For<IGitPlugin>();

        _viewModel.RunUserScriptCommand.Execute(script);
        await _window.DidNotReceiveWithAnyArgs().RunUserScriptAsync(default!, default!);

        await OpenAsync("aaa1");
        _viewModel.RunUserScriptCommand.Execute(script);
        _viewModel.ExecutePluginCommand.Execute(plugin);
        _viewModel.CloneFromHostCommand.Execute("GitHub");

        await _window.Received(1).RunUserScriptAsync(RepositoryPath, script);
        _window.Received(1).ExecutePlugin(plugin);
        _window.Received(1).CloneFromHost("GitHub");
    }

    [Test]
    public void The_grid_hotkey_items_should_run_their_grid_command()
    {
        _viewModel.RunGridCommand.Execute(GridCommand.CompareToBase);
        _viewModel.RewriteCommitHelpCommand.Execute(null);

        _window.Received(1).RunGridCommand(GridCommand.CompareToBase);
        _window.Received(1).ShowUserManual(RewriteCommit.HelpSubfolder, RewriteCommit.HelpAnchor);
    }

    private Task OpenAsync(string hash) => OpenRowAsync(new CommitRow(hash, hash, "subject", "author", "2026-10-03 12:00"));

    private async Task OpenRowAsync(CommitRow row)
    {
        Task open = _commits.OpenAsync(RepositoryPath);
        _history.CompletePage(0, new CommitPage([row], HasMore: false));
        await open;
        _ = _commits.SelectAsync(_commits.Rows[0]);
    }
}

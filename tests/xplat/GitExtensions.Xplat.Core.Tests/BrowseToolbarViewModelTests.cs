using AwesomeAssertions;
using GitCommands.Git;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class BrowseToolbarViewModelTests
{
    private const string RepositoryPath = "/work/repo";

    private FakeRepositoryService _service = null!;
    private RepositoryViewModel _repository = null!;
    private InMemoryAppPreferences _preferences = null!;

    [SetUp]
    public void Setup()
    {
        _service = new FakeRepositoryService();
        _repository = new RepositoryViewModel(_service);
        _preferences = new InMemoryAppPreferences { ShowAheadBehindData = true, ShowGitStatusInBrowseToolbar = true };
    }

    [Test]
    public void Without_a_repository_only_opening_one_should_be_offered_and_only_with_git()
    {
        BrowseToolbarViewModel withGit = NewViewModel(gitAvailable: true);
        BrowseToolbarViewModel withoutGit = NewViewModel(gitAvailable: false);

        withGit.Refresh(null);
        withoutGit.Refresh(null);

        withGit.CanStartRepository.Should().BeTrue();
        withGit.IsOpen.Should().BeFalse();
        withGit.CanPullOrPush.Should().BeFalse();
        withGit.BranchText.Should().BeEmpty();
        withGit.WorkingDirText.Should().BeEmpty();
        withGit.CommitButtonImage.Should().Be(nameof(RepoState.RepoStateClean));
        withoutGit.CanStartRepository.Should().BeFalse("opening a repository needs git");
    }

    [Test]
    public async Task On_a_branch_with_remotes_the_toolstrip_should_offer_pull_and_push_with_the_counts()
    {
        BrowseToolbarViewModel viewModel = NewViewModel();
        await LoadAsync(new RepositorySnapshot("main", [new BranchInfo("main", false, true)],
            [new FileChange("a.txt", ChangeKind.Modified, Staged: false), new FileChange("a.txt", ChangeKind.Modified, Staged: true),
                new FileChange("b.txt", ChangeKind.Added, Staged: true)],
            false, [], [], false, Remotes: ["origin", "fork"], Sync: new SyncStatus(Ahead: 2, Behind: 1)));
        List<string?> changed = [];
        viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        viewModel.Refresh(RepositoryPath);

        changed.Should().Equal(string.Empty);
        viewModel.IsOpen.Should().BeTrue();
        viewModel.CanPullOrPush.Should().BeTrue();
        viewModel.CanOpenPullDialog.Should().BeTrue();
        viewModel.CanOpenPushDialog.Should().BeTrue();
        viewModel.IsFetchAllVisible.Should().BeTrue("upstream shows Fetch all with more than one remote");
        viewModel.BranchText.Should().Be("main");
        viewModel.PushButtonText.Should().Be(new AheadBehindData("main", "", "2", "1").ToDisplay());
        viewModel.PushButtonImage.Should().Be("Unstage", "the branch is behind");
        viewModel.PushButtonTip.Should().Be(
            $"[t]2 new commit(s) will be pushed{Environment.NewLine}[t]1 commit(s) should be integrated (or will be lost if force pushed)",
            "upstream translates the push button's texts");
        viewModel.CommitButtonText.Should().Be("[t]Commit (2)", "upstream counts the changed files once");
        viewModel.CommitButtonImage.Should().Be(RepoStates.Of(_repository.Changes).ToString());
        viewModel.Title.Should().Contain("main");
    }

    [Test]
    public async Task On_a_detached_head_pull_and_push_should_wait_and_the_branch_should_be_upstreams_no_branch()
    {
        BrowseToolbarViewModel viewModel = NewViewModel();
        await LoadAsync(new RepositorySnapshot(null, [], [], false, [], [], false, Remotes: ["origin"]));

        viewModel.Refresh(RepositoryPath);

        viewModel.CanPullOrPush.Should().BeFalse();
        viewModel.CanOpenPullDialog.Should().BeTrue();
        viewModel.CanOpenPushDialog.Should().BeFalse("there is no local branch to push");
        viewModel.IsFetchAllVisible.Should().BeFalse();
        viewModel.BranchText.Should().Be(DetachedHeadParser.DetachedBranch);
        viewModel.PushButtonText.Should().BeNull();
        viewModel.PushButtonTip.Should().Be("[t]Push");
    }

    [Test]
    public async Task Without_the_status_settings_the_buttons_should_show_upstreams_plain_texts()
    {
        _preferences.ShowAheadBehindData = false;
        _preferences.ShowGitStatusInBrowseToolbar = false;
        BrowseToolbarViewModel viewModel = NewViewModel();
        await LoadAsync(new RepositorySnapshot("main", [new BranchInfo("main", false, true)],
            [new FileChange("a.txt", ChangeKind.Modified, Staged: false)], false, [], [], false,
            Sync: new SyncStatus(Ahead: 1, Behind: 0)));

        viewModel.Refresh(RepositoryPath);

        viewModel.PushButtonText.Should().BeNull();
        viewModel.PushButtonImage.Should().Be("Push");
        viewModel.CommitButtonText.Should().Be("[t]Commit");
        viewModel.CommitButtonImage.Should().Be(nameof(RepoState.RepoStateClean));
    }

    [Test]
    public void While_the_repository_is_read_its_commands_should_wait()
    {
        BrowseToolbarViewModel viewModel = NewViewModel();
        _ = _repository.RefreshAsync(RepositoryPath);

        viewModel.Refresh(RepositoryPath);

        viewModel.IsBusy.Should().BeTrue();
        viewModel.IsOpen.Should().BeFalse();
        viewModel.CanLoadMore.Should().BeFalse();
        viewModel.CanStartRepository.Should().BeFalse();
        viewModel.CommitButtonImage.Should().Be(nameof(RepoState.RepoStateUnknown), "the status has not been read yet");
        viewModel.IsCommitSelected.Should().BeFalse();
    }

    [Test]
    public void The_pull_button_should_show_the_default_action_which_its_menu_sets()
    {
        _preferences.DefaultPullAction = GitPullAction.None;
        BrowseToolbarViewModel viewModel = NewViewModel();
        List<string?> changed = [];
        viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        viewModel.PullButtonImage.Should().Be("Pull");
        viewModel.PullButtonTip.Should().Be("[t]Open pull dialog");

        viewModel.SetDefaultPullActionCommand.Execute(GitPullAction.FetchPruneAll);

        _preferences.DefaultPullAction.Should().Be(GitPullAction.FetchPruneAll);
        viewModel.DefaultPullAction.Should().Be(GitPullAction.FetchPruneAll);
        viewModel.PullButtonImage.Should().Be("PullFetchPruneAll");
        viewModel.PullButtonTip.Should().Be("[t]Fetch and prune all");
        changed.Should().Contain([nameof(BrowseToolbarViewModel.DefaultPullAction),
            nameof(BrowseToolbarViewModel.PullButtonImage), nameof(BrowseToolbarViewModel.PullButtonTip)]);
    }

    [Test]
    public void In_a_submodule_the_submodules_button_should_go_up_and_in_a_bare_repository_it_should_be_off()
    {
        BrowseToolbarViewModel viewModel = NewViewModel();
        viewModel.Refresh(RepositoryPath);
        SubmoduleProject super = new("super", "/work/", IsCurrent: false, "", "/work/");
        SubmoduleProject repo = new("repo", RepositoryPath + "/", IsCurrent: true, "repo", "/work/");
        (viewModel.SubmodulesButtonImage, viewModel.CanUseSubmodules).Should().Be(("SubmodulesManage", true),
            "the button works before the submodules are read");

        viewModel.ShowSubmodules(new SubmoduleStructure(super, [repo], IsBareRepository: false) { SuperProject = super });

        viewModel.IsInSubmodule.Should().BeTrue();
        viewModel.SubmodulesButtonImage.Should().Be("NavigateUp");
        viewModel.SubmodulesButtonTip.Should().Be(FakeDialogHost.TranslatedMark + SubmoduleStructure.GoToSuperprojectText);

        viewModel.ShowSubmodules(new SubmoduleStructure(repo, [], IsBareRepository: true));

        viewModel.IsInSubmodule.Should().BeFalse();
        viewModel.SubmodulesButtonTip.Should().Be(FakeDialogHost.TranslatedMark + BrowseToolbarViewModel.SubmodulesText);
        viewModel.CanUseSubmodules.Should().BeFalse();
    }

    [Test]
    public async Task The_status_bar_should_show_the_branch_with_the_rebase_and_the_counts_and_the_stash_count()
    {
        _preferences.ShowStashCount = true;
        BrowseToolbarViewModel viewModel = NewViewModel();
        viewModel.Refresh(null);
        (viewModel.IsRepositoryShown, viewModel.IsStashCountVisible, viewModel.StatusBranchText).Should()
            .Be((false, false, ""));

        await LoadAsync(new RepositorySnapshot("main", [new BranchInfo("main", false, true)], [], false,
            [new StashInfo("stash@{0}", "wip")], [], IsRebasing: true, Sync: new SyncStatus(Ahead: 2, Behind: 1)));
        viewModel.Refresh(RepositoryPath);

        viewModel.IsRepositoryShown.Should().BeTrue();
        viewModel.StatusBranchText.Should().Be("Branch: main" + BrowseToolbarViewModel.RebaseInProgressText
                                               + " [ahead 2, behind 1]");
        (viewModel.IsStashCountVisible, viewModel.StashCountText).Should().Be((true, "Stashes: 1"));
    }

    [Test]
    public void The_grid_status_should_follow_the_grid_without_a_refresh()
    {
        CommitListViewModel commits = new(new FakeCommitHistory());
        BrowseToolbarViewModel viewModel = new(commits, _repository,
            new RepositoryOperationsViewModel(new FakeGitOperations()),
            new RecentRepositoriesViewModel(new InMemoryRecentRepositoryStore()), _preferences, gitAvailable: true);
        List<string?> changed = [];
        viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        _ = commits.OpenAsync(RepositoryPath);

        viewModel.GridStatusText.Should().Be("Loading...");
        changed.Should().Contain(nameof(viewModel.GridStatusText));
        viewModel.HasMoreCommits.Should().BeFalse();
    }

    [Test]
    public async Task A_stopped_rebase_with_conflicts_and_a_bisect_should_show_both_bars_with_their_buttons()
    {
        BrowseToolbarViewModel viewModel = NewViewModel();
        viewModel.Refresh(null);
        (viewModel.ActionBanner, viewModel.BisectBanner, viewModel.IsBisecting).Should().Be((null, null, false));

        await LoadAsync(new RepositorySnapshot("main", [], [new FileChange("a.txt", ChangeKind.Conflict, Staged: false)],
            false, [], [], IsRebasing: true, IsBisecting: true));
        viewModel.Refresh(RepositoryPath);

        GitActionBanner banner = viewModel.ActionBanner!;
        (banner.Action, banner.HasConflicts, banner.Image).Should().Be((GitAction.Rebase, true, "SolveMerge"));
        (viewModel.ShowsResolveButton, viewModel.ShowsContinueButton, viewModel.ShowsAbortButton, viewModel.ShowsMoreButton)
            .Should().Be((true, false, true, true));
        viewModel.BisectBanner!.Image.Should().Be("information");
        viewModel.IsBisecting.Should().BeTrue();
    }

    private async Task LoadAsync(RepositorySnapshot snapshot)
    {
        Task refresh = _repository.RefreshAsync(RepositoryPath);
        _service.Complete(0, snapshot);
        await refresh;
    }

    private BrowseToolbarViewModel NewViewModel(bool gitAvailable = true)
        => new(new CommitListViewModel(new FakeCommitHistory()), _repository,
            new RepositoryOperationsViewModel(new FakeGitOperations()),
            new RecentRepositoriesViewModel(new InMemoryRecentRepositoryStore()), _preferences, gitAvailable,
            text => FakeDialogHost.TranslatedMark + text);
}

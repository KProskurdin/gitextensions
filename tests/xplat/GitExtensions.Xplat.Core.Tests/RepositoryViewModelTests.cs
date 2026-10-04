using AwesomeAssertions;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Repository;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class RepositoryViewModelTests
{
    private FakeRepositoryService _service = null!;
    private RepositoryViewModel _viewModel = null!;

    [SetUp]
    public void Setup()
    {
        _service = new FakeRepositoryService();
        _viewModel = new RepositoryViewModel(_service);
    }

    [Test]
    public async Task RefreshAsync_should_show_branch_branches_and_changes()
    {
        Task refresh = _viewModel.RefreshAsync("/work/one");
        _service.Complete(0, Snapshot("main", new BranchInfo("main", IsRemote: false, IsCurrent: true)));
        await refresh;

        _viewModel.CurrentBranch.Should().Be("main");
        _viewModel.Branches.Should().ContainSingle().Which.Display.Should().Be("* main");
        _viewModel.Changes.Should().ContainSingle().Which.Display.Should().Be("Modified: a.txt");
    }

    [Test]
    public async Task RefreshAsync_with_detached_head_should_show_no_branch()
    {
        Task refresh = _viewModel.RefreshAsync("/work/one");
        _service.Complete(0, Snapshot(null));
        await refresh;

        _viewModel.CurrentBranch.Should().BeEmpty();
    }

    [Test]
    public async Task RefreshAsync_failure_should_set_the_error_and_clear_the_state()
    {
        Task first = _viewModel.RefreshAsync("/work/one");
        _service.Complete(0, Snapshot("main"));
        await first;

        Task second = _viewModel.RefreshAsync("/work/none");
        _service.Fail(1, new InvalidOperationException("Not a git repository: /work/none"));
        await second;

        _viewModel.ErrorMessage.Should().Be("Not a git repository: /work/none");
        _viewModel.CurrentBranch.Should().BeEmpty();
        _viewModel.Changes.Should().BeEmpty();
    }

    [Test]
    public async Task RefreshAsync_should_show_whether_a_merge_is_in_progress()
    {
        Task refresh = _viewModel.RefreshAsync("/work/one");
        _service.Complete(0, new RepositorySnapshot("main", [], [], IsMerging: true));
        await refresh;

        _viewModel.IsMerging.Should().BeTrue();
    }

    [Test]
    public async Task A_newer_refresh_wins_over_an_older_one_that_finishes_later()
    {
        Task older = _viewModel.RefreshAsync("/work/one");
        Task newer = _viewModel.RefreshAsync("/work/two");

        _service.Complete(1, Snapshot("feature"));
        await newer;
        _service.Complete(0, Snapshot("main"));
        await older;

        _viewModel.CurrentBranch.Should().Be("feature");
    }

    [Test]
    public void ToFileChange_should_map_an_untracked_file()
    {
        GitRepositoryService.ToFileChange(new GitItemStatus("new.txt") { IsNew = true })
            .Should().Be(new FileChange("new.txt", ChangeKind.Untracked, Staged: false));
    }

    [Test]
    public void ToFileChange_should_map_a_modified_file_in_the_work_tree()
    {
        GitRepositoryService
            .ToFileChange(new GitItemStatus("a.txt")
            {
                IsTracked = true, IsChanged = true, Staged = StagedStatus.WorkTree
            })
            .Should().Be(new FileChange("a.txt", ChangeKind.Modified, Staged: false));
    }

    [Test]
    public void ToFileChange_should_map_a_file_added_to_the_index()
    {
        GitRepositoryService
            .ToFileChange(new GitItemStatus("b.txt") { IsTracked = true, IsNew = true, Staged = StagedStatus.Index })
            .Should().Be(new FileChange("b.txt", ChangeKind.Added, Staged: true));
    }

    [Test]
    public void ToFileChange_should_map_a_deleted_file()
    {
        GitRepositoryService
            .ToFileChange(new GitItemStatus("c.txt")
            {
                IsTracked = true, IsDeleted = true, Staged = StagedStatus.WorkTree
            })
            .Should().Be(new FileChange("c.txt", ChangeKind.Deleted, Staged: false));
    }

    [Test]
    public void ToFileChange_should_map_a_renamed_file()
    {
        GitRepositoryService
            .ToFileChange(
                new GitItemStatus("d.txt") { IsTracked = true, IsRenamed = true, Staged = StagedStatus.Index })
            .Should().Be(new FileChange("d.txt", ChangeKind.Renamed, Staged: true));
    }

    [Test]
    public void ToFileChange_should_map_an_unmerged_file_to_a_conflict()
    {
        GitRepositoryService
            .ToFileChange(new GitItemStatus("e.txt")
            {
                IsTracked = true, IsUnmerged = true, Staged = StagedStatus.WorkTree
            })
            .Should().Be(new FileChange("e.txt", ChangeKind.Conflict, Staged: false));
    }

    private static RepositorySnapshot Snapshot(string? branch, params BranchInfo[] branches)
        => new(branch, branches, [new FileChange("a.txt", ChangeKind.Modified, Staged: false)]);
}

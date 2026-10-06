using AwesomeAssertions;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Operations;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class RepositoryOperationsViewModelTests
{
    private const string RepositoryPath = "/work/one";

    private FakeGitOperations _git = null!;
    private RepositoryOperationsViewModel _viewModel = null!;
    private List<string> _changedPaths = null!;

    [SetUp]
    public void Setup()
    {
        _git = new FakeGitOperations();
        _viewModel = new RepositoryOperationsViewModel(_git);
        _changedPaths = [];
        _viewModel.RepositoryChanged += (_, e) => _changedPaths.Add(e.RepositoryPath);
    }

    [Test]
    public async Task CommitAsync_runs_the_commit_and_reports_the_repository_changed()
    {
        Task<bool> commit = _viewModel.CommitAsync(RepositoryPath, "add feature", amend: false, signOff: false, author: "");
        _git.NameAt(0).Should().Be("Commit");
        _git.ArgumentsAt(0).Should().Be($"{RepositoryPath} add feature amend=False signOff=False author=");
        _git.Complete(0);

        (await commit).Should().BeTrue();
        _viewModel.StatusMessage.Should().Be("Committed");
        _changedPaths.Should().Equal(RepositoryPath);
        _viewModel.IsBusy.Should().BeFalse();
    }

    [Test]
    public async Task CommitAsync_with_amend_reports_amended()
    {
        Task<bool> commit = _viewModel.CommitAsync(RepositoryPath, "reword", amend: true, signOff: false, author: "");
        _git.Complete(0);

        await commit;
        _viewModel.StatusMessage.Should().Be("Amended");
    }

    [Test]
    public async Task CommitAsync_with_a_blank_message_does_not_run_git()
    {
        bool committed = await _viewModel.CommitAsync(RepositoryPath, "   ", amend: false, signOff: false, author: "");

        committed.Should().BeFalse();
        _git.Count.Should().Be(0);
        _viewModel.ErrorMessage.Should().Be("Enter a commit message.");
        _changedPaths.Should().BeEmpty();
    }

    [Test]
    public async Task A_failing_operation_sets_the_error_and_does_not_report_a_change()
    {
        Task<bool> commit = _viewModel.CommitAsync(RepositoryPath, "nothing staged", amend: false, signOff: false, author: "");
        _git.Fail(0, new GitOperationException("nothing added to commit"));

        (await commit).Should().BeFalse();
        _viewModel.ErrorMessage.Should().Be("nothing added to commit");
        _viewModel.StatusMessage.Should().BeEmpty();
        _changedPaths.Should().BeEmpty();
    }

    [Test]
    public async Task IsBusy_is_true_while_an_operation_is_pending()
    {
        Task<bool> stage = _viewModel.StageAsync(RepositoryPath, ["a.txt"]);
        _viewModel.IsBusy.Should().BeTrue();

        _git.Complete(0);
        await stage;

        _viewModel.IsBusy.Should().BeFalse();
    }

    [Test]
    public async Task StageAsync_and_UnstageAsync_pass_the_paths_through()
    {
        Task stage = _viewModel.StageAsync(RepositoryPath, ["a.txt", "b.txt"]);
        _git.Complete(0);
        await stage;

        Task unstage = _viewModel.UnstageAsync(RepositoryPath, ["a.txt"]);
        _git.Complete(1);
        await unstage;

        _git.ArgumentsAt(0).Should().Be($"{RepositoryPath} a.txt,b.txt");
        _git.ArgumentsAt(1).Should().Be($"{RepositoryPath} a.txt");
        _changedPaths.Should().HaveCount(2);
    }

    [Test]
    public async Task CreateBranchAsync_trims_the_name_and_checks_it_out()
    {
        Task<bool> create = _viewModel.CreateBranchAsync(RepositoryPath, "  feature/x ", checkout: true);
        _git.ArgumentsAt(0).Should().Be($"{RepositoryPath} feature/x checkout=True");
        _git.Complete(0);

        await create;
        _viewModel.StatusMessage.Should().Be("Created feature/x");
    }

    [Test]
    public async Task CreateBranchAsync_with_a_blank_name_does_not_run_git()
    {
        await _viewModel.CreateBranchAsync(RepositoryPath, " ", checkout: true);

        _git.Count.Should().Be(0);
        _viewModel.ErrorMessage.Should().Be("Enter a branch name.");
    }

    [Test]
    public async Task CheckoutRemoteAsync_passes_the_remote_branch_and_reports_the_change()
    {
        Task<bool> checkout = _viewModel.CheckoutRemoteAsync(RepositoryPath, "origin/feature");
        _git.ArgumentsAt(0).Should().Be($"{RepositoryPath} origin/feature");
        _git.Complete(0);

        (await checkout).Should().BeTrue();
        _viewModel.StatusMessage.Should().Be("Checked out origin/feature");
        _changedPaths.Should().Equal(RepositoryPath);
    }

    [Test]
    public async Task FetchAsync_pull_and_push_report_the_repository_changed()
    {
        Task fetch = _viewModel.FetchAsync(RepositoryPath, "origin", prune: false);
        _git.Complete(0);
        await fetch;

        Task pull = _viewModel.PullAsync(RepositoryPath, "origin", "main", rebase: true);
        _git.Complete(1);
        await pull;

        Task push = _viewModel.PushAsync(RepositoryPath, "origin", "main");
        _git.Complete(2);
        await push;

        _git.ArgumentsAt(1).Should().Be($"{RepositoryPath} origin main rebase=True");
        _changedPaths.Should().HaveCount(3);
    }

    [Test]
    public async Task CloneAsync_reports_the_target_path_so_a_view_can_open_it()
    {
        Task<bool> clone = _viewModel.CloneAsync("https://example.com/repo.git", "/work/clone");
        _git.Complete(0);

        (await clone).Should().BeTrue();
        _changedPaths.Should().Equal("/work/clone");
    }

    [Test]
    public async Task CloneAsync_with_a_blank_url_does_not_run_git()
    {
        await _viewModel.CloneAsync("", "/work/clone");

        _git.Count.Should().Be(0);
        _viewModel.ErrorMessage.Should().Be("Enter a repository URL or path to clone.");
    }

    [Test]
    public async Task StashAsync_trims_the_message_and_reports_the_change()
    {
        Task<bool> stash = _viewModel.StashAsync(RepositoryPath, "  work in progress ", includeUntracked: false, keepIndex: false);
        _git.ArgumentsAt(0).Should().Be($"{RepositoryPath} work in progress untracked=False keepIndex=False paths=");
        _git.Complete(0);

        (await stash).Should().BeTrue();
        _viewModel.StatusMessage.Should().Be("Stashed");
        _changedPaths.Should().Equal(RepositoryPath);
    }

    [Test]
    public async Task PopStashAsync_pops_the_named_stash_and_reports_the_change()
    {
        Task<bool> pop = _viewModel.PopStashAsync(RepositoryPath, "stash@{1}");
        _git.ArgumentsAt(0).Should().Be($"{RepositoryPath} stash@{{1}}");
        _git.Complete(0);

        (await pop).Should().BeTrue();
        _viewModel.StatusMessage.Should().Be("Stash popped");
        _changedPaths.Should().Equal(RepositoryPath);
    }

    [Test]
    public async Task ApplyStashAsync_and_DropStashAsync_report_the_change()
    {
        Task<bool> apply = _viewModel.ApplyStashAsync(RepositoryPath, "stash@{0}");
        _git.Complete(0);
        await apply;
        _viewModel.StatusMessage.Should().Be("Stash applied");

        Task<bool> drop = _viewModel.DropStashAsync(RepositoryPath, "stash@{0}");
        _git.NameAt(1).Should().Be("DropStash");
        _git.Complete(1);
        await drop;
        _viewModel.StatusMessage.Should().Be("Stash dropped");
        _changedPaths.Should().HaveCount(2);
    }

    [Test]
    public async Task AddRemoteAsync_rejects_a_blank_url_without_running_git()
    {
        (await _viewModel.AddRemoteAsync(RepositoryPath, "upstream", " ")).Should().BeFalse();

        _git.Count.Should().Be(0);
        _viewModel.ErrorMessage.Should().Be("Enter a remote URL or path.");
    }

    [Test]
    public async Task RenameBranchAsync_rejects_a_blank_name_without_running_git()
    {
        (await _viewModel.RenameBranchAsync(RepositoryPath, "main", " ")).Should().BeFalse();

        _git.Count.Should().Be(0);
        _viewModel.ErrorMessage.Should().Be("Enter a new branch name.");
    }

    [Test]
    public async Task CreateTagAsync_rejects_a_blank_name_without_running_git()
    {
        (await _viewModel.CreateTagAsync(RepositoryPath, "  ", "HEAD", "")).Should().BeFalse();

        _git.Count.Should().Be(0);
        _viewModel.ErrorMessage.Should().Be("Enter a tag name.");
    }

    [Test]
    public async Task CreateTagAsync_and_DeleteTagAsync_report_the_change()
    {
        Task<bool> create = _viewModel.CreateTagAsync(RepositoryPath, " v1.0 ", "abc123", "");
        _git.ArgumentsAt(0).Should().Be($"{RepositoryPath} v1.0 abc123 message=");
        _git.Complete(0);
        await create;
        _viewModel.StatusMessage.Should().Be("Created tag v1.0");

        Task<bool> delete = _viewModel.DeleteTagAsync(RepositoryPath, "v1.0");
        _git.Complete(1);
        await delete;
        _viewModel.StatusMessage.Should().Be("Deleted tag v1.0");
    }

    [Test]
    public async Task RevertAsync_and_RebaseAsync_report_the_change()
    {
        Task<bool> revert = _viewModel.RevertAsync(RepositoryPath, "0123456789abcdef");
        _git.Complete(0);
        await revert;
        _viewModel.StatusMessage.Should().Be("Reverted 01234567");

        Task<bool> rebase = _viewModel.RebaseAsync(RepositoryPath, "main");
        _git.ArgumentsAt(1).Should().Be($"{RepositoryPath} main");
        _git.Complete(1);
        await rebase;
        _viewModel.StatusMessage.Should().Be("Rebased onto main");
    }

    [Test]
    public async Task AbortRebaseAsync_and_ContinueRebaseAsync_report_the_change()
    {
        Task<bool> abort = _viewModel.AbortRebaseAsync(RepositoryPath);
        _git.Complete(0);
        await abort;
        _viewModel.StatusMessage.Should().Be("Rebase aborted");

        Task<bool> continueRebase = _viewModel.ContinueRebaseAsync(RepositoryPath);
        _git.NameAt(1).Should().Be("ContinueRebase");
        _git.Complete(1);
        await continueRebase;
        _viewModel.StatusMessage.Should().Be("Rebase continued");
        _changedPaths.Should().HaveCount(2);
    }

    [Test]
    public async Task CherryPickAsync_and_ResetAsync_name_the_short_commit()
    {
        Task<bool> pick = _viewModel.CherryPickAsync(RepositoryPath, "0123456789abcdef");
        _git.Complete(0);
        await pick;
        _viewModel.StatusMessage.Should().Be("Cherry-picked 01234567");

        Task<bool> reset = _viewModel.ResetAsync(RepositoryPath, "0123456789abcdef", ResetMode.Soft);
        _git.ArgumentsAt(1).Should().Be($"{RepositoryPath} 0123456789abcdef Soft");
        _git.Complete(1);
        await reset;
        _viewModel.StatusMessage.Should().Be("Reset to 01234567");
    }

    [Test]
    public async Task MergeAsync_and_AbortMergeAsync_report_the_change()
    {
        Task<bool> merge = _viewModel.MergeAsync(RepositoryPath, "feature");
        _git.ArgumentsAt(0).Should().Be($"{RepositoryPath} feature");
        _git.Complete(0);
        await merge;
        _viewModel.StatusMessage.Should().Be("Merged feature");

        Task<bool> abort = _viewModel.AbortMergeAsync(RepositoryPath);
        _git.Complete(1);
        await abort;

        _viewModel.StatusMessage.Should().Be("Merge aborted");
        _changedPaths.Should().HaveCount(2);
    }

    [Test]
    public async Task A_failed_merge_sets_the_error_from_git()
    {
        Task<bool> merge = _viewModel.MergeAsync(RepositoryPath, "feature");
        _git.Fail(0, new GitOperationException("Automatic merge failed; fix conflicts and then commit the result."));

        (await merge).Should().BeFalse();
        _viewModel.ErrorMessage.Should().StartWith("Automatic merge failed");
    }

    [Test]
    public async Task ClearError_removes_the_message()
    {
        await _viewModel.CommitAsync(RepositoryPath, "", amend: false, signOff: false, author: "");

        _viewModel.ClearError();

        _viewModel.ErrorMessage.Should().BeNull();
    }
}

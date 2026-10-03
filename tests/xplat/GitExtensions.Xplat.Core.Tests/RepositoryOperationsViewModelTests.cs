using AwesomeAssertions;
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
        Task<bool> commit = _viewModel.CommitAsync(RepositoryPath, "add feature", amend: false);
        _git.NameAt(0).Should().Be("Commit");
        _git.ArgumentsAt(0).Should().Be($"{RepositoryPath} add feature amend=False");
        _git.Complete(0);

        (await commit).Should().BeTrue();
        _viewModel.StatusMessage.Should().Be("Committed");
        _changedPaths.Should().Equal(RepositoryPath);
        _viewModel.IsBusy.Should().BeFalse();
    }

    [Test]
    public async Task CommitAsync_with_amend_reports_amended()
    {
        Task<bool> commit = _viewModel.CommitAsync(RepositoryPath, "reword", amend: true);
        _git.Complete(0);

        await commit;
        _viewModel.StatusMessage.Should().Be("Amended");
    }

    [Test]
    public async Task CommitAsync_with_a_blank_message_does_not_run_git()
    {
        bool committed = await _viewModel.CommitAsync(RepositoryPath, "   ", amend: false);

        committed.Should().BeFalse();
        _git.Count.Should().Be(0);
        _viewModel.ErrorMessage.Should().Be("Enter a commit message.");
        _changedPaths.Should().BeEmpty();
    }

    [Test]
    public async Task A_failing_operation_sets_the_error_and_does_not_report_a_change()
    {
        Task<bool> commit = _viewModel.CommitAsync(RepositoryPath, "nothing staged", amend: false);
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
    public async Task FetchAsync_pull_and_push_report_the_repository_changed()
    {
        Task fetch = _viewModel.FetchAsync(RepositoryPath, "origin");
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
    public async Task ClearError_removes_the_message()
    {
        await _viewModel.CommitAsync(RepositoryPath, "", amend: false);

        _viewModel.ClearError();

        _viewModel.ErrorMessage.Should().BeNull();
    }
}

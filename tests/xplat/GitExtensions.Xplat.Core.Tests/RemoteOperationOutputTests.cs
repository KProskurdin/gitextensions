using AwesomeAssertions;
using GitExtensions.Xplat.Core.Operations;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class RemoteOperationOutputTests
{
    private const string RepositoryPath = "/work/one";

    [Test]
    public void Pump_should_split_lines_and_mark_carriage_return_lines_as_progress()
    {
        List<GitOutputLine> lines = [];
        List<string> errors = [];

        GitOutputRunner.Pump(new StringReader("Counting: 10%\rCounting: 100%, done.\nTo /remote\r\n  main -> main\n"),
            new ImmediateProgress(lines), errors);

        lines.Should().Equal(
            new GitOutputLine("Counting: 10%", IsProgress: true),
            new GitOutputLine("Counting: 100%, done.", IsProgress: false),
            new GitOutputLine("To /remote", IsProgress: false),
            new GitOutputLine("  main -> main", IsProgress: false));
        errors.Should().Equal("Counting: 100%, done.", "To /remote", "  main -> main");
    }

    [Test]
    public void Pump_should_report_a_last_line_without_a_line_end()
    {
        List<GitOutputLine> lines = [];

        GitOutputRunner.Pump(new StringReader("fatal: no remote"), new ImmediateProgress(lines), collected: null);

        lines.Should().Equal(new GitOutputLine("fatal: no remote", IsProgress: false));
    }

    [Test]
    public async Task A_remote_operation_should_announce_itself_and_pass_an_output_and_a_token()
    {
        FakeGitOperations git = new();
        RepositoryOperationsViewModel viewModel = new(git);
        int started = 0;
        viewModel.RemoteOperationStarted += (_, _) => started++;

        Task<bool> push = viewModel.PushAsync(RepositoryPath, "origin", "main");

        started.Should().Be(1);
        viewModel.OutputTitle.Should().Be("Push main to origin");
        viewModel.CanCancel.Should().BeTrue();
        git.OutputAt(0).Should().NotBeNull();
        git.Complete(0);
        (await push).Should().BeTrue();
        viewModel.RemoteState.Should().Be(RemoteOperationState.Succeeded);
        viewModel.CanCancel.Should().BeFalse();
    }

    [Test]
    public async Task Cancel_should_signal_the_token_and_end_as_cancelled_without_an_error()
    {
        FakeGitOperations git = new();
        RepositoryOperationsViewModel viewModel = new(git);
        List<string> reloaded = [];
        viewModel.RepositoryChanged += (_, e) => reloaded.Add(e.RepositoryPath);

        Task<bool> fetch = viewModel.FetchAsync(RepositoryPath, "origin", prune: false);
        viewModel.Cancel();

        git.TokenAt(0).IsCancellationRequested.Should().BeTrue();
        git.Fail(0, new OperationCanceledException());
        (await fetch).Should().BeFalse();
        viewModel.RemoteState.Should().Be(RemoteOperationState.Cancelled);
        viewModel.StatusMessage.Should().Be("Cancelled");
        viewModel.ErrorMessage.Should().BeNull();
        reloaded.Should().Equal(RepositoryPath);
    }

    [Test]
    public async Task A_failed_remote_operation_should_end_as_failed_with_the_error()
    {
        FakeGitOperations git = new();
        RepositoryOperationsViewModel viewModel = new(git);

        Task<bool> pull = viewModel.PullAsync(RepositoryPath, "origin", "main", rebase: false);
        git.Fail(0, new GitOperationException("fatal: could not read from remote"));

        (await pull).Should().BeFalse();
        viewModel.RemoteState.Should().Be(RemoteOperationState.Failed);
        viewModel.ErrorMessage.Should().Be("fatal: could not read from remote");
    }

    [Test]
    public async Task Output_should_replace_a_progress_line_with_the_line_after_it()
    {
        FakeGitOperations git = new();
        RepositoryOperationsViewModel viewModel = new(git);
        Task<bool> clone = viewModel.CloneAsync("https://example.com/repo.git", "/work/repo");
        IProgress<GitOutputLine> output = git.OutputAt(0)!;

        output.Report(new GitOutputLine("Cloning into 'repo'...", IsProgress: false));
        output.Report(new GitOutputLine("Receiving objects:  50%", IsProgress: true));
        output.Report(new GitOutputLine("Receiving objects: 100%, done.", IsProgress: false));
        git.Complete(0);
        await clone;

        viewModel.OutputLines.Should().Equal("Cloning into 'repo'...", "Receiving objects: 100%, done.");
    }

    [Test]
    public async Task A_push_request_without_a_remote_is_rejected_before_git_runs()
    {
        FakeGitOperations git = new();
        RepositoryOperationsViewModel viewModel = new(git);

        bool pushed = await viewModel.PushAsync(RepositoryPath, new PushRequest("", "main", "", GitCommands.Git.ForcePushOptions.DoNotForce, Track: false));

        pushed.Should().BeFalse();
        git.Count.Should().Be(0);
        viewModel.ErrorMessage.Should().Be("Choose a remote and a branch to push.");
    }

    [Test]
    public async Task A_fetch_only_pull_request_is_titled_and_reported_as_a_fetch()
    {
        FakeGitOperations git = new();
        RepositoryOperationsViewModel viewModel = new(git);

        Task<bool> fetch = viewModel.PullAsync(RepositoryPath, new PullRequest("origin", "main", PullAction.FetchOnly, Prune: true, AutoStash: false));
        viewModel.OutputTitle.Should().Be("Fetch origin/main");
        git.NameAt(0).Should().Be("PullRequest");
        git.Complete(0);

        (await fetch).Should().BeTrue();
        viewModel.StatusMessage.Should().Be("Fetched");
    }

    private sealed class ImmediateProgress(List<GitOutputLine> lines) : IProgress<GitOutputLine>
    {
        public void Report(GitOutputLine value) => lines.Add(value);
    }
}

using Avalonia.Headless.NUnit;
using GitExtensions.Xplat.Core.Operations;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

// Real git writes need ThreadHelper.JoinableTaskContext, which the app sets at startup; AvaloniaTest runs the app.
internal sealed class GitOperationsTests
{
    private TestRepository _repo = null!;
    private GitOperations _operations = null!;
    private readonly List<string> _folders = [];

    [SetUp]
    public void Setup()
    {
        _repo = new TestRepository();
        _operations = new GitOperations();
    }

    [TearDown]
    public void TearDown()
    {
        _repo.Dispose();
        foreach (string folder in _folders)
        {
            GitProcess.DeleteFolder(folder);
        }
    }

    [AvaloniaTest]
    public void Stage_and_commit_add_a_commit_with_the_message()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "new.txt"), "content");

        Wait(_operations.StageAsync(_repo.Path, ["new.txt"]));
        Wait(_operations.CommitAsync(_repo.Path, "add new", amend: false));

        _repo.Run("log", "-1", "--format=%s").Trim().Should().Be("add new");
        _repo.Run("rev-list", "--count", "HEAD").Trim().Should().Be("3");
    }

    [AvaloniaTest]
    public void Commit_with_amend_rewrites_the_last_commit_message()
    {
        Wait(_operations.CommitAsync(_repo.Path, "reworded", amend: true));

        _repo.Run("log", "-1", "--format=%s").Trim().Should().Be("reworded");
        _repo.Run("rev-list", "--count", "HEAD").Trim().Should().Be("2");
    }

    [AvaloniaTest]
    public void Commit_without_staged_changes_throws_with_git_message()
    {
        Func<Task> commit = () => _operations.CommitAsync(_repo.Path, "nothing", amend: false);

        commit.Should().ThrowAsync<GitOperationException>();
    }

    [AvaloniaTest]
    public void Unstage_takes_a_staged_file_out_of_the_index()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "new.txt"), "content");
        Wait(_operations.StageAsync(_repo.Path, ["new.txt"]));

        Wait(_operations.UnstageAsync(_repo.Path, ["new.txt"]));

        _repo.Run("diff", "--cached", "--name-only").Trim().Should().BeEmpty();
    }

    [AvaloniaTest]
    public void CreateBranch_with_checkout_switches_to_the_new_branch()
    {
        Wait(_operations.CreateBranchAsync(_repo.Path, "feature/x", checkout: true));

        _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim().Should().Be("feature/x");
    }

    [AvaloniaTest]
    public void Checkout_returns_to_an_existing_branch()
    {
        string original = _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim();
        Wait(_operations.CreateBranchAsync(_repo.Path, "feature/x", checkout: true));

        Wait(_operations.CheckoutAsync(_repo.Path, original));

        _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim().Should().Be(original);
    }

    [AvaloniaTest]
    public void DeleteBranch_removes_a_branch_that_is_not_checked_out()
    {
        Wait(_operations.CreateBranchAsync(_repo.Path, "feature/x", checkout: false));

        Wait(_operations.DeleteBranchAsync(_repo.Path, "feature/x", force: false));

        _repo.Run("branch", "--list", "feature/x").Trim().Should().BeEmpty();
    }

    [AvaloniaTest]
    public void DeleteBranch_of_the_checked_out_branch_throws()
    {
        string current = _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim();

        Func<Task> delete = () => _operations.DeleteBranchAsync(_repo.Path, current, force: false);

        delete.Should().ThrowAsync<GitOperationException>();
    }

    [AvaloniaTest]
    public void Push_publishes_the_current_branch_to_the_remote()
    {
        string remote = CreateBareRemote();
        _repo.Run("remote", "add", "origin", remote);
        string current = _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim();

        Wait(_operations.PushAsync(_repo.Path, "origin", current));

        GitProcess.Run(remote, "rev-parse", current).Trim().Should().Be(_repo.Run("rev-parse", "HEAD").Trim());
    }

    [AvaloniaTest]
    public void Clone_copies_the_remote_history()
    {
        string remote = CreateBareRemote();
        _repo.Run("remote", "add", "origin", remote);
        Wait(_operations.PushAsync(_repo.Path, "origin", _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim()));
        string target = NewFolder();

        Wait(_operations.CloneAsync(remote, target));

        GitProcess.Run(target, "log", "-1", "--format=%s").Trim().Should().Be("second");
    }

    [AvaloniaTest]
    public void Pull_fast_forwards_to_the_remote_commit()
    {
        string remote = CreateBareRemote();
        _repo.Run("remote", "add", "origin", remote);
        string current = _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim();
        Wait(_operations.PushAsync(_repo.Path, "origin", current));
        string target = NewFolder();
        Wait(_operations.CloneAsync(remote, target));

        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "three");
        _repo.Run("commit", "-q", "-am", "third");
        Wait(_operations.PushAsync(_repo.Path, "origin", current));

        Wait(_operations.PullAsync(target, "origin", current, rebase: false));

        GitProcess.Run(target, "log", "-1", "--format=%s").Trim().Should().Be("third");
    }

    [AvaloniaTest]
    public void CheckoutRemote_creates_a_tracking_branch_with_the_remote_name()
    {
        string remote = CreateBareRemote();
        _repo.Run("remote", "add", "origin", remote);
        _repo.Run("branch", "feature");
        Wait(_operations.PushAsync(_repo.Path, "origin", "feature"));
        _repo.Run("checkout", "-q", "--detach");
        _repo.Run("branch", "-D", "feature");

        Wait(_operations.CheckoutRemoteAsync(_repo.Path, "origin/feature"));

        _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim().Should().Be("feature");
        _repo.Run("config", "branch.feature.remote").Trim().Should().Be("origin");
    }

    private static void Wait(Task task) => task.GetAwaiter().GetResult();

    private string CreateBareRemote()
    {
        string remote = NewFolder();
        GitProcess.Run(remote, "init", "--bare", "-q");
        return remote;
    }

    private string NewFolder()
    {
        string folder = Path.Combine(Path.GetTempPath(), "xplat-git-" + Guid.NewGuid().ToString("N"));
        _folders.Add(folder);
        Directory.CreateDirectory(folder);
        return folder;
    }
}

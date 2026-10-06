using Avalonia.Headless.NUnit;
using GitExtensions.Xplat.Core.Repository;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

// Real git reads need ThreadHelper.JoinableTaskContext, which the app sets at startup; AvaloniaTest runs the app.
internal sealed class RepositoryServiceTests
{
    private TestRepository _repo = null!;
    private GitRepositoryService _service = null!;

    [SetUp]
    public void Setup()
    {
        _repo = new TestRepository();
        _service = new GitRepositoryService();
    }

    [TearDown]
    public void TearDown()
    {
        _repo.Dispose();
    }

    [AvaloniaTest]
    public void GetSnapshot_should_report_the_current_branch_and_mark_it()
    {
        string current = _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim();

        RepositorySnapshot snapshot = _service.GetSnapshotAsync(_repo.Path).GetAwaiter().GetResult();

        snapshot.CurrentBranch.Should().Be(current);
        snapshot.Branches.Should().Contain(branch => branch.Name == current && branch.IsCurrent && !branch.IsRemote);
    }

    [AvaloniaTest]
    public void GetSnapshot_should_list_an_untracked_file_as_untracked_and_unstaged()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "new.txt"), "content");

        RepositorySnapshot snapshot = _service.GetSnapshotAsync(_repo.Path).GetAwaiter().GetResult();

        snapshot.Changes.Should().ContainSingle()
            .Which.Should().Be(new FileChange("new.txt", ChangeKind.Untracked, Staged: false));
    }

    [AvaloniaTest]
    public void GetSnapshot_should_report_a_staged_new_file_as_added_and_staged()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "new.txt"), "content");
        _repo.Run("add", "new.txt");

        RepositorySnapshot snapshot = _service.GetSnapshotAsync(_repo.Path).GetAwaiter().GetResult();

        snapshot.Changes.Should().ContainSingle()
            .Which.Should().Be(new FileChange("new.txt", ChangeKind.Added, Staged: true));
    }

    [AvaloniaTest]
    public void GetSnapshot_should_list_the_stashes_and_tags()
    {
        _repo.Run("tag", "v1.0");
        _repo.Run("tag", "v0.9");
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "changed");
        _repo.Run("stash", "push", "-q", "-m", "work in progress");

        RepositorySnapshot snapshot = _service.GetSnapshotAsync(_repo.Path).GetAwaiter().GetResult();

        StashInfo stash = snapshot.Stashes.Should().ContainSingle().Which;
        stash.Name.Should().Be("stash@{0}");
        stash.Message.Should().EndWith("work in progress");
        snapshot.Tags.Should().Equal("v0.9", "v1.0");
    }

    [AvaloniaTest]
    public void GetSnapshot_should_count_the_commits_ahead_of_the_upstream()
    {
        string remote = Path.Combine(Path.GetTempPath(), "xplat-remote-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(remote);
        try
        {
            GitProcess.Run(remote, "init", "--bare", "-q");
            _repo.Run("remote", "add", "origin", remote);
            _repo.Run("push", "-q", "-u", "origin", _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim());
            File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "local");
            _repo.Run("commit", "-q", "-am", "local");

            RepositorySnapshot snapshot = _service.GetSnapshotAsync(_repo.Path).GetAwaiter().GetResult();

            snapshot.Sync.Should().Be(new SyncStatus(1, 0));
            snapshot.TrackingRemote.Should().Be("origin");
        }
        finally
        {
            GitProcess.DeleteFolder(remote);
        }
    }

    [AvaloniaTest]
    public void GetSnapshot_should_report_no_sync_status_without_an_upstream()
    {
        RepositorySnapshot snapshot = _service.GetSnapshotAsync(_repo.Path).GetAwaiter().GetResult();

        snapshot.Sync.Should().BeNull();
        snapshot.TrackingRemote.Should().BeNull();
    }

    [AvaloniaTest]
    public void GetSnapshot_should_fail_for_a_folder_that_is_not_a_repository()
    {
        string folder = Path.Combine(Path.GetTempPath(), "xplat-not-a-repo-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            Func<Task> read = () => _service.GetSnapshotAsync(folder);

            Action act = () => read().GetAwaiter().GetResult();
            act.Should().Throw<InvalidOperationException>().WithMessage("Not a git repository: *");
        }
        finally
        {
            Directory.Delete(folder);
        }
    }
}

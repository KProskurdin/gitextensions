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
    public void SubmoduleChanges_should_list_the_commits_a_staged_submodule_change_brings_in()
    {
        // git refuses local-path submodules unless the file protocol is allowed; this is set for this test's processes only.
        Environment.SetEnvironmentVariable("GIT_CONFIG_COUNT", "1");
        Environment.SetEnvironmentVariable("GIT_CONFIG_KEY_0", "protocol.file.allow");
        Environment.SetEnvironmentVariable("GIT_CONFIG_VALUE_0", "always");
        using TestRepository library = new();
        try
        {
            _repo.Run("submodule", "add", "-q", library.Path, "lib");
            _repo.Run("commit", "-q", "-m", "add submodule");
            string lib = Path.Combine(_repo.Path, "lib");
            GitProcess.Run(lib, "-c", "user.name=Test", "-c", "user.email=test@example.com", "commit", "-q",
                "--allow-empty", "-m", "third");
            string third = GitProcess.Run(lib, "log", "-1", "--format=%h").Trim();
            _repo.Run("add", "lib");

            string message = PreviousCommitMessages.SubmoduleChangesAsync(_repo.Path, ["lib", "a.txt"])
                .GetAwaiter().GetResult();

            message.ReplaceLineEndings("\n").Should()
                .Be($"Submodule lib updated\n\nSubmodule lib:\n    > {third} - third");
            PreviousCommitMessages.SubmoduleChangesAsync(_repo.Path, ["a.txt"]).GetAwaiter().GetResult().Should()
                .BeEmpty();
        }
        finally
        {
            Environment.SetEnvironmentVariable("GIT_CONFIG_COUNT", null);
            Environment.SetEnvironmentVariable("GIT_CONFIG_KEY_0", null);
            Environment.SetEnvironmentVariable("GIT_CONFIG_VALUE_0", null);
        }
    }

    [AvaloniaTest]
    public void PreviousMessages_should_read_the_newest_first_and_only_the_users_own_when_asked()
    {
        _repo.Run("-c", "user.name=Other", "-c", "user.email=other@example.com", "commit", "-q", "--allow-empty", "-m",
            "by other");

        PreviousCommitMessages.ReadAsync(_repo.Path, 2, onlyMine: false).GetAwaiter().GetResult().Should()
            .Equal("by other", "second\n\nbody line");
        PreviousCommitMessages.ReadAsync(_repo.Path, 5, onlyMine: true).GetAwaiter().GetResult().Should()
            .Equal("second\n\nbody line", "first");
    }

    [AvaloniaTest]
    public void CurrentBranch_should_read_the_checked_out_branch_and_nothing_outside_a_repository()
    {
        string current = _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim();
        string folder = Path.Combine(Path.GetTempPath(), "xplat-not-a-repo-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            RecentRepositoryBranches.CurrentBranch(_repo.Path).Should().Be(current);
            RecentRepositoryBranches.CurrentBranch(folder).Should().BeNull();
            RecentRepositoryBranches.CurrentBranch(Path.Combine(folder, "gone")).Should().BeNull();
        }
        finally
        {
            Directory.Delete(folder);
        }
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

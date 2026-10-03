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
    public void GetSnapshot_should_fail_for_a_folder_that_is_not_a_repository()
    {
        string folder = Path.Combine(Path.GetTempPath(), "xplat-not-a-repo-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            Func<Task> read = () => _service.GetSnapshotAsync(folder);

            read.Should().ThrowAsync<InvalidOperationException>().WithMessage("Not a git repository: *");
        }
        finally
        {
            Directory.Delete(folder);
        }
    }
}

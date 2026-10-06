using Avalonia.Headless.NUnit;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Diff;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

// Real git reads need ThreadHelper.JoinableTaskContext, which the app sets at startup; AvaloniaTest runs the app.
internal sealed class DiffServiceTests
{
    private TestRepository _repo = null!;
    private GitDiffService _diff = null!;

    [SetUp]
    public void Setup()
    {
        _repo = new TestRepository();
        _diff = new GitDiffService();
    }

    [TearDown]
    public void TearDown()
    {
        _repo.Dispose();
    }

    [AvaloniaTest]
    public void GetDiff_can_ignore_whitespace_only_changes()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "two   ");

        IReadOnlyList<DiffLine> withWhitespace = _diff.GetDiffAsync(_repo.Path, null, "a.txt", staged: false).GetAwaiter().GetResult();
        IReadOnlyList<DiffLine> ignoringWhitespace = _diff.GetDiffAsync(_repo.Path, null, "a.txt", staged: false, ignoreWhitespace: true).GetAwaiter().GetResult();

        withWhitespace.Should().NotBeEmpty();
        ignoringWhitespace.Should().BeEmpty();
    }

    [AvaloniaTest]
    public void GetDiff_of_an_untracked_file_shows_its_content_as_added()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "new.txt"), "content");

        IReadOnlyList<DiffLine> lines = _diff.GetDiffAsync(_repo.Path, null, "new.txt", staged: false).GetAwaiter().GetResult();

        lines.Should().Contain(line => line.Kind == DiffLineKind.Added && line.Text == "+content");
    }

    [AvaloniaTest]
    public void GetDiff_of_an_unchanged_tracked_file_is_empty()
    {
        IReadOnlyList<DiffLine> lines = _diff.GetDiffAsync(_repo.Path, null, "a.txt", staged: false).GetAwaiter().GetResult();

        lines.Should().BeEmpty();
    }

    [AvaloniaTest]
    public void GetDiff_without_a_file_shows_the_whole_stash()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "changed");
        _repo.Run("stash", "push", "-q");

        IReadOnlyList<DiffLine> lines = _diff.GetDiffAsync(_repo.Path, "stash@{0}", filePath: null, staged: false).GetAwaiter().GetResult();

        lines.Should().Contain(line => line.Kind == DiffLineKind.Added && line.Text == "+changed");
    }

    [AvaloniaTest]
    public void GetDiff_for_a_commit_shows_what_it_changed()
    {
        string head = _repo.Run("rev-parse", "HEAD").Trim();

        IReadOnlyList<DiffLine> lines = _diff.GetDiffAsync(_repo.Path, head, "a.txt", staged: false).GetAwaiter().GetResult();

        lines.Should().Contain(line => line.Kind == DiffLineKind.Removed && line.Text == "-one");
        lines.Should().Contain(line => line.Kind == DiffLineKind.Added && line.Text == "+two");
    }

    [AvaloniaTest]
    public void GetDiff_of_the_working_tree_shows_unstaged_changes()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "changed");

        IReadOnlyList<DiffLine> lines = _diff.GetDiffAsync(_repo.Path, commitHash: null, "a.txt", staged: false).GetAwaiter().GetResult();

        lines.Should().Contain(line => line.Kind == DiffLineKind.Added && line.Text == "+changed");
    }

    [AvaloniaTest]
    public void GetDiff_of_the_index_shows_staged_changes_only()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "staged");
        _repo.Run("add", "a.txt");

        IReadOnlyList<DiffLine> staged = _diff.GetDiffAsync(_repo.Path, commitHash: null, "a.txt", staged: true).GetAwaiter().GetResult();
        IReadOnlyList<DiffLine> unstaged = _diff.GetDiffAsync(_repo.Path, commitHash: null, "a.txt", staged: false).GetAwaiter().GetResult();

        staged.Should().Contain(line => line.Text == "+staged");
        unstaged.Should().BeEmpty();
    }

    [AvaloniaTest]
    public void LoadFiles_lists_the_files_a_commit_changed()
    {
        string head = _repo.Run("rev-parse", "HEAD").Trim();

        IReadOnlyList<CommitFile> files = new GitCommitHistory().LoadFilesAsync(_repo.Path, head).GetAwaiter().GetResult();

        files.Should().ContainSingle().Which.Display.Should().Be("M a.txt");
    }
}

using Avalonia.Headless.NUnit;
using GitCommands.Settings;
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
    public async Task GetDiff_can_ignore_whitespace_only_changes()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "two   ");

        IReadOnlyList<DiffLine> withWhitespace =
            await _diff.GetDiffAsync(_repo.Path, null, "a.txt", staged: false);
        IReadOnlyList<DiffLine> ignoringWhitespace = await _diff.GetDiffAsync(_repo.Path, null, "a.txt", staged: false,
            new DiffOptions(IgnoreWhitespaceKind.AllSpace));

        withWhitespace.Should().NotBeEmpty();
        ignoringWhitespace.Should().BeEmpty();
    }

    [AvaloniaTest]
    public void GetDiff_follows_the_whitespace_kind_and_the_context_as_upstreams_file_viewer()
    {
        string[] original = [.. Enumerable.Range(1, 20).Select(i => $"line {i}")];
        File.WriteAllLines(Path.Combine(_repo.Path, "a.txt"), original);
        _repo.Run("commit", "-q", "-am", "twenty lines");
        string[] changed = [.. original];
        changed[9] = "line 10   ";
        changed[10] = "line  11";
        File.WriteAllLines(Path.Combine(_repo.Path, "a.txt"), changed);

        // At end of line only the trailing spaces are ignored; a change in amount ignores both edits.
        Changed(new DiffOptions(IgnoreWhitespaceKind.Eol)).Should().Be(1);
        Changed(new DiffOptions(IgnoreWhitespaceKind.Change)).Should().Be(0);
        Changed(new DiffOptions()).Should().Be(2);

        // git's default context is 3 lines on each side; none, more, and the entire file follow the options.
        Context(new DiffOptions()).Should().Be(6);
        Context(new DiffOptions(ContextLines: 0)).Should().Be(0);
        Context(new DiffOptions().WithMoreContext()).Should().Be(8);
        Context(new DiffOptions(ShowEntireFile: true)).Should().Be(18);

        return;

        int Changed(DiffOptions options) => Diff(options).Count(line => line.Kind == DiffLineKind.Added);

        int Context(DiffOptions options) => Diff(options).Count(line => line.Kind == DiffLineKind.Context);

        IReadOnlyList<DiffLine> Diff(DiffOptions options)
            => GitUI.ThreadHelper.JoinableTaskFactory.Run(
                () => _diff.GetDiffAsync(_repo.Path, null, "a.txt", staged: false, options));
    }

    [AvaloniaTest]
    public async Task GetDiff_of_an_untracked_file_shows_its_content_as_added()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "new.txt"), "content");

        IReadOnlyList<DiffLine> lines = await _diff.GetDiffAsync(_repo.Path, null, "new.txt", staged: false);

        lines.Should().Contain(line => line.Kind == DiffLineKind.Added && line.Text == "+content");
    }

    [AvaloniaTest]
    public async Task GetDiff_of_an_unchanged_tracked_file_is_empty()
    {
        IReadOnlyList<DiffLine> lines = await _diff.GetDiffAsync(_repo.Path, null, "a.txt", staged: false);

        lines.Should().BeEmpty();
    }

    [AvaloniaTest]
    public async Task GetDiff_without_a_file_shows_the_whole_stash()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "changed");
        _repo.Run("stash", "push", "-q");

        IReadOnlyList<DiffLine> lines = await _diff.GetDiffAsync(_repo.Path, "stash@{0}", filePath: null, staged: false);

        lines.Should().Contain(line => line.Kind == DiffLineKind.Added && line.Text == "+changed");
    }

    [AvaloniaTest]
    public async Task GetDiff_for_a_commit_shows_what_it_changed()
    {
        string head = _repo.Run("rev-parse", "HEAD").Trim();

        IReadOnlyList<DiffLine> lines = await _diff.GetDiffAsync(_repo.Path, head, "a.txt", staged: false);

        lines.Should().Contain(line => line.Kind == DiffLineKind.Removed && line.Text == "-one");
        lines.Should().Contain(line => line.Kind == DiffLineKind.Added && line.Text == "+two");
    }

    [AvaloniaTest]
    public async Task GetDiff_of_the_working_tree_shows_unstaged_changes()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "changed");

        IReadOnlyList<DiffLine> lines = await _diff.GetDiffAsync(_repo.Path, commitHash: null, "a.txt", staged: false);

        lines.Should().Contain(line => line.Kind == DiffLineKind.Added && line.Text == "+changed");
    }

    [AvaloniaTest]
    public async Task GetDiff_of_the_index_shows_staged_changes_only()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "staged");
        _repo.Run("add", "a.txt");

        IReadOnlyList<DiffLine> staged = await _diff.GetDiffAsync(_repo.Path, commitHash: null, "a.txt", staged: true);
        IReadOnlyList<DiffLine> unstaged = await _diff.GetDiffAsync(_repo.Path, commitHash: null, "a.txt", staged: false);

        staged.Should().Contain(line => line.Text == "+staged");
        unstaged.Should().BeEmpty();
    }

    [AvaloniaTest]
    public async Task LoadFiles_lists_the_files_a_commit_changed()
    {
        string head = _repo.Run("rev-parse", "HEAD").Trim();

        IReadOnlyList<CommitFile> files = await new GitCommitHistory().LoadFilesAsync(_repo.Path, head);

        files.Should().ContainSingle().Which.Display.Should().Be("M a.txt");
    }
}

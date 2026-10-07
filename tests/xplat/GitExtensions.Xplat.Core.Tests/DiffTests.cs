using AwesomeAssertions;
using GitExtensions.Xplat.Core.Diff;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class DiffTests
{
    private const string SampleDiff =
        "diff --git a/a.txt b/a.txt\n" +
        "index 1111111..2222222 100644\n" +
        "--- a/a.txt\n" +
        "+++ b/a.txt\n" +
        "@@ -1 +1 @@\n" +
        "-one\n" +
        "+two\n" +
        " unchanged\n";

    [Test]
    public void Parse_should_classify_each_line_of_a_unified_diff()
    {
        IReadOnlyList<DiffLine> lines = DiffParser.Parse(SampleDiff);

        lines.Select(line => line.Kind).Should().Equal(
            DiffLineKind.Header, DiffLineKind.Header, DiffLineKind.Header, DiffLineKind.Header,
            DiffLineKind.Hunk, DiffLineKind.Removed, DiffLineKind.Added, DiffLineKind.Context);
    }

    [Test]
    public void Parse_should_drop_the_empty_line_after_the_last_newline()
    {
        DiffParser.Parse(SampleDiff).Should().HaveCount(8);
    }

    [Test]
    public void Parse_should_strip_carriage_returns()
    {
        DiffParser.Parse("+added\r\n").Should().ContainSingle()
            .Which.Text.Should().Be("+added");
    }

    [Test]
    public void Parse_should_number_the_lines_of_each_hunk_in_the_old_and_new_file()
    {
        IReadOnlyList<DiffLine> lines = DiffParser.Parse(
            "--- a/f\n+++ b/f\n@@ -10,3 +10,3 @@ section\n keep\n-old\n+new\n keep2\n@@ -40 +40,2 @@\n x\n+y\n");

        lines.Where(line => line.Kind is not (DiffLineKind.Header or DiffLineKind.Hunk))
            .Select(line => (line.Text, line.OldNumber, line.NewNumber))
            .Should().Equal(
                (" keep", 10, 10), ("-old", 11, (int?)null), ("+new", (int?)null, 11), (" keep2", 12, 12),
                (" x", 40, 40), ("+y", (int?)null, 41));
    }

    [Test]
    public void Parse_should_read_lines_inside_a_hunk_by_their_first_character_only()
    {
        IReadOnlyList<DiffLine> lines =
            DiffParser.Parse("@@ -1,2 +1,2 @@\n--- a removed SQL comment\n+++ an added line\n-x\n+y\n");

        lines[1].Kind.Should().Be(DiffLineKind.Removed);
        lines[2].Kind.Should().Be(DiffLineKind.Added);
    }

    [Test]
    public void Parse_should_keep_the_no_newline_marker_without_numbers()
    {
        IReadOnlyList<DiffLine> lines = DiffParser.Parse("@@ -1 +1 @@\n-a\n\\ No newline at end of file\n+b\n");

        lines[2].Should().Be(new DiffLine("\\ No newline at end of file", DiffLineKind.Header));
        lines[3].Should().Be(new DiffLine("+b", DiffLineKind.Added, NewNumber: 1));
    }

    [Test]
    public async Task DiffViewModel_LoadAsync_should_show_the_classified_lines()
    {
        FakeDiffService diff = new(DiffParser.Parse(SampleDiff));
        DiffViewModel viewModel = new(diff);

        await viewModel.LoadAsync("/work/repo", commitHash: "abc", filePath: "a.txt", staged: false);

        viewModel.Lines.Should().HaveCount(8);
        diff.Requests.Should().Equal("/work/repo abc a.txt staged=False");
        viewModel.IsLoading.Should().BeFalse();
    }

    [Test]
    public async Task DiffViewModel_LoadAsync_failure_should_set_the_error()
    {
        DiffViewModel viewModel = new(new FakeDiffService(new InvalidOperationException("no such file")));

        await viewModel.LoadAsync("/work/repo", commitHash: null, filePath: "gone.txt", staged: false);

        viewModel.ErrorMessage.Should().Be("no such file");
        viewModel.Lines.Should().BeEmpty();
    }

    private sealed class FakeDiffService : IDiffService
    {
        private readonly IReadOnlyList<DiffLine> _lines;
        private readonly Exception? _failure;

        public FakeDiffService(IReadOnlyList<DiffLine> lines)
        {
            _lines = lines;
        }

        public FakeDiffService(Exception failure)
        {
            _lines = [];
            _failure = failure;
        }

        public List<string> Requests { get; } = [];

        public Task<IReadOnlyList<DiffLine>> GetDiffAsync(string repositoryPath, string? commitHash, string? filePath,
            bool staged, DiffOptions? options = null)
        {
            Requests.Add($"{repositoryPath} {commitHash} {filePath} staged={staged}");
            return _failure is null ? Task.FromResult(_lines) : Task.FromException<IReadOnlyList<DiffLine>>(_failure);
        }
    }

    [Test]
    public void FindChange_should_move_between_the_first_lines_of_the_changes()
    {
        IReadOnlyList<DiffLine> lines =
        [
            new("@@ -1,5 +1,5 @@", DiffLineKind.Hunk),
            new(" a", DiffLineKind.Context),
            new("-b", DiffLineKind.Removed),
            new("+B", DiffLineKind.Added),
            new(" c", DiffLineKind.Context),
            new("+d", DiffLineKind.Added),
        ];

        DiffNavigation.FindChange(lines, from: -1, forward: true).Should().Be(2);
        DiffNavigation.FindChange(lines, from: 2, forward: true).Should().Be(5, "the added line belongs to the same change");
        DiffNavigation.FindChange(lines, from: 5, forward: true).Should().BeNull();
        DiffNavigation.FindChange(lines, from: 5, forward: false).Should().Be(2);
        DiffNavigation.FindChange(lines, from: 2, forward: false).Should().BeNull();
        DiffNavigation.FindChange(lines, from: -1, forward: false).Should().Be(5);
    }
}

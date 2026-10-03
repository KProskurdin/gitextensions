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

        public Task<IReadOnlyList<DiffLine>> GetDiffAsync(string repositoryPath, string? commitHash, string filePath, bool staged)
        {
            Requests.Add($"{repositoryPath} {commitHash} {filePath} staged={staged}");
            return _failure is null ? Task.FromResult(_lines) : Task.FromException<IReadOnlyList<DiffLine>>(_failure);
        }
    }
}

using AwesomeAssertions;
using GitCommands;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Diff;
using GitExtensions.Xplat.Core.Settings;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class CommitDiffViewModelTests
{
    private const string RepositoryPath = "/work/repo";
    private const string Hash = "1234567890abcdef1234567890abcdef12345678";
    private const string OtherHash = "abcdefabcdefabcdefabcdefabcdefabcdefabcd";

    private static readonly CommitDetails _details =
        new(Hash, "Ann Author <ann@example.com>", "2026-11-15 12:00", "2026-11-15 12:00", "", "second\n");

    private FakeCommitHistory _history = null!;
    private RequestedDiffs _diffs = null!;
    private FakeDialogHost _host = null!;

    [SetUp]
    public void Setup()
    {
        _history = new FakeCommitHistory { Files = [new CommitFile("M", "a.txt"), new CommitFile("A", "b.txt")] };
        _diffs = new RequestedDiffs();
        _host = new FakeDialogHost();
    }

    [Test]
    public async Task SetRevisionAsync_should_show_the_commit_its_files_and_the_first_files_diff_titled_as_upstreams()
    {
        CommitDiffViewModel viewModel = NewViewModel();

        await SetRevisionAsync(viewModel, Hash, fileToSelect: null);

        viewModel.Info.Should().Be(new ShownCommit(_details, null));
        viewModel.Files.Select(file => file.Path).Should().Equal("a.txt", "b.txt");
        viewModel.SelectedFile!.Path.Should().Be("a.txt");
        viewModel.Title.Should().Be(
            $"Diff - 12345678 - 2026-11-15 12:00 - Ann Author - {RepositoryPath.EnsureTrailingPathSeparator()}");
        await WaitUntilAsync(() => _diffs.Requests.Count == 1);
        _diffs.Requests.Should().Equal($"{Hash} a.txt");
        viewModel.Diff.Title.Should().Be("a.txt");
    }

    [Test]
    public async Task SetRevisionAsync_should_select_the_file_asked_for_when_the_commit_changed_it()
    {
        CommitDiffViewModel viewModel = NewViewModel();

        await SetRevisionAsync(viewModel, Hash, fileToSelect: "b.txt");

        viewModel.SelectedFile!.Path.Should().Be("b.txt");
    }

    [Test]
    public async Task A_file_of_the_same_name_in_the_next_commit_should_show_that_commits_diff()
    {
        CommitDiffViewModel viewModel = NewViewModel();
        await SetRevisionAsync(viewModel, Hash, fileToSelect: null);

        Task second = viewModel.SetRevisionAsync(RepositoryPath, OtherHash, fileToSelect: null);
        _history.CompleteDetails(1, _details with { Hash = OtherHash });
        await second;

        await WaitUntilAsync(() => _diffs.Requests.Count == 2);
        _diffs.Requests[^1].Should().Be($"{OtherHash} a.txt");
    }

    [Test]
    public async Task A_failed_read_should_show_the_error_in_the_commit_information_and_clear_the_files_and_diff()
    {
        CommitDiffViewModel viewModel = NewViewModel();
        await SetRevisionAsync(viewModel, Hash, fileToSelect: null);

        Task failed = viewModel.SetRevisionAsync(RepositoryPath, OtherHash, fileToSelect: null);
        _history.FailDetails(1, new InvalidOperationException("bad object"));
        await failed;

        viewModel.Info.Should().Be(new ShownCommit(null, "bad object"));
        viewModel.Files.Should().BeEmpty();
        viewModel.SelectedFile.Should().BeNull();
        viewModel.Diff.Title.Should().BeEmpty();
    }

    [Test]
    public async Task A_scroll_past_the_diffs_end_should_select_the_neighbour_file_and_stop_at_the_ends()
    {
        CommitDiffViewModel viewModel = NewViewModel();
        await SetRevisionAsync(viewModel, Hash, fileToSelect: null);

        viewModel.SelectNeighbourFile(next: true);
        viewModel.SelectedFile!.Path.Should().Be("b.txt");
        viewModel.SelectNeighbourFile(next: true);
        viewModel.SelectedFile!.Path.Should().Be("b.txt", "upstream does not go around");
        viewModel.SelectNeighbourFile(next: false);
        viewModel.SelectedFile!.Path.Should().Be("a.txt");
    }

    [Test]
    public async Task DiffWindowViewModel_should_title_a_commits_file_and_read_its_diff_when_loaded()
    {
        DiffWindowViewModel viewModel = new(_host, NewDiff(), RepositoryPath, Hash, "a.txt", staged: false);

        await viewModel.LoadAsync();

        viewModel.Title.Should().Be("12345678: a.txt");
        _diffs.Requests.Should().Equal($"{Hash} a.txt");
        viewModel.Diff.Title.Should().Be("a.txt");
        new DiffWindowViewModel(_host, NewDiff(), RepositoryPath, null, "a.txt", staged: true).Title
            .Should().Be("Staged: a.txt");
        new DiffWindowViewModel(_host, NewDiff(), RepositoryPath, null, null, staged: false).Title
            .Should().Be("Unstaged");
    }

    [Test]
    public async Task DiffWindowViewModel_should_show_an_error_of_the_callers_read_over_the_window()
    {
        DiffWindowViewModel viewModel = new(_host, NewDiff(), "a.txt",
            _ => Task.FromException(new InvalidOperationException("no such revision")));

        await viewModel.LoadAsync();

        _host.Errors.Should().ContainSingle().Which.Message.Should().Be("no such revision");
    }

    private async Task SetRevisionAsync(CommitDiffViewModel viewModel, string hash, string? fileToSelect)
    {
        Task set = viewModel.SetRevisionAsync(RepositoryPath, hash, fileToSelect);
        _history.CompleteDetails(_history.DetailHashes.Count - 1, _details with { Hash = hash });
        await set;
    }

    private CommitDiffViewModel NewViewModel() => new(_history, NewDiff());

    private DiffViewModel NewDiff()
        => new(_diffs, new InMemoryAppPreferences(), new DiffViewModelServices
        {
            IsDifftasticEnabled = _ => false,
            Encodings = () => [],
        });

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail("Timed out");
            }

            await Task.Delay(10);
        }
    }

    private sealed class RequestedDiffs : IDiffService
    {
        public List<string> Requests { get; } = [];

        public Task<IReadOnlyList<DiffLine>> GetDiffAsync(string repositoryPath, string? commitHash, string? filePath,
            bool staged, DiffOptions? options = null)
        {
            lock (Requests)
            {
                Requests.Add($"{commitHash} {filePath}");
            }

            return Task.FromResult<IReadOnlyList<DiffLine>>([new DiffLine("@@ -1 +1 @@", DiffLineKind.Hunk)]);
        }
    }
}

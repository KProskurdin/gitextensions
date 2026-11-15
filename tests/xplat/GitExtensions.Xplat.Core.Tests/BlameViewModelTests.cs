using AwesomeAssertions;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Settings;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class BlameViewModelTests
{
    private const string RepositoryPath = "/work/repo";
    private const string FilePath = "a.txt";
    private const string Head = "1111111111111111111111111111111111111111";
    private const string Older = "2222222222222222222222222222222222222222";
    private const string Base = "3333333333333333333333333333333333333333";

    // Git's hash of a line not committed yet.
    private const string NotCommitted = "0000000000000000000000000000000000000000";

    private FakeCommitHistory _history = null!;
    private InMemoryAppPreferences _preferences = null!;

    [SetUp]
    public void Setup()
    {
        _history = new FakeCommitHistory
        {
            Blame =
            [
                Line(1, Older, "one", originLine: 1),
                Line(2, Head, "two", originLine: 2),
                Line(3, NotCommitted, "not committed", originLine: 3),
            ],
        };
        _preferences = new InMemoryAppPreferences();
    }

    [Test]
    public async Task LoadAsync_should_show_the_rows_and_the_blamed_commits_information()
    {
        BlameViewModel viewModel = NewViewModel();

        Task load = viewModel.LoadAsync(RepositoryPath, Head, FilePath);
        await load;
        _history.CompleteDetails(0, Details(Head));
        await WaitUntilAsync(() => viewModel.Info.Details is not null);

        viewModel.Rows.Select(row => row.Content).Should().Equal("one", "two", "not committed");
        viewModel.Info.Details!.Hash.Should().Be(Head);
        _history.DetailHashes.Should().Equal(Head);
    }

    [Test]
    public async Task A_selected_line_should_show_its_commit_and_lead_to_it_unless_it_is_not_committed()
    {
        BlameViewModel viewModel = await LoadedAsync();
        List<BlameCommitRequest> requests = [];
        viewModel.CommitRequested += (_, request) => requests.Add(request);

        viewModel.SetSelection(0);
        viewModel.BlameRevisionCommand.Execute(null);

        viewModel.HasSelectedCommit.Should().BeTrue();
        viewModel.CurrentFileLine.Should().Be(1);
        _history.DetailHashes.Should().Equal(Head, Older);
        requests.Should().Equal(new BlameCommitRequest(Older, FilePath));

        viewModel.SetSelection(2);
        viewModel.BlameRevisionCommand.Execute(null);

        viewModel.HasSelectedCommit.Should().BeFalse("a line not committed yet has no commit");
        requests.Should().HaveCount(1);
    }

    [Test]
    public async Task A_commit_in_the_grid_should_be_blamed_on_its_line_when_it_loads()
    {
        BlameViewModel viewModel = await LoadedAsync();
        viewModel.GridRevision = hash => Row(hash);
        List<int> selected = [];
        viewModel.SelectLineRequested += (_, index) => selected.Add(index);
        viewModel.SetSelection(1);

        viewModel.BlameRevisionCommand.Execute(null);

        selected.Should().Equal([1], "the blamed commit only goes to the line");

        viewModel.SetSelection(0);
        viewModel.BlameRevisionCommand.Execute(null);
        await viewModel.LoadAsync(RepositoryPath, Older, FilePath);

        selected[^1].Should().Be(0, "the line the commit had is selected once it is blamed");
    }

    [Test]
    public async Task The_menu_should_offer_the_real_previous_revision_when_the_grid_shows_it()
    {
        BlameViewModel viewModel = await LoadedAsync();
        _history.Parents[Older] = [Base];
        _history.PreviousLine = 7;
        viewModel.GridRevision = hash => hash == Base || hash == Older ? Row(hash) : null;
        List<BlameCommitRequest> requests = [];
        viewModel.CommitRequested += (_, request) => requests.Add(request);
        viewModel.SetSelection(0);

        viewModel.RefreshMenu();
        await WaitUntilAsync(() => viewModel.CanBlamePreviousRevision);

        viewModel.CanBlameRevision.Should().BeTrue();
        viewModel.PreviousRevisionHeader.Should().Be("_Blame previous revision");
        await viewModel.BlamePreviousRevisionCommand.ExecuteAsync(null);
        requests.Should().Equal(new BlameCommitRequest(Base, FilePath));
    }

    [Test]
    public async Task The_menu_should_name_the_visible_revision_and_disable_it_when_the_grid_shows_no_parent()
    {
        BlameViewModel viewModel = await LoadedAsync();
        _history.Parents[Older] = [Base];
        viewModel.GridRevision = hash => hash == Older ? Row(hash) : null;
        viewModel.SetSelection(0);

        viewModel.RefreshMenu();
        await WaitUntilAsync(() => viewModel.PreviousRevisionHeader == "_Blame previous visible revision");

        viewModel.CanBlamePreviousRevision.Should().BeFalse();
    }

    [Test]
    public async Task A_setting_should_be_stored_and_blame_again_only_when_it_changes_gits_flags()
    {
        BlameViewModel viewModel = await LoadedAsync();
        int changes = 0;
        viewModel.OptionsChanged += (_, _) => changes++;

        await viewModel.ToggleOptionCommand.ExecuteAsync(BlameOption.ShowAuthorDate);

        _preferences.BlameOptions.ShowAuthorDate.Should().BeFalse();
        viewModel.ShowAuthorDate.Should().BeFalse();
        viewModel.CanShowAuthorTime.Should().BeFalse("upstream's time goes with the date");
        changes.Should().Be(1);

        await viewModel.ToggleOptionCommand.ExecuteAsync(BlameOption.ShowAuthor);

        viewModel.ShowAuthorDate.Should().BeTrue("the line always shows the author or the date");
    }

    [Test]
    public async Task Copy_should_put_the_selected_lines_commit_on_the_clipboard()
    {
        BlameViewModel viewModel = await LoadedAsync();
        List<string> copied = [];
        viewModel.SetClipboard = text =>
        {
            copied.Add(text);
            return Task.CompletedTask;
        };
        viewModel.SetSelection(0);

        await viewModel.CopyCommitHashCommand.ExecuteAsync(null);
        await viewModel.CopyCommitMessageCommand.ExecuteAsync(null);

        copied.Should().Equal(Older, "summary of " + Older[..4]);
    }

    [Test]
    public async Task A_hidden_commit_information_should_not_be_read()
    {
        BlameViewModel viewModel = NewViewModel();
        viewModel.HideCommitInfo();

        await viewModel.LoadAsync(RepositoryPath, Head, FilePath);
        viewModel.SetSelection(0);

        viewModel.IsCommitInfoVisible.Should().BeFalse();
        _history.DetailHashes.Should().BeEmpty();
    }

    [Test]
    public async Task BlameWindowViewModel_should_title_the_file_and_show_the_blames_errors()
    {
        BlameViewModel blame = NewViewModel();
        BlameWindowViewModel window = new(blame, RepositoryPath, Head, FilePath);

        await window.LoadAsync();
        _history.FailDetails(0, new InvalidOperationException("bad object"));
        await WaitUntilAsync(() => blame.Info.Error is not null);

        window.Title.Should().Be("Blame: a.txt");
        blame.Rows.Should().HaveCount(3);
        window.ErrorMessage.Should().BeNull("the commit information shows its own error");
    }

    private async Task<BlameViewModel> LoadedAsync()
    {
        BlameViewModel viewModel = NewViewModel();
        await viewModel.LoadAsync(RepositoryPath, Head, FilePath);
        return viewModel;
    }

    private BlameViewModel NewViewModel() => new(_history, _preferences);

    private static BlameLine Line(int number, string hash, string content, int originLine)
        => new(number, hash[..8], "Ann", "2026-11-15", content)
        {
            Commit = new GitBlameCommit(ObjectId.Parse(hash), "Ann", "ann@example.com",
                new DateTime(2026, 11, 15), "+0000", "Ann", "ann@example.com", new DateTime(2026, 11, 15), "+0000",
                "summary of " + hash[..4], FilePath),
            OriginLineNumber = originLine,
        };

    private static CommitRow Row(string hash) => new(hash, hash[..8], "subject", "Ann", "2026-11-15", ParentHashes: [Base]);

    private static CommitDetails Details(string hash) => new(hash, "Ann <ann@example.com>", "", "", "", "message");

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
}

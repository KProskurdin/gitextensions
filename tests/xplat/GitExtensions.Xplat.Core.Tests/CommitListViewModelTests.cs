using AwesomeAssertions;
using GitExtensions.Xplat.Core.CommitHistory;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class CommitListViewModelTests
{
    private const string RepositoryPath = "/work/one";
    private const string OtherRepositoryPath = "/work/two";

    private FakeCommitHistory _history = null!;
    private CommitListViewModel _viewModel = null!;

    [SetUp]
    public void Setup()
    {
        _history = new FakeCommitHistory();
        _viewModel = new CommitListViewModel(_history);
    }

    [Test]
    public async Task OpenAsync_should_show_the_first_page()
    {
        Task open = _viewModel.OpenAsync(RepositoryPath);
        _history.CompletePage(0, Page(hasMore: false, "aaa1", "bbb2"));
        await open;

        _viewModel.Rows.Should().HaveCount(2);
        _viewModel.Status.Should().Be("2 commits");
        _viewModel.RepositoryName.Should().Be("one");
        _viewModel.HasMore.Should().BeFalse();
        _viewModel.IsLoading.Should().BeFalse();
        _history.PageLimits.Should().Equal(CommitListViewModel.PageSize);
    }

    [Test]
    public async Task OpenAsync_should_report_more_history_when_the_page_is_not_the_last()
    {
        Task open = _viewModel.OpenAsync(RepositoryPath);
        _history.CompletePage(0, Page(hasMore: true, "aaa1"));
        await open;

        _viewModel.HasMore.Should().BeTrue();
        _viewModel.Status.Should().Be("1 commit, more available");
    }

    [Test]
    public async Task LoadMoreAsync_should_request_the_next_page_size()
    {
        Task open = _viewModel.OpenAsync(RepositoryPath);
        _history.CompletePage(0, Page(hasMore: true, "aaa1"));
        await open;

        Task more = _viewModel.LoadMoreAsync();
        _history.CompletePage(1, Page(hasMore: false, "aaa1", "bbb2"));
        await more;

        _history.PageLimits.Should().Equal(CommitListViewModel.PageSize, 2 * CommitListViewModel.PageSize);
        _viewModel.Rows.Should().HaveCount(2);
        _viewModel.HasMore.Should().BeFalse();
    }

    [Test]
    public async Task LoadMoreAsync_should_do_nothing_before_a_repository_is_open()
    {
        await _viewModel.LoadMoreAsync();

        _history.PageLimits.Should().BeEmpty();
    }

    [Test]
    public async Task OpenAsync_failure_should_set_the_error_and_clear_the_rows()
    {
        Task open = _viewModel.OpenAsync(RepositoryPath);
        _history.CompletePage(0, Page(hasMore: false, "aaa1"));
        await open;

        Task failing = _viewModel.OpenAsync(OtherRepositoryPath);
        _history.FailPage(1, new InvalidOperationException("Not a git repository: /work/two"));
        await failing;

        _viewModel.ErrorMessage.Should().Be("Not a git repository: /work/two");
        _viewModel.Rows.Should().BeEmpty();
        _viewModel.RepositoryName.Should().BeEmpty();
    }

    [Test]
    public async Task A_newer_open_wins_over_an_older_open_that_finishes_later()
    {
        Task first = _viewModel.OpenAsync(RepositoryPath);
        Task second = _viewModel.OpenAsync(OtherRepositoryPath);

        _history.CompletePage(1, Page(hasMore: false, "ccc3"));
        await second;
        _history.CompletePage(0, Page(hasMore: false, "aaa1", "bbb2"));
        await first;

        _viewModel.RepositoryName.Should().Be("two");
        _viewModel.Rows.Should().ContainSingle().Which.Hash.Should().Be("ccc3");
    }

    [Test]
    public async Task SelectAsync_should_load_the_details_of_the_selected_commit()
    {
        await OpenWithRows("aaa1");

        Task select = _viewModel.SelectAsync(_viewModel.Rows[0]);
        _history.DetailHashes.Should().Equal("aaa1");
        _history.CompleteDetails(0, Details("aaa1", "message"));
        await select;

        _viewModel.Details!.Message.Should().Be("message");
        _viewModel.DetailsError.Should().BeEmpty();
    }

    [Test]
    public async Task SelectAsync_null_should_clear_the_details()
    {
        await OpenWithRows("aaa1");
        Task select = _viewModel.SelectAsync(_viewModel.Rows[0]);
        _history.CompleteDetails(0, Details("aaa1", "message"));
        await select;

        await _viewModel.SelectAsync(null);

        _viewModel.Details.Should().BeNull();
        _viewModel.Selected.Should().BeNull();
    }

    [Test]
    public async Task An_older_selection_that_finishes_later_is_ignored()
    {
        await OpenWithRows("aaa1", "bbb2");

        Task firstSelect = _viewModel.SelectAsync(_viewModel.Rows[0]);
        Task secondSelect = _viewModel.SelectAsync(_viewModel.Rows[1]);
        _history.CompleteDetails(1, Details("bbb2", "second"));
        await secondSelect;
        _history.CompleteDetails(0, Details("aaa1", "first"));
        await firstSelect;

        _viewModel.Details!.Hash.Should().Be("bbb2");
    }

    [Test]
    public async Task SelectAsync_failure_should_set_the_details_error()
    {
        await OpenWithRows("aaa1");

        Task select = _viewModel.SelectAsync(_viewModel.Rows[0]);
        _history.FailDetails(0, new InvalidOperationException("Commit not found: aaa1"));
        await select;

        _viewModel.Details.Should().BeNull();
        _viewModel.DetailsError.Should().Be("Commit not found: aaa1");
    }

    [Test]
    public async Task Reopening_should_clear_the_selection_and_its_details()
    {
        await OpenWithRows("aaa1");
        Task select = _viewModel.SelectAsync(_viewModel.Rows[0]);
        _history.CompleteDetails(0, Details("aaa1", "message"));
        await select;

        Task reopen = _viewModel.OpenAsync(OtherRepositoryPath);
        _history.CompletePage(1, Page(hasMore: false, "ccc3"));
        await reopen;

        _viewModel.Selected.Should().BeNull();
        _viewModel.Details.Should().BeNull();
    }

    [Test]
    public void Property_changes_are_raised_for_the_state_a_view_shows()
    {
        List<string?> changed = [];
        _viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        Task open = _viewModel.OpenAsync(RepositoryPath);
        _history.CompletePage(0, Page(hasMore: false, "aaa1"));

        open.Wait();
        changed.Should().Contain(nameof(CommitListViewModel.IsLoading));
        changed.Should().Contain(nameof(CommitListViewModel.Rows));
        changed.Should().Contain(nameof(CommitListViewModel.Status));
    }

    private async Task OpenWithRows(params string[] hashes)
    {
        Task open = _viewModel.OpenAsync(RepositoryPath);
        _history.CompletePage(0, Page(hasMore: false, hashes));
        await open;
    }

    private static CommitPage Page(bool hasMore, params string[] hashes)
        => new(hashes.Select(hash => new CommitRow(hash, hash[..4], "subject " + hash, "author", "2026-10-03 12:00")).ToList(), hasMore);

    private static CommitDetails Details(string hash, string message)
        => new(hash, "author <a@example.com>", "2026-10-03 12:00", "2026-10-03 12:00", "", message);
}

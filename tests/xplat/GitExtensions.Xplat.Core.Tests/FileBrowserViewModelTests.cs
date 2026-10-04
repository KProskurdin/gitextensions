using AwesomeAssertions;
using GitExtensions.Xplat.Core.CommitHistory;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class FileBrowserViewModelTests
{
    private FakeCommitHistory _history = null!;
    private FileBrowserViewModel _viewModel = null!;

    [SetUp]
    public void Setup()
    {
        _history = new FakeCommitHistory { Tree = ["a.txt", "dir/b.txt"] };
        _viewModel = new FileBrowserViewModel(_history);
    }

    [Test]
    public async Task OpenAsync_should_list_the_files_of_the_commit()
    {
        await _viewModel.OpenAsync("/work/repo", "abc");

        _viewModel.Files.Should().Equal("a.txt", "dir/b.txt");
        _viewModel.SelectedFile.Should().BeNull();
    }

    [Test]
    public async Task SelectFileAsync_should_load_the_history_of_that_file_from_the_commit()
    {
        await _viewModel.OpenAsync("/work/repo", "abc");
        _history.FileHistory = [new CommitRow("abc", "abc", "second", "author", "2026-10-04 10:00")];

        await _viewModel.SelectFileAsync("a.txt");

        _history.FileHistoryRequests.Should().Equal(("abc", "a.txt"));
        _viewModel.SelectedFile.Should().Be("a.txt");
        _viewModel.FileHistory.Should().ContainSingle().Which.Subject.Should().Be("second");
    }

    [Test]
    public async Task SelectFileAsync_null_should_clear_the_history_without_reading()
    {
        await _viewModel.OpenAsync("/work/repo", "abc");
        await _viewModel.SelectFileAsync("a.txt");

        await _viewModel.SelectFileAsync(null);

        _viewModel.FileHistory.Should().BeEmpty();
        _history.FileHistoryRequests.Should().HaveCount(1);
    }

    [Test]
    public async Task Opening_another_commit_clears_the_selected_file()
    {
        await _viewModel.OpenAsync("/work/repo", "abc");
        await _viewModel.SelectFileAsync("a.txt");

        await _viewModel.OpenAsync("/work/repo", "def");

        _viewModel.SelectedFile.Should().BeNull();
        _viewModel.FileHistory.Should().BeEmpty();
    }
}

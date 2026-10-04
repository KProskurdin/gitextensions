using Avalonia.Headless.NUnit;
using GitExtensions.Xplat.Core.CommitHistory;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

// Real git reads need ThreadHelper.JoinableTaskContext, which the app sets at startup; AvaloniaTest runs the app.
internal sealed class CommitHistoryTests
{
    private TestRepository _repo = null!;
    private GitCommitHistory _history = null!;

    [SetUp]
    public void Setup()
    {
        _repo = new TestRepository();
        _history = new GitCommitHistory();
    }

    [TearDown]
    public void TearDown()
    {
        _repo.Dispose();
    }

    [AvaloniaTest]
    public void LoadPage_should_stop_at_the_limit_and_report_more_history()
    {
        CommitPage page = _history.LoadPageAsync(_repo.Path, limit: 1).GetAwaiter().GetResult();

        page.Rows.Should().HaveCount(1);
        page.Rows[0].Subject.Should().Be("second");
        page.HasMore.Should().BeTrue();
    }

    [AvaloniaTest]
    public void LoadPage_should_report_no_more_history_when_the_limit_covers_all_commits()
    {
        CommitPage page = _history.LoadPageAsync(_repo.Path, limit: 2).GetAwaiter().GetResult();

        page.Rows.Should().HaveCount(2);
        page.HasMore.Should().BeFalse();
    }

    [AvaloniaTest]
    public void LoadPage_should_return_all_commits_when_the_limit_is_larger()
    {
        CommitPage page = _history.LoadPageAsync(_repo.Path, limit: 5).GetAwaiter().GetResult();

        page.Rows.Should().HaveCount(2);
        page.HasMore.Should().BeFalse();
    }

    [AvaloniaTest]
    public void LoadTree_lists_every_file_of_the_commit()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "dir name.txt"), "x");
        _repo.Run("add", "dir name.txt");
        _repo.Run("commit", "-q", "-m", "add file");
        string head = _repo.Run("rev-parse", "HEAD").Trim();

        IReadOnlyList<string> files = _history.LoadTreeAsync(_repo.Path, head).GetAwaiter().GetResult();

        files.Should().Equal("a.txt", "dir name.txt");
    }

    [AvaloniaTest]
    public void LoadFileHistory_lists_only_the_commits_that_changed_the_file()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "other.txt"), "x");
        _repo.Run("add", "other.txt");
        _repo.Run("commit", "-q", "-m", "other file");
        string head = _repo.Run("rev-parse", "HEAD").Trim();

        CommitPage aHistory = _history.LoadFileHistoryAsync(_repo.Path, head, "a.txt", limit: 10).GetAwaiter()
            .GetResult();
        CommitPage otherHistory = _history.LoadFileHistoryAsync(_repo.Path, head, "other.txt", limit: 10).GetAwaiter()
            .GetResult();

        aHistory.Rows.Select(row => row.Subject).Should().Equal("second", "first");
        otherHistory.Rows.Select(row => row.Subject).Should().Equal("other file");
    }

    [AvaloniaTest]
    public void LoadBlame_attributes_each_line_to_the_commit_that_last_changed_it()
    {
        string head = _repo.Run("rev-parse", "HEAD").Trim();

        IReadOnlyList<BlameLine> lines = _history.LoadBlameAsync(_repo.Path, head, "a.txt").GetAwaiter().GetResult();

        lines.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new { LineNumber = 1, Hash = head[..8], Author = "Test", Content = "two" });
    }

    [AvaloniaTest]
    public void LoadDetails_should_return_parents_and_message()
    {
        string head = _repo.Run("rev-parse", "HEAD").Trim();
        string parent = _repo.Run("rev-parse", "HEAD~1").Trim();

        CommitDetails details = _history.LoadDetailsAsync(_repo.Path, head).GetAwaiter().GetResult();

        details.Hash.Should().Be(head);
        details.Parents.Should().StartWith(parent[..7]);
        details.Message.Should().Be("second\n\nbody line");
    }
}

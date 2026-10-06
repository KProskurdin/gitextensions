using Avalonia.Headless.NUnit;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Repository;
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
    public void LoadPage_should_label_commits_with_their_branches_and_tags()
    {
        _repo.Run("branch", "feature");
        _repo.Run("tag", "v1", "HEAD~1");

        CommitPage page = _history.LoadPageAsync(_repo.Path, limit: 5).GetAwaiter().GetResult();

        page.Rows[0].Refs.Should().Contain("feature").And.Contain("HEAD");
        page.Rows[1].Refs.Should().Equal("v1");
    }

    [AvaloniaTest]
    public void Reflog_keeps_the_commit_a_hard_reset_moved_away_from()
    {
        string previous = _repo.Run("rev-parse", "HEAD").Trim();
        _repo.Run("reset", "-q", "--hard", "HEAD~1");

        IReadOnlyList<ReflogEntry> entries = Reflog.LoadAsync(_repo.Path, 10).GetAwaiter().GetResult();

        entries[0].Message.Should().StartWith("reset:");
        entries.Should().Contain(entry => entry.Hash == previous);
    }

    [AvaloniaTest]
    public void Search_finds_only_the_commits_whose_message_contains_the_text()
    {
        CommitPage found = _history.SearchAsync(_repo.Path, "BODY LINE", 10).GetAwaiter().GetResult();
        CommitPage none = _history.SearchAsync(_repo.Path, "no such text", 10).GetAwaiter().GetResult();

        found.Rows.Should().ContainSingle().Which.Subject.Should().Be("second");
        none.Rows.Should().BeEmpty();
    }

    [AvaloniaTest]
    public void LoadPage_should_give_each_commit_its_parent_hashes()
    {
        CommitPage page = _history.LoadPageAsync(_repo.Path, limit: 5).GetAwaiter().GetResult();

        string first = _repo.Run("rev-parse", "HEAD~1").Trim();
        page.Rows[0].ParentHashes.Should().Equal(first);
        page.Rows[1].ParentHashes.Should().BeEmpty();
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

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

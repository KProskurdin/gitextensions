using GitExtensions.Xplat.App;
using Avalonia.Headless.NUnit;

namespace GitExtensions.Xplat.App.Tests;

internal sealed class CommitHistoryTests
{
    private TestRepository _repo = null!;

    [SetUp]
    public void Setup()
    {
        _repo = new TestRepository();
    }

    [TearDown]
    public void TearDown()
    {
        _repo.Dispose();
    }

    [AvaloniaTest]
    public void LoadPage_should_stop_at_the_limit_and_report_more_history()
    {
        CommitPage page = CommitHistory.LoadPage(_repo.Path, limit: 1);

        page.Rows.Should().HaveCount(1);
        page.Rows[0].Subject.Should().Be("second");
        page.HasMore.Should().BeTrue();
    }

    [AvaloniaTest]
    public void LoadPage_should_report_no_more_history_when_the_limit_covers_all_commits()
    {
        CommitPage page = CommitHistory.LoadPage(_repo.Path, limit: 2);

        page.Rows.Should().HaveCount(2);
        page.HasMore.Should().BeFalse();
    }

    [AvaloniaTest]
    public void LoadPage_should_return_all_commits_when_the_limit_is_larger()
    {
        CommitPage page = CommitHistory.LoadPage(_repo.Path, limit: 5);

        page.Rows.Should().HaveCount(2);
        page.HasMore.Should().BeFalse();
    }

    [AvaloniaTest]
    public void LoadDetails_should_return_parents_and_message()
    {
        string head = _repo.Run("rev-parse", "HEAD").Trim();
        string parent = _repo.Run("rev-parse", "HEAD~1").Trim();

        CommitDetails details = CommitHistory.LoadDetails(_repo.Path, head);

        details.Hash.Should().Be(head);
        details.Parents.Should().StartWith(parent[..7]);
        details.Message.Should().Be("second\n\nbody line");
    }
}

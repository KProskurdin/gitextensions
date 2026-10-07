using AwesomeAssertions;
using GitExtensions.Xplat.Core.CommitHistory;
using NUnit.Framework;
using ResourceManager;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class CommitDateStyleTests
{
    private static readonly DateTime _now = new(2026, 10, 7, 12, 0, 0);
    private static readonly DateTime _authored = new(2026, 10, 4, 12, 0, 0);
    private static readonly DateTime _committed = new(2026, 10, 6, 9, 30, 0);

    [Test]
    public void Format_should_show_upstreams_relative_text_for_the_author_date_by_default()
    {
        new CommitDateStyle().Format(_authored, _committed, _now).Should()
            .Be(LocalizationHelpers.GetRelativeDateString(_now, _authored, displayWeeks: false));
    }

    [Test]
    public void Format_should_show_the_full_commit_date_when_asked()
    {
        new CommitDateStyle(Relative: false, AuthorDate: false).Format(_authored, _committed, _now).Should()
            .Be(_committed.ToString("G"));
        new CommitDateStyle(Relative: false).Format(_authored, _committed, _now).Should().Be(_authored.ToString("G"));
    }

    [Test]
    public void Format_should_leave_an_unknown_date_empty()
    {
        new CommitDateStyle().Format(DateTime.MinValue, _committed, _now).Should().BeEmpty();
    }
}

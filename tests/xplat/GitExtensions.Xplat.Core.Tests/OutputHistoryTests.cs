using AwesomeAssertions;
using GitExtensions.Xplat.Core.Operations;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class OutputHistoryTests
{
    private static readonly DateTime _time = new(2026, 11, 9, 14, 5, 0);

    [Test]
    public void RecordRun_should_keep_upstreams_format_without_color_codes()
    {
        OutputHistory history = new(20);

        history.RecordRun("Fetch origin", ["\u001b[32mdone\u001b[0m"], _time);
        history.RecordRun(null, [], _time);

        string time = _time.ToString("t", System.Globalization.CultureInfo.CurrentCulture);
        string n = Environment.NewLine;
        history.History.Should().Be($"{time} Fetch origin{n}done{n}{n}{time} ---{n}{n}{n}###{n}");
    }

    [Test]
    public void The_history_should_keep_only_the_newest_entries_and_be_off_at_depth_0()
    {
        OutputHistory history = new(2);
        int changes = 0;
        history.HistoryChanged += (_, _) => changes++;

        history.Record("one", _time);
        history.Record("two", _time);
        history.Record("three", _time);

        history.History.Should().NotContain("one").And.Contain("two").And.Contain("three");
        changes.Should().Be(3);
        history.Clear();
        history.History.Trim().Should().Be(OutputHistory.EndMark);

        OutputHistory off = new(0);
        off.Record("ignored", _time);
        off.Enabled.Should().BeFalse();
        off.History.Should().NotContain("ignored");
    }
}

using AwesomeAssertions;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Repository;
using NSubstitute;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

// Upstream FormReflog's grid sorts by a column on a header click (its SortableRefLineList).
internal sealed class ReflogSortTests
{
    private static readonly ObjectId _low = ObjectId.Parse("1111111111111111111111111111111111111111");
    private static readonly ObjectId _high = ObjectId.Parse("eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee");

    private static readonly IReadOnlyList<ReflogEntry> _gitOrder =
    [
        new(_high, "HEAD@{0}", "commit: b"),
        new(_low, "HEAD@{1}", "checkout: a"),
    ];

    [Test]
    public async Task SortBy_should_sort_by_the_column_then_the_other_way_and_a_reload_should_show_gits_order()
    {
        ReflogViewModel reflog = new(new FakeDialogHost(), "/repo", Substitute.For<IGitModule>(),
            _ => Task.CompletedTask, (_, _) => Task.CompletedTask, (_, _) => Task.FromResult(_gitOrder));
        reflog.SelectedReference = "HEAD";
        await reflog.EntriesLoaded;

        reflog.SortByCommand.Execute(ReflogColumn.Sha);
        reflog.Entries.Select(entry => entry.Sha).Should().Equal(_low, _high);
        reflog.SortByCommand.Execute(ReflogColumn.Sha);
        reflog.Entries.Select(entry => entry.Sha).Should().Equal(_high, _low);
        reflog.SortDescending.Should().BeTrue();

        reflog.SortByCommand.Execute(ReflogColumn.Action);
        reflog.Entries.Select(entry => entry.Action).Should().Equal("checkout: a", "commit: b");
        reflog.SortDescending.Should().BeFalse("another column starts from the first");

        reflog.SelectedReference = "main";
        await reflog.EntriesLoaded;
        reflog.Entries.Should().Equal(_gitOrder);
        reflog.SortColumn.Should().BeNull();
    }
}

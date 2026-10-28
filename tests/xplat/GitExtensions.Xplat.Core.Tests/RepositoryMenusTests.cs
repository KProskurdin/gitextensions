using AwesomeAssertions;
using GitCommands;
using GitCommands.UserRepositoryHistory;
using GitExtensions.Xplat.Core.Repository;
using NUnit.Framework;
using HistoryEntry = GitCommands.UserRepositoryHistory.Repository;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class RepositoryMenusTests
{
    private static readonly string _alpha = Rooted("alpha");
    private static readonly string _beta = Rooted("beta");
    private static readonly string _gamma = Rooted("gamma");

    [Test]
    public void Recent_should_number_the_top_repositories_then_the_others_and_mark_pinned_ones()
    {
        (IReadOnlyList<RepositoryMenuEntry> top, IReadOnlyList<RepositoryMenuEntry> others) = RepositoryMenus.Recent(
            [
                new RepositoryEntry(_alpha),
                new RepositoryEntry(_beta, Anchor: HistoryEntry.RepositoryAnchor.AnchoredInRecent),
                new RepositoryEntry(_gamma),
            ],
            Splitter(maxTop: 1));

        top.Should().Equal(new RepositoryMenuEntry(_alpha, _alpha, 1, Anchored: false));
        others.Should().Equal(
            new RepositoryMenuEntry(_beta, _beta, 2, Anchored: true),
            new RepositoryMenuEntry(_gamma, _gamma, 3, Anchored: false));
        top[0].Tooltip.Should().BeNull("the caption is the path");
    }

    [Test]
    public void Favourites_should_group_by_category_in_name_order_and_number_each_group()
    {
        IReadOnlyList<RepositoryMenuGroup> groups = RepositoryMenus.Favourites(
            [
                new RepositoryEntry(_alpha, "Work"),
                new RepositoryEntry(_beta, "Hobby"),
                new RepositoryEntry(_gamma, "Work"),
            ],
            Splitter(maxTop: 0));

        groups.Select(group => group.Category).Should().Equal("Hobby", "Work");
        groups[0].Entries.Select(entry => (entry.Path, entry.Number)).Should().Equal((_beta, 1));
        groups[1].Entries.Select(entry => (entry.Path, entry.Number)).Should().Equal((_alpha, 1), (_gamma, 2));
    }

    [Test]
    public void Tooltip_should_be_the_path_when_the_caption_is_shortened()
    {
        new RepositoryMenuEntry(_alpha, "alpha", 1, Anchored: false).Tooltip.Should().Be(_alpha);
    }

    private static RecentRepoSplitter Splitter(int maxTop) => new()
    {
        ShorteningStrategy = ShorteningRecentRepoPathStrategy.None,
        MaxTopRepositories = maxTop,
        HideTopRepositoriesFromRecentList = true,
        SortTopRepos = false,
        SortRecentRepos = false,
    };

    private static string Rooted(string name)
        => Path.Combine(OperatingSystem.IsWindows() ? @"Q:\" : "/xplat-outside-home", name);
}

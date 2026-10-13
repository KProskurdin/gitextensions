using AwesomeAssertions;
using GitCommands;
using GitCommands.UserRepositoryHistory;
using GitExtensions.Xplat.Core.Repository;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class DashboardViewModelTests
{
    private static readonly string _alpha = Rooted("alpha");
    private static readonly string _beta = Rooted("beta");
    private static readonly string _gamma = Rooted("gamma");

    private InMemoryRecentRepositoryStore _store = null!;
    private HashSet<string> _missing = null!;

    [SetUp]
    public void Setup()
    {
        _store = new InMemoryRecentRepositoryStore();
        _missing = [];
    }

    [Test]
    public async Task LoadAsync_should_show_the_recent_group_then_the_categories_in_order()
    {
        await _store.AddAsync(_gamma);
        await _store.AddAsync(_beta);
        await _store.AddAsync(_alpha);
        await _store.AssignCategoryAsync(_gamma, "Work");
        await _store.AssignCategoryAsync(_beta, "Hobby");
        DashboardViewModel dashboard = New();

        await dashboard.LoadAsync();

        dashboard.Groups.Select(group => (group.Header, group.IsRecent)).Should()
            .Equal((DashboardViewModel.RecentGroupHeader, true), ("Hobby", false), ("Work", false));
        dashboard.Groups[0].Repositories.Select(repository => repository.Path).Should().Equal(_alpha, _beta, _gamma);
        DashboardRepository work = dashboard.Groups[2].Repositories.Should().ContainSingle().Which;
        (work.Path, work.IsFavourite, work.HasCategory).Should().Be((_gamma, true, true));
        dashboard.Categories.Should().Equal("Hobby", "Work");
    }

    [Test]
    public async Task LoadAsync_should_read_the_branches_and_find_missing_folders_after_the_list_shows()
    {
        await _store.AddAsync(_beta);
        await _store.AddAsync(_alpha);
        _missing.Add(_beta);
        DashboardViewModel dashboard = New();

        await dashboard.LoadAsync();

        dashboard.Groups[0].Repositories.Select(repository => (repository.IsValid, repository.Branch)).Should()
            .Equal((true, "main"), (false, null));
        dashboard.HasInvalid.Should().BeTrue();
    }

    [Test]
    public async Task SearchText_should_show_the_repositories_whose_path_contains_it()
    {
        await _store.AddAsync(_beta);
        await _store.AddAsync(_alpha);
        DashboardViewModel dashboard = New();
        await dashboard.LoadAsync();

        dashboard.SearchText = "ALP";

        dashboard.Groups.Single().Repositories.Select(repository => repository.Path).Should().Equal(_alpha);
        dashboard.Groups.Single().Repositories[0].Branch.Should().Be("main", "the states read before are kept");
    }

    [Test]
    public async Task RemoveAsync_should_take_a_favourite_out_of_its_category_and_a_recent_one_out_of_the_list()
    {
        await _store.AddAsync(_alpha);
        await _store.AssignCategoryAsync(_alpha, "Work");
        DashboardViewModel dashboard = New();
        await dashboard.LoadAsync();

        await dashboard.RemoveAsync(dashboard.Groups[1].Repositories[0]);

        dashboard.Groups.Select(group => group.Header).Should().Equal(DashboardViewModel.RecentGroupHeader);

        await dashboard.RemoveAsync(dashboard.Groups[0].Repositories[0]);

        dashboard.Groups.Should().BeEmpty();
    }

    [Test]
    public async Task RenameCategoryAsync_should_move_the_repositories_and_a_null_name_should_delete_it()
    {
        await _store.AssignCategoryAsync(_alpha, "Work");
        await _store.AssignCategoryAsync(_beta, "Work");
        DashboardViewModel dashboard = New();
        await dashboard.LoadAsync();

        await dashboard.RenameCategoryAsync("Work", "Job");

        dashboard.Groups.Single().Header.Should().Be("Job");
        dashboard.CountIn("Job").Should().Be(2);

        await dashboard.RenameCategoryAsync("Job", null);

        dashboard.Groups.Should().BeEmpty();
    }

    [Test]
    public async Task RemoveMissingAsync_and_ClearRecentAsync_should_drop_entries()
    {
        await _store.AddAsync(_beta);
        await _store.AddAsync(_alpha);
        _missing.Add(_beta);
        DashboardViewModel dashboard = New();
        await dashboard.LoadAsync();

        await dashboard.RemoveMissingAsync();

        dashboard.Groups.Single().Repositories.Select(repository => repository.Path).Should().Equal(_alpha);

        await dashboard.ClearRecentAsync();

        dashboard.Groups.Should().BeEmpty();
        (await _store.LoadAsync()).Should().BeEmpty();
    }

    private DashboardViewModel New()
        => new(_store, path => _missing.Contains(path)
                ? new DashboardRepositoryState(false, null)
                : new DashboardRepositoryState(true, "main"),
            () => new RecentRepoSplitter
            {
                ShorteningStrategy = ShorteningRecentRepoPathStrategy.None,
                MaxTopRepositories = 0,
                HideTopRepositoriesFromRecentList = false,
                SortTopRepos = false,
                SortRecentRepos = false,
                RecentReposComboMinWidth = 0,
            });

    // A path on a drive or root of its own, so it is never under the user's home folder.
    private static string Rooted(string name)
        => Path.Combine(OperatingSystem.IsWindows() ? @"Q:\" : "/xplat-outside-home", name);
}

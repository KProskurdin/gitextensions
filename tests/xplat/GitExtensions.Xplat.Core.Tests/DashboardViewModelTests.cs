using AwesomeAssertions;
using GitCommands;
using GitCommands.UserRepositoryHistory;
using GitExtensions.Xplat.Core.Presentation;
using GitExtensions.Xplat.Core.Repository;
using NSubstitute;
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

    [Test]
    public async Task The_recent_groups_header_should_be_translated_and_a_missing_folder_should_show_the_error_image()
    {
        await _store.AddAsync(_alpha);
        await _store.AddAsync(_beta);
        _missing.Add(_beta);
        DashboardViewModel dashboard = New(text => "T:" + text);

        await dashboard.LoadAsync();

        dashboard.Groups.Single().Header.Should().Be("T:" + DashboardViewModel.RecentGroupHeader);
        dashboard.Groups.Single().Repositories.Select(repository => (repository.Path, repository.FolderImage)).Should()
            .Equal((_beta, "DashboardFolderError"), (_alpha, "DashboardFolderGit"));
    }

    [Test]
    public async Task A_tiles_menu_should_list_the_categories_and_put_the_repository_in_the_one_clicked()
    {
        await _store.AddAsync(_beta);
        await _store.AddAsync(_alpha);
        await _store.AssignCategoryAsync(_gamma, "Work");
        _missing.Add(_beta);
        IDashboardWindow window = Substitute.For<IDashboardWindow>();
        DashboardViewModel dashboard = New();
        dashboard.Window = window;
        await dashboard.LoadAsync();
        DashboardRepository alpha = dashboard.Groups[0].Repositories.Single(repository => repository.Path == _alpha);

        IReadOnlyList<MenuEntry> menu = dashboard.RepositoryMenu(alpha);

        menu.Where(entry => !entry.IsSeparator).Select(entry => entry.Text).Should().Equal(
            DashboardViewModel.ShowInFolderText, DashboardViewModel.CategoriesText, DashboardViewModel.RemoveText,
            DashboardViewModel.RemoveMissingText);
        menu[^1].IsVisible.Should().BeTrue("a shown folder is no longer a repository");
        IReadOnlyList<MenuEntry> categories = menu[2].Children;
        categories.Select(entry => (entry.Text, entry.IsEnabled)).Should().Equal(
            (DashboardViewModel.NoCategoryText, false), ("Work", true), ("", true), (DashboardViewModel.AddCategoryText, true));

        menu[0].Command!.Execute(menu[0].CommandParameter);
        categories[1].Command!.Execute(categories[1].CommandParameter);
        await WaitForAsync(() => window.ReceivedCalls().Any(call =>
            call.GetMethodInfo().Name == nameof(IDashboardWindow.RepositoriesChangedAsync)));

        window.Received(1).OpenFolder(_alpha);
        dashboard.CountIn("Work").Should().Be(2);
    }

    [Test]
    public async Task A_groups_menu_should_delete_its_category_only_when_confirmed()
    {
        await _store.AssignCategoryAsync(_alpha, "Work");
        await _store.AddAsync(_beta);
        IDashboardWindow window = Substitute.For<IDashboardWindow>();
        window.AskAsync(default!, default!).ReturnsForAnyArgs(false, true);
        DashboardViewModel dashboard = New(text => "T:" + text);
        dashboard.Window = window;
        await dashboard.LoadAsync();
        DashboardGroup work = dashboard.Groups.Single(group => group.Category == "Work");

        IReadOnlyList<MenuEntry> recentMenu = dashboard.GroupMenu(dashboard.Groups[0]);
        IReadOnlyList<MenuEntry> menu = dashboard.GroupMenu(work);

        recentMenu.Select(entry => entry.IsVisible).Should().Equal(false, false, true);
        menu.Select(entry => entry.IsVisible).Should().Equal(true, true, false);
        menu[1].Command!.Execute(menu[1].CommandParameter);
        await window.Received(1).AskAsync(
            string.Format("T:" + DashboardViewModel.DeleteCategoryQuestion, "Work", 1), "T:" + DashboardViewModel.DeleteCategoryCaption);
        dashboard.CountIn("Work").Should().Be(1, "the question was answered no");

        menu[1].Command!.Execute(menu[1].CommandParameter);
        await WaitForAsync(() => dashboard.Groups.All(group => group.Category != "Work"));
        await window.Received(1).RepositoriesChangedAsync();
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (!condition())
        {
            DateTime.UtcNow.Should().BeBefore(deadline, "the command should finish");
            await Task.Delay(10);
        }
    }

    private DashboardViewModel New(Func<string, string>? translate = null)
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
            }, translate);

    // A path on a drive or root of its own, so it is never under the user's home folder.
    private static string Rooted(string name)
        => Path.Combine(OperatingSystem.IsWindows() ? @"Q:\" : "/xplat-outside-home", name);
}

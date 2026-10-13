using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using GitCommands;
using GitExtensions.Xplat.Core.Repository;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

internal sealed class DashboardTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(20);

    private TestRepository _repo = null!;
    private string _missing = null!;

    [SetUp]
    public async Task Setup()
    {
        _repo = new TestRepository();
        _missing = Path.Combine(Path.GetTempPath(), $"xplat-missing-{Guid.NewGuid():N}");
        await AppServices.RecentRepositories.AddAsync(_missing);
        await AppServices.RecentRepositories.AddAsync(_repo.Path);
    }

    [TearDown]
    public async Task TearDown()
    {
        foreach (string path in new[] { _repo.Path, _missing })
        {
            await AppServices.RecentRepositories.RemoveAsync(path);
            await AppServices.RecentRepositories.AssignCategoryAsync(path, null);
        }

        _repo.Dispose();
    }

    [AvaloniaTest]
    public async Task The_dashboard_shows_the_recent_repositories_and_the_favourites_by_category()
    {
        await AppServices.RecentRepositories.AssignCategoryAsync(_repo.Path, "Work");
        MainWindow window = OpenWindow();

        WaitUntil(() => Groups(window).Contains("Work") && Tile(window, _missing) is { IsValid: false });

        Groups(window).Should().StartWith(DashboardViewModel.RecentGroupHeader);
        DashboardRepository favourite = Tiles(window).Select(tile => (DashboardRepository)tile.Tag!)
            .Single(repository => repository.IsFavourite);
        (favourite.Category, favourite.HasBranch).Should().Be(("Work", true));
    }

    [AvaloniaTest]
    public async Task The_tile_menu_puts_a_repository_in_a_category_and_removes_one_from_the_list()
    {
        await AppServices.RecentRepositories.AssignCategoryAsync(_missing, "Work");
        MainWindow window = OpenWindow();
        WaitUntil(() => Groups(window).Contains("Work") && TileButton(window, _repo.Path, favourite: false) is not null);

        MenuItem work = CategoryItems(window, TileButton(window, _repo.Path, favourite: false)!)
            .Single(item => item.Header as string == "Work");
        work.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

        WaitUntil(() => TileButton(window, _repo.Path, favourite: true) is not null);

        Button missing = TileButton(window, _missing, favourite: true)!;
        missing.ContextMenu!.Items.OfType<MenuItem>().Single(item => item.Header as string == "Remove project from the list")
            .RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

        WaitUntil(() => TileButton(window, _missing, favourite: true) is null);
        TileButton(window, _missing, favourite: false).Should().NotBeNull("only the favourite was removed");
    }

    [AvaloniaTest]
    public void Clicking_a_tile_opens_the_repository()
    {
        MainWindow window = OpenWindow();
        WaitUntil(() => TileButton(window, _repo.Path, favourite: false) is not null);

        TileButton(window, _repo.Path, favourite: false)!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        WaitUntil(() => Find<Grid>(window, "BrowsePanel").IsVisible && !window.IsBusy);
        Find<Grid>(window, "DashboardPanel").IsVisible.Should().BeFalse();
    }

    // The "Categories" submenu fills itself when the tile's menu opens.
    private static IReadOnlyList<MenuItem> CategoryItems(Window window, Button tile)
    {
        tile.RaiseEvent(new ContextRequestedEventArgs());
        Dispatcher.UIThread.RunJobs();
        ContextMenu menu = tile.ContextMenu!;
        menu.Close();
        return [.. (menu.Items.OfType<MenuItem>().Single(item => item.Header as string == "Categories").ItemsSource
                    as IEnumerable<Control> ?? []).OfType<MenuItem>()];
    }

    private static IReadOnlyList<string> Groups(Window window)
        =>
        [
            .. Find<StackPanel>(window, "DashboardGroups").Children.OfType<StackPanel>()
                .Select(group => group.Name ?? "")
        ];

    private static DashboardRepository? Tile(Window window, string path)
        => Tiles(window).Select(tile => (DashboardRepository)tile.Tag!)
            .FirstOrDefault(repository => RecentRepositoryPaths.Same(repository.Path, path));

    private static Button? TileButton(Window window, string path, bool favourite)
        => Tiles(window).FirstOrDefault(tile => tile.Tag is DashboardRepository repository
                                                && repository.IsFavourite == favourite
                                                && RecentRepositoryPaths.Same(repository.Path, path));

    private static IEnumerable<Button> Tiles(Window window)
        => Find<StackPanel>(window, "DashboardGroups").GetLogicalDescendants().OfType<Button>()
            .Where(button => button.Classes.Contains("dashboardTile"));

    private static MainWindow OpenWindow()
    {
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        window.Show();
        return window;
    }

    private static T Find<T>(Window window, string name) where T : Control
        => window.FindControl<T>(name) ?? throw new InvalidOperationException($"Control {name} not found");

    private static void WaitUntil(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow + _timeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail("Timed out waiting for the UI");
            }

            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(20);
        }
    }
}

using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using GitCommands;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

// The macOS menu bar's copy of the browse window's menu (PLAN.md question 15). The copy is a NativeMenu, which the headless
// platform does not show, so these open it as macOS's opening does and click it through Avalonia's exporter call, on
// every OS.
internal sealed class SystemMenuBarTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(20);

    private TestRepository _repo = null!;

    [SetUp]
    public void Setup() => _repo = new TestRepository();

    [TearDown]
    public void TearDown() => _repo.Dispose();

    [AvaloniaTest]
    public void The_top_menus_are_the_windows_and_follow_it_from_the_dashboard_to_a_repository()
    {
        MainWindow window = NewWindow();
        window.Show();
        Menu menu = window.FindControl<Menu>("MainMenu")!;
        NativeMenu bar = SystemMenuBar.Mirror(menu);
        Top(bar).Should().Equal(Top(menu));
        Top(bar).Should().Contain(("Dashboard", true));

        Open(window, _repo.Path);

        Top(bar).Should().Equal(Top(menu));
        Top(bar).Should().Contain([("Dashboard", false), ("Repository", true), ("Commands", true)]);
    }

    [AvaloniaTest]
    public void A_submenu_is_copied_when_it_opens_with_texts_separators_and_enabled_state()
    {
        MainWindow window = NewWindow();
        window.Show();
        Open(window, _repo.Path);
        MenuItem commands = window.FindControl<MenuItem>("CommandsMenu")!;

        NativeMenu copy = Submenu(SystemMenuBar.Mirror(window.FindControl<Menu>("MainMenu")!), "Commands");

        copy.Items.Select(Row).Should().Equal(commands.Items.Select(item => item switch
        {
            MenuItem source => (SystemMenuBar.Text(source.Header),
                source.IsEnabled && (source.Command?.CanExecute(source.CommandParameter) ?? true)),
            _ => ("-", true),
        }));
        copy.Items.Select(Row).Should().Contain([("Commit...", true), ("Pull/Fetch...", false)],
            "a repository without remotes has nothing to pull from");
    }

    [AvaloniaTest]
    public void A_click_on_a_copy_runs_the_windows_item_and_the_next_opening_shows_its_check()
    {
        MainWindow window = NewWindow();
        window.Show();
        Open(window, _repo.Path);
        bool shown = window.GridViewMenu.ShowTags;
        NativeMenu view = Submenu(SystemMenuBar.Mirror(window.FindControl<Menu>("MainMenu")!), "View");
        NativeMenuItem showTags = Item(view, "Show tags");
        showTags.IsChecked.Should().Be(shown);

        ((INativeMenuItemExporterEventsImplBridge)showTags).RaiseClicked();
        Dispatcher.UIThread.RunJobs();

        window.GridViewMenu.ShowTags.Should().Be(!shown, "the copy runs the View menu's command once");
        SystemMenuBar.Open(view);
        Item(view, "Show tags").IsChecked.Should().Be(!shown);
    }

    [AvaloniaTest]
    public void The_windows_menu_stays_where_the_platform_shows_no_system_menu()
    {
        MainWindow window = NewWindow();
        window.Show();
        Menu menu = window.FindControl<Menu>("MainMenu")!;

        SystemMenuBar.Show(window, menu);
        Dispatcher.UIThread.RunJobs();

        NativeMenu.GetMenu(window).Should().NotBeNull();
        menu.IsVisible.Should().BeTrue();
    }

    [AvaloniaTest]
    [TestCase("_Commands", "Commands")]
    [TestCase("Pull_/Fetch...", "Pull/Fetch...")]
    [TestCase("Show SHA_-1 column", "Show SHA-1 column")]
    [TestCase("a__b", "a_b")]
    [TestCase("end_", "end_")]
    public void A_text_loses_its_access_keys(string header, string expected)
        => SystemMenuBar.Text(header).Should().Be(expected);

    private static NativeMenuItem Item(NativeMenu menu, string header)
        => menu.Items.OfType<NativeMenuItem>().Single(item => item.Header == header);

    private static (string Header, bool Enabled) Row(NativeMenuItemBase item)
        => item is NativeMenuItem { } native ? (native.Header ?? "", native.IsEnabled) : ("-", true);

    private static NativeMenu Submenu(NativeMenu bar, string header)
    {
        NativeMenu menu = Item(bar, header).Menu!;
        SystemMenuBar.Open(menu);
        return menu;
    }

    private static (string Header, bool Visible)[] Top(NativeMenu bar)
        => [.. bar.Items.OfType<NativeMenuItem>().Select(item => (item.Header ?? "", item.IsVisible))];

    private static (string Header, bool Visible)[] Top(Menu menu)
        => [.. menu.Items.OfType<MenuItem>().Select(item => (SystemMenuBar.Text(item.Header), item.IsVisible))];

    private static MainWindow NewWindow() =>
        new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));

    private static void Open(MainWindow window, string path)
    {
        window.OpenRepository(path);
        WaitUntil(() => !window.IsBusy);
    }

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

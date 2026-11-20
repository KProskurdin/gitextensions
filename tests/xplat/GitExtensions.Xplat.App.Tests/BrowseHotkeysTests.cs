using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Threading;
using GitCommands;
using GitExtensions.Xplat.Core.CommitHistory;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

/// <summary>
///  Upstream FormBrowse's and RevisionGridControl's hotkeys of features the browse window has: the focus of its tabs, the
///  View menu's choices, the fork point and the selected commit's refs.
/// </summary>
internal sealed class BrowseHotkeysTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(20);

    private TestRepository _repo = null!;
    private bool _showTags;

    [SetUp]
    public void Setup()
    {
        _repo = new TestRepository();
        _showTags = TestAppBuilder.Preferences.GridViewOptions.ShowTags;
    }

    [TearDown]
    public void TearDown()
    {
        TestAppBuilder.Preferences.GridViewOptions = TestAppBuilder.Preferences.GridViewOptions with { ShowTags = _showTags };
        _repo.Dispose();
    }

    [AvaloniaTest]
    public void The_focus_keys_show_their_tab_and_the_tab_keys_go_round_the_tabs_shown()
    {
        MainWindow window = OpenWindow();
        TabControl tabs = Find<TabControl>(window, "DetailTabs");
        ListBox grid = Find<ListBox>(window, "CommitList");

        Press(grid, Key.D3, Hotkeys.CommandModifier);
        tabs.SelectedItem.Should().Be(Find<TabItem>(window, "DiffTab"));
        WaitUntil(() => Find<ListBox>(window, "CommitFilesList").IsKeyboardFocusWithin);

        Press(grid, Key.D4, Hotkeys.CommandModifier);
        tabs.SelectedItem.Should().Be(Find<TabItem>(window, "FileTreeTab"));

        Press(grid, Key.D2, Hotkeys.CommandModifier);
        tabs.SelectedItem.Should().Be(Find<TabItem>(window, "CommitInfoTab"));

        // macOS's tab keys there, upstream's elsewhere.
        if (OperatingSystem.IsMacOS())
        {
            Press(grid, Key.OemCloseBrackets, Hotkeys.CommandModifier | KeyModifiers.Shift);
        }
        else
        {
            Press(grid, Key.Tab, Hotkeys.CommandModifier);
        }

        tabs.SelectedItem.Should().Be(Find<TabItem>(window, "DiffTab"));
        window.Close();
    }

    [AvaloniaTest]
    public void A_grid_key_toggles_its_view_choice_and_the_fork_point_key_selects_the_base()
    {
        _repo.Run("checkout", "-q", "-b", "feature");
        File.WriteAllText(Path.Combine(_repo.Path, "f.txt"), "f");
        _repo.Run("add", "f.txt");
        _repo.Run("commit", "-q", "-m", "on feature");
        MainWindow window = OpenWindow();
        ListBox grid = Find<ListBox>(window, "CommitList");
        bool tags = window.GridViewMenu.ShowTags;

        Press(grid, Key.T, Hotkeys.CommandModifier | KeyModifiers.Alt);
        window.GridViewMenu.ShowTags.Should().Be(!tags);
        Find<MenuItem>(window, "ShowTagsMenuItem").InputGesture.Should()
            .Be(new KeyGesture(Key.T, Hotkeys.CommandModifier | KeyModifiers.Alt), "upstream's View menu shows the key");

        // From "on feature", the next commit with a branch is "second", where the other branch stays.
        grid.SelectedItem = grid.Items.OfType<CommitListItem>().First(item => item.Row.Subject == "on feature");
        Press(grid, Key.K, Hotkeys.CommandModifier);
        window.SelectedRevisions.Select(row => row.Subject).Should().Equal("on feature", "second");
        window.Close();
    }

    [AvaloniaTest]
    public void Rename_ref_with_one_branch_opens_the_rename_window_at_once()
    {
        MainWindow window = OpenWindow();
        ListBox grid = Find<ListBox>(window, "CommitList");
        grid.SelectedItem = grid.Items.OfType<CommitListItem>().First(item => item.Row.Subject == "second");

        Press(grid, Key.F2, KeyModifiers.None);

        WaitUntil(() => window.OwnedWindows.OfType<RenameBranchWindow>().Any());
        window.OwnedWindows.OfType<RenameBranchWindow>().Single().Close();
        window.Close();
    }

    private MainWindow OpenWindow()
    {
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        window.Show();
        window.OpenRepository(_repo.Path);
        WaitUntil(() => !window.IsBusy && Find<ListBox>(window, "CommitList").ItemCount > 0);
        return window;
    }

    // The key as the grid (or a control in it) gets it.
    private static void Press(Control source, Key key, KeyModifiers modifiers)
    {
        source.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, KeyModifiers = modifiers });
        Dispatcher.UIThread.RunJobs();
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
            Thread.Sleep(10);
        }
    }
}

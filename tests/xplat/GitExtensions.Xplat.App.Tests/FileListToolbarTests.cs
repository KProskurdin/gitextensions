using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using GitCommands;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

/// <summary>
///  Upstream's <c>tsmiToolbar</c>, on a window's real toolbar.
/// </summary>
internal sealed class FileListToolbarTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(20);

    private readonly List<string> _hidden = [];

    [TearDown]
    public void TearDown()
    {
        foreach (string name in _hidden)
        {
            TestAppBuilder.Preferences.SetFileListToolbarItemVisible(name, true);
        }
    }

    [AvaloniaTest]
    public void The_diff_tabs_settings_menu_lists_the_toolbar_with_upstreams_names_and_texts()
    {
        MainWindow window = NewWindow();

        MenuItem toolbar = ToolbarMenu(window, "DiffFilesSettingsButton");

        List<MenuItem> entries = [.. toolbar.Items.OfType<MenuItem>()];
        entries.Select(entry => entry.Header).Should().Contain(["Toggle flat list / tree", "Separator 'Toggle flat list / tree'", "Group by file path", "Settings"]);
        entries.Single(entry => (string?)entry.Header == "Settings").IsEnabled.Should().BeFalse("upstream's settings button stays");
        entries.Should().OnlyContain(entry => entry.IsChecked);
        window.Close();
    }

    [AvaloniaTest]
    public void Every_name_is_one_of_upstreams_so_the_setting_is_shared()
    {
        MainWindow window = NewWindow();
        string designer = UpstreamSource("src/app/GitUI/UserControls/FileStatusList.Designer.cs");
        string[] upstream = Regex.Match(designer, @"Toolbar\.Items\.AddRange\(new ToolStripItem\[\] \{([^}]*)\}").Groups[1].Value
            .Split(',', StringSplitOptions.TrimEntries);

        IReadOnlyList<FileListToolbarItem> items = FileListToolbar.Attach(window.FindControl<Control>("DiffFilesSettingsButton")!, TestAppBuilder.Preferences);

        upstream.Should().Contain("btnRefresh");
        items.Select(item => item.UpstreamName).Should().BeSubsetOf(upstream);
        items.Select(item => item.UpstreamName).Should().ContainInOrder(upstream.Where(items.Select(item => item.UpstreamName).Contains));
        window.Close();
    }

    [AvaloniaTest]
    public void Unchecking_hides_the_item_stores_upstreams_setting_and_holds_for_the_next_window()
    {
        MainWindow window = NewWindow();
        MenuItem entry = ToolbarMenu(window, "DiffFilesSettingsButton").Items.OfType<MenuItem>()
            .Single(item => (string?)item.Header == "Group by file path");
        Control button = window.FindControl<Control>("DiffFilesByPathButton")!;
        _hidden.Add("btnByPath");

        entry.IsChecked = false;
        entry.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

        button.Classes.Should().Contain(FileListToolbar.HiddenClass);
        TestAppBuilder.Preferences.IsFileListToolbarItemVisible("btnByPath").Should().BeFalse();
        window.Close();

        MainWindow next = NewWindow();
        next.FindControl<Control>("DiffFilesByPathButton")!.Classes.Should().Contain(FileListToolbar.HiddenClass);
        ToolbarMenu(next, "DiffFilesSettingsButton").Items.OfType<MenuItem>()
            .Single(item => (string?)item.Header == "Group by file path").IsChecked.Should().BeFalse();
        next.Close();
    }

    [AvaloniaTest]
    public void A_hidden_item_takes_no_room_even_when_the_toolbars_rules_show_it()
    {
        TestAppBuilder.Preferences.SetFileListToolbarItemVisible("btnUnequalChange", false);
        _hidden.Add("btnUnequalChange");
        MainWindow window = NewWindow();
        Control button = window.FindControl<Control>("DiffFilesUnequalChangeButton")!;

        button.IsVisible = true;
        button.Measure(new Avalonia.Size(200, 40));

        button.DesiredSize.Width.Should().Be(0);
        window.Close();
    }

    private static string UpstreamSource(string relativePath, [CallerFilePath] string thisFile = "")
        => File.ReadAllText(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", "..", relativePath)));

    private static MainWindow NewWindow()
    {
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static MenuItem ToolbarMenu(MainWindow window, string settingsButton)
        => ((MenuFlyout)window.FindControl<Button>(settingsButton)!.Flyout!).Items.OfType<MenuItem>()
            .Single(item => (string?)item.Header == "_Toolbar");
}

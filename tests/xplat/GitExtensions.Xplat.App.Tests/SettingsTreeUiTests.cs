using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using GitCommands;
using GitExtensions.Extensibility.Settings;
using GitExtensions.Xplat.Core.Settings;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

internal sealed class SettingsTreeUiTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(20);

    [TearDown]
    public void TearDown() => TestAppBuilder.Preferences.RelativeDate = true;

    [AvaloniaTest]
    public void The_tree_has_upstreams_groups_and_pages_with_their_images()
    {
        SettingsWindow settings = OpenSettings();

        settings.PageTree.Select(node => node.Title).Should().Equal("Git Extensions", "Git", "Plugins");
        settings.PageTree[0].Children.Select(node => node.Title).Should().Equal("General", "Appearance",
            "Revision links", "Build server integration", "Scripts", "Hotkeys", "Advanced", "Detailed", "SSH");
        Child(settings, 0, "Appearance").Children.Select(node => node.Title).Should()
            .Equal("Sorting", "Colors", "Fonts");
        Child(settings, 0, "Advanced").Children.Select(node => node.Title).Should().Equal("Confirmations");
        Child(settings, 0, "Detailed").Children.Select(node => node.Title).Should()
            .Equal("Browse repository window", "Commit dialog", "Diff viewer", "Blame viewer");
        settings.PageTree[1].Children.Select(node => node.Title).Should().Equal("Paths", "Config");
        Descendants(settings.PageTree).Where(node => node.Plugin is null).Should()
            .OnlyContain(node => node.Icon != null);
        settings.Close();
    }

    [AvaloniaTest]
    public void A_group_shows_its_own_page_or_its_first_page_and_the_title_names_it()
    {
        SettingsWindow settings = OpenSettings();
        TabControl tabs = Find<TabControl>(settings, "Tabs");

        settings.GoToPage(settings.PageTree[1]);
        tabs.SelectedItem.Should().Be(Find<TabItem>(settings, "GitRootTab"));
        settings.Title.Should().Be("Settings - Git Settings");

        settings.GoToPage(settings.PageTree[0]);
        tabs.SelectedItem.Should().Be(Find<TabItem>(settings, "ChecklistTab"));
        settings.Title.Should().Be("Settings - Checklist");

        settings.GoToPage(Child(settings, 0, "General"));
        settings.Title.Should().Be("Settings - General");

        // A page shown by its tab, as on a save error, selects its node.
        tabs.SelectedItem = Find<TabItem>(settings, "SshTab");
        settings.SelectedPage!.Title.Should().Be("SSH");
        settings.Close();
    }

    [AvaloniaTest]
    public void The_next_settings_window_opens_on_the_page_shown_last()
    {
        SettingsWindow first = OpenSettings();
        first.GoToPage(Child(first, 0, "Appearance").Children.Single(node => node.Title == "Fonts"));
        first.Close();

        SettingsWindow second = OpenSettings();

        second.SelectedPage!.Title.Should().Be("Fonts");
        Find<TabControl>(second, "Tabs").SelectedItem.Should().Be(Find<TabItem>(second, "FontsTab"));
        Child(second, 0, "Appearance").IsExpanded.Should().BeTrue();
        second.Close();
    }

    [AvaloniaTest]
    public void The_search_box_marks_the_pages_with_the_text_and_enter_shows_them_in_turn()
    {
        SettingsWindow settings = OpenSettings();
        TextBox search = Find<TextBox>(settings, "SettingsSearchBox");
        search.PlaceholderText.Should().Be(SettingsPageTree.TypeToFind);

        search.Text = "colorblind";
        Dispatcher.UIThread.RunJobs();
        Descendants(settings.PageTree).Where(node => node.IsFound).Select(node => node.Title).Should()
            .Equal("Colors");
        Child(settings, 0, "Appearance").IsExpanded.Should().BeTrue();
        PressEnter(search);
        settings.SelectedPage!.Title.Should().Be("Colors");

        // The Hotkeys page names the diff viewer's hotkeys.
        search.Text = "viewer";
        Dispatcher.UIThread.RunJobs();
        Descendants(settings.PageTree).Where(node => node.IsFound).Select(node => node.Title).Should()
            .Equal("Hotkeys", "Diff viewer", "Blame viewer");
        PressEnter(search);
        settings.SelectedPage!.Title.Should().Be("Hotkeys");
        PressEnter(search);
        settings.SelectedPage!.Title.Should().Be("Diff viewer");
        PressEnter(search);
        settings.SelectedPage!.Title.Should().Be("Blame viewer");
        PressEnter(search);
        settings.SelectedPage!.Title.Should().Be("Hotkeys");

        search.Text = "";
        Dispatcher.UIThread.RunJobs();
        Descendants(settings.PageTree).Should().NotContain(node => node.IsFound);
        settings.Close();
    }

    [AvaloniaTest]
    public void Each_page_shows_its_settings_source_and_is_read_only_where_upstreams_is()
    {
        using TestRepository repo = new();
        SettingsWindow settings = OpenSettings(repo.Path);

        SettingsSourceHeader general = settings.SourceHeaderOf(Find<TabItem>(settings, "GeneralTab"));
        general.Levels.Should().Equal(SettingLevel.Global);
        general.IsReadOnly.Should().BeFalse();
        general.GetLogicalDescendants().OfType<TextBlock>().First().Text.Should().Be("Settings source:");
        general.GetLogicalDescendants().OfType<RadioButton>().Select(button => button.Content).Should()
            .Equal("Global for all repositories");

        Find<TabControl>(settings, "Tabs").SelectedItem = Find<TabItem>(settings, "DetailedTab");
        Dispatcher.UIThread.RunJobs();
        SettingsSourceHeader detailed = settings.SourceHeaderOf(Find<TabItem>(settings, "DetailedTab"));
        CheckBox graphLanes = Find<CheckBox>(settings, "MergeGraphLanesCheck");
        detailed.GetLogicalDescendants().OfType<RadioButton>().Select(button => button.Content).Should().Equal(
            "Effective", "Local for current repository", "Distributed with current repository",
            "Global for all repositories");
        detailed.GetLogicalDescendants().OfType<TextBlock>().Where(text => text.Text == "<<").Should()
            .HaveCount(3).And.OnlyContain(arrow => arrow.Opacity == 1, "the arrows show while Effective is chosen");
        graphLanes.IsEffectivelyEnabled.Should().BeFalse();
        detailed.Level = SettingLevel.Local;
        graphLanes.IsEffectivelyEnabled.Should().BeFalse("upstream edits the revision graph settings globally only");
        detailed.GetLogicalDescendants().OfType<TextBlock>().Where(text => text.Text == "<<").Should()
            .OnlyContain(arrow => arrow.Opacity == 0);
        detailed.Level = SettingLevel.Global;
        graphLanes.IsEffectivelyEnabled.Should().BeTrue();
        settings.Close();
    }

    [AvaloniaTest]
    public void The_checklist_shows_a_row_per_check_and_repair_shows_the_page_to_fix_it_on()
    {
        FakeGitConfigService config = TestAppBuilder.GitConfig;
        config.Set(ConfigScope.Global, "user.name", "Test");
        config.Set(ConfigScope.Global, "user.email", "test@example.com");
        config.Set(ConfigScope.Global, "merge.tool", "meld");
        TestAppBuilder.Preferences.Translation = "English";
        try
        {
            SettingsWindow settings = OpenSettings();
            settings.GoToPage(settings.PageTree[0]);
            WaitUntil(() => settings.ChecklistEntries.Count > 0);

            settings.Title.Should().Be("Settings - Checklist");
            Find<TextBlock>(settings, "InstantSaveNoticeText").IsVisible.Should().BeTrue();
            settings.ChecklistEntries.Select(entry => (entry.Item, entry.State)).Should().Contain(
            [
                (ChecklistItem.UserName, ChecklistState.Set), (ChecklistItem.MergeTool, ChecklistState.Set),
                (ChecklistItem.DiffTool, ChecklistState.Unset), (ChecklistItem.Language, ChecklistState.Set),
            ]);
            settings.ChecklistEntries.Single(entry => entry.Item == ChecklistItem.MergeTool).Text.Should()
                .Be("There is a mergetool configured: meld", "git starts meld itself");
            Grid rows = Find<Grid>(settings, "ChecklistGrid");
            int diffRow = Grid.GetRow(rows.Children.OfType<Button>()
                .Single(button => Equals(button.Tag, ChecklistItem.DiffTool)));
            Button repair = rows.Children.OfType<Button>()
                .Single(button =>
                    Grid.GetRow(button) == diffRow && Equals(button.Content, SettingsChecklist.RepairText));
            rows.Children.OfType<Button>().Where(button => Equals(button.Content, SettingsChecklist.RepairText))
                .Should().HaveCount(settings.ChecklistEntries.Count(entry => entry.State != ChecklistState.Set));

            repair.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            settings.SelectedPage!.Title.Should().Be("Config");
            Find<TextBlock>(settings, "InstantSaveNoticeText").IsVisible.Should().BeFalse();
            settings.Close();
        }
        finally
        {
            foreach (string key in (string[])["user.name", "user.email", "merge.tool"])
            {
                config.SetAsync(ConfigScope.Global, key, "").Wait();
            }

            TestAppBuilder.Preferences.Translation = "";
        }
    }

    [AvaloniaTest]
    public void Apply_saves_and_keeps_the_window_open()
    {
        TestAppBuilder.Preferences.RelativeDate = true;
        SettingsWindow settings = OpenSettings();
        Find<CheckBox>(settings, "RelativeDateCheck").IsChecked = false;

        Click(settings, "ApplyButton");

        WaitUntil(() => !TestAppBuilder.Preferences.RelativeDate);
        settings.IsVisible.Should().BeTrue();
        Click(settings, "CancelButton");
        settings.IsVisible.Should().BeFalse();
    }

    private static SettingsPageNode Child(SettingsWindow settings, int root, string title)
        => settings.PageTree[root].Children.Single(node => node.Title == title);

    private static IEnumerable<SettingsPageNode> Descendants(IEnumerable<SettingsPageNode> nodes)
        => nodes.SelectMany(node => (IEnumerable<SettingsPageNode>)[node, .. Descendants(node.Children)]);

    private static void PressEnter(TextBox box)
        => box.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Enter });

    private static SettingsWindow OpenSettings(string? repository = null)
    {
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        window.Show();
        if (repository is not null)
        {
            window.OpenRepository(repository);
            WaitUntil(() => !window.IsBusy);
        }

        Click(window, "SettingsMenuItem");
        WaitUntil(() => window.OwnedWindows.OfType<SettingsWindow>().Any());
        SettingsWindow settings = window.OwnedWindows.OfType<SettingsWindow>().Single();
        WaitUntil(() => settings.IsGitConfigLoaded);
        return settings;
    }

    private static void Click(Window window, string name)
    {
        Control control = Find<Control>(window, name);
        RoutedEvent click = control is MenuItem ? MenuItem.ClickEvent : Button.ClickEvent;
        control.RaiseEvent(new RoutedEventArgs(click));
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

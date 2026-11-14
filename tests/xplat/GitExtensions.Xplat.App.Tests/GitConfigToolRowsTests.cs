using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using GitCommands;
using GitExtensions.Xplat.Core.Settings;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

/// <summary>
///  Upstream's Git config page beyond the plain values: the tools' path and command rows, the files encoding, and the page
///  without git.
/// </summary>
internal sealed class GitConfigToolRowsTests
{
    private const string GoodGit = "/good/git";
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(20);

    private Func<string?, Task<bool>> _canFindGit = null!;
    private Encoding[] _encodings = [];

    [SetUp]
    public void Setup()
    {
        _canFindGit = AppServices.CanFindGit;
        _encodings = [.. AppSettings.AvailableEncodings.Values];

        // The app's startup fills the list (AppSettings.LoadSettings); the tests start no Program.Main.
        AvailableEncodingsDialog.Apply(AppSettings.AvailableEncodings, [new UTF8Encoding(false), Encoding.Latin1]);
        TestAppBuilder.GitConfig.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        AppServices.CanFindGit = _canFindGit;
        AvailableEncodingsDialog.Apply(AppSettings.AvailableEncodings, _encodings);
        TestAppBuilder.GitConfig.Clear();
    }

    [AvaloniaTest]
    public void The_rows_show_what_is_stored_for_the_tool_and_are_disabled_without_one()
    {
        TestAppBuilder.GitConfig.Set(ConfigScope.Global, "merge.guitool", "mytool");
        TestAppBuilder.GitConfig.Set(ConfigScope.Global, "mergetool.mytool.path", "/opt/mytool");
        TestAppBuilder.GitConfig.Set(ConfigScope.Global, "mergetool.mytool.cmd", "mytool $MERGED");

        SettingsWindow settings = OpenSettings(out MainWindow window);

        Find<TextBox>(settings, "MergeToolPathBox").Text.Should().Be("/opt/mytool");
        Find<TextBox>(settings, "MergeToolCommandBox").Text.Should().Be("mytool $MERGED");
        Find<TextBox>(settings, "MergeToolPathBox").IsEnabled.Should().BeTrue();
        Find<TextBox>(settings, "DiffToolPathBox").IsEnabled.Should().BeFalse("no diff tool is named");
        Find<Button>(settings, "SuggestDiffToolButton").IsEnabled.Should().BeFalse();
        window.Close();
    }

    [AvaloniaTest]
    public void A_stored_tool_missing_from_the_list_survives_the_list_loading_and_a_save()
    {
        TestAppBuilder.GitConfig.Set(ConfigScope.Global, "merge.guitool", "not-in-the-list");
        TestAppBuilder.GitConfig.Set(ConfigScope.Global, "core.editor", "not-in-the-list-either");
        SettingsWindow settings = OpenSettings(out MainWindow window);
        ComboBox tools = Find<ComboBox>(settings, "MergeToolBox");
        WaitUntil(() => tools.ItemCount > 0);
        WaitUntil(() => settings.GitConfigListsLoaded.IsCompleted);
        Dispatcher.UIThread.RunJobs();

        tools.Text.Should().Be("not-in-the-list");
        Find<ComboBox>(settings, "EditorBox").Text.Should().Be("not-in-the-list-either");
        Save(window, settings);

        TestAppBuilder.GitConfig.Get(ConfigScope.Global, "merge.guitool").Should().Be("not-in-the-list");
        TestAppBuilder.GitConfig.Get(ConfigScope.Global, "core.editor").Should().Be("not-in-the-list-either");
    }

    [AvaloniaTest]
    public void A_tool_that_stayed_writes_only_what_was_edited()
    {
        TestAppBuilder.GitConfig.Set(ConfigScope.Global, "merge.guitool", "mytool");
        TestAppBuilder.GitConfig.Set(ConfigScope.Global, "mergetool.mytool.path", "/opt/mytool");
        SettingsWindow settings = OpenSettings(out MainWindow window);

        Find<TextBox>(settings, "MergeToolPathBox").Text = "/opt/other";
        Save(window, settings);

        TestAppBuilder.GitConfig.Get(ConfigScope.Global, "mergetool.mytool.path").Should().Be("/opt/other");
        TestAppBuilder.GitConfig.Get(ConfigScope.Global, "mergetool.mytool.cmd").Should()
            .BeNullOrEmpty("what was found on this computer does not become the user's setting");
    }

    [AvaloniaTest]
    public void A_newly_chosen_tool_writes_its_path_and_command_and_Suggest_fills_the_command()
    {
        SettingsWindow settings = OpenSettings(out MainWindow window);
        ComboBox tools = Find<ComboBox>(settings, "DiffToolBox");
        TextBox path = Find<TextBox>(settings, "DiffToolPathBox");
        TextBox command = Find<TextBox>(settings, "DiffToolCommandBox");

        tools.Text = "meld";
        WaitUntil(() => path.IsEnabled && !string.IsNullOrEmpty(path.Text));
        path.Text = "/opt/bin/meld";
        Click(settings, "SuggestDiffToolButton");
        command.Text.Should().Be("\"/opt/bin/meld\" \"$LOCAL\" \"$REMOTE\"");
        Save(window, settings);

        TestAppBuilder.GitConfig.Get(ConfigScope.Global, "diff.guitool").Should().Be("meld");
        TestAppBuilder.GitConfig.Get(ConfigScope.Global, "difftool.meld.path").Should().Be("/opt/bin/meld");
        TestAppBuilder.GitConfig.Get(ConfigScope.Global, "difftool.meld.cmd").Should().Be("\"/opt/bin/meld\" \"$LOCAL\" \"$REMOTE\"");
    }

    [AvaloniaTest]
    public void The_files_encoding_is_read_and_written_by_its_web_name()
    {
        TestAppBuilder.GitConfig.Set(ConfigScope.Global, "i18n.filesencoding", "UTF-8");
        SettingsWindow settings = OpenSettings(out MainWindow window);
        ComboBox encodings = Find<ComboBox>(settings, "FilesEncodingBox");
        Encoding utf8 = AppSettings.AvailableEncodings["utf-8"];
        Encoding other = AppSettings.AvailableEncodings.Values.First(encoding => encoding.WebName != "utf-8");

        encodings.SelectedItem.Should().Be(utf8.EncodingName, "git's setting is not case sensitive");
        encodings.SelectedItem = other.EncodingName;
        Save(window, settings);

        TestAppBuilder.GitConfig.Get(ConfigScope.Global, "i18n.filesencoding").Should().Be(other.WebName);
    }

    [AvaloniaTest]
    public void Without_git_the_page_says_so_and_the_git_command_can_still_be_corrected()
    {
        AppServices.CanFindGit = command => Task.FromResult(command == GoodGit);
        TestAppBuilder.Preferences.GitCommand = "/bad/git";
        try
        {
            TestAppBuilder.GitConfig.Set(ConfigScope.Global, "user.name", "Kept");
            SettingsWindow settings = OpenSettings(out MainWindow window, waitForGitConfig: false);
            TextBlock warning = Find<TextBlock>(settings, "InvalidGitPathText");
            WaitUntil(() => warning.IsVisible);
            Find<TextBox>(settings, "UserNameBox").IsEnabled.Should().BeFalse();
            Find<ComboBox>(settings, "MergeToolBox").IsEnabled.Should().BeFalse();
            WaitUntil(() => Find<Button>(settings, "SaveButton").IsEnabled);
            settings.IsGitConfigLoaded.Should().BeFalse();

            Find<TextBox>(settings, "GitCommandBox").Text = GoodGit;
            WaitUntil(() => !warning.IsVisible);
            Find<TextBox>(settings, "UserNameBox").IsEnabled.Should().BeTrue("what is typed is tried, as upstream's");
            Save(window, settings);

            TestAppBuilder.Preferences.GitCommand.Should().Be(GoodGit);
            TestAppBuilder.GitConfig.Get(ConfigScope.Global, "user.name").Should().Be("Kept", "nothing unread is written over");
        }
        finally
        {
            TestAppBuilder.Preferences.GitCommand = "";
        }
    }

    [AvaloniaTest]
    public void The_encodings_dialog_moves_an_encoding_and_only_OK_keeps_it()
    {
        Encoding[] before = [.. AppSettings.AvailableEncodings.Values];
        Window owner = new();
        owner.Show();
        AvailableEncodingsWindow cancelled = new();
        _ = cancelled.ShowDialog<bool>(owner);
        ListBox available = Find<ListBox>(cancelled, "ListAvailableEncodings");
        available.SelectedIndex = 0;
        Find<Button>(cancelled, "ToLeft").IsEffectivelyEnabled.Should().BeTrue();
        Click(cancelled, "ToLeft");
        Click(cancelled, "ButtonCancel");
        AppSettings.AvailableEncodings.Values.Should().Equal(before);

        AvailableEncodingsWindow window = new();
        _ = window.ShowDialog<bool>(owner);
        available = Find<ListBox>(window, "ListAvailableEncodings");
        available.SelectedIndex = 0;
        Encoding added = (Encoding)available.SelectedItem!;
        Click(window, "ToLeft");
        Find<ListBox>(window, "ListIncludedEncodings").SelectedItem = added;
        Find<Button>(window, "ToRight").IsEffectivelyEnabled.Should().Be(AvailableEncodingsDialog.CanRemove(added));
        Click(window, "ButtonOk");

        AppSettings.AvailableEncodings.Should().ContainKey(added.WebName);
        owner.Close();
    }

    private static SettingsWindow OpenSettings(out MainWindow window, bool waitForGitConfig = true)
    {
        window = new MainWindow(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        window.Show();
        UiClicks.Raise(window.FindControl<MenuItem>("SettingsMenuItem")!, MenuItem.ClickEvent);
        MainWindow main = window;
        WaitUntil(() => main.OwnedWindows.OfType<SettingsWindow>().Any());
        SettingsWindow settings = main.OwnedWindows.OfType<SettingsWindow>().Single();
        if (waitForGitConfig)
        {
            WaitUntil(() => settings.IsGitConfigLoaded);
            Dispatcher.UIThread.RunJobs();
        }

        return settings;
    }

    private static void Save(MainWindow window, SettingsWindow settings)
    {
        Click(settings, "SaveButton");
        WaitUntil(() => !window.OwnedWindows.OfType<SettingsWindow>().Any());
        window.Close();
    }

    private static void Click(Window window, string name)
        => UiClicks.Raise(Find<Button>(window, name), Button.ClickEvent);

    private static T Find<T>(Window window, string name) where T : Control
        => window.FindControl<T>(name) ??
           window.GetLogicalDescendants().OfType<T>().FirstOrDefault(control => control.Name == name) ??
           throw new InvalidOperationException($"Control {name} not found");

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

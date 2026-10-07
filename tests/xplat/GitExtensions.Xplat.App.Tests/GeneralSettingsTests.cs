using System.Globalization;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using GitCommands;
using GitExtensions.Xplat.Core.CommitHistory;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

internal sealed class GeneralSettingsTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(20);

    private TestRepository _repo = null!;

    [SetUp]
    public void Setup()
    {
        _repo = new TestRepository();
        Reset();
    }

    [TearDown]
    public void TearDown()
    {
        Reset();
        _repo.Dispose();
    }

    [AvaloniaTest]
    public void The_view_menu_switches_the_grid_between_relative_and_full_dates_of_the_author_or_the_committer()
    {
        _repo.Run("-c", "user.name=Test", "commit", "-q", "--allow-empty", "-m", "third",
            "--date=2001-02-03T04:05:06");
        DateTime authored = UnixDate(_repo.Run("log", "-1", "--format=%at"));
        DateTime committed = UnixDate(_repo.Run("log", "-1", "--format=%ct"));
        MainWindow window = OpenWindow();
        ListBox grid = Find<ListBox>(window, "CommitList");
        WaitUntil(() => grid.ItemCount == 3);
        string relative = FirstRowDate(window);

        Click(window, "ShowRelativeDateMenuItem");
        WaitUntil(() => FirstRowDate(window) == authored.ToString("G"));
        Click(window, "ShowAuthorDateMenuItem");
        WaitUntil(() => FirstRowDate(window) == committed.ToString("G"));

        relative.Should().NotBe(authored.ToString("G"));
        TestAppBuilder.Preferences.RelativeDate.Should().BeFalse();
        TestAppBuilder.Preferences.ShowAuthorDate.Should().BeFalse();
    }

    [AvaloniaTest]
    public void The_app_opens_the_repository_used_last_when_the_setting_asks_for_it()
    {
        MainWindow first = OpenWindow();
        TestAppBuilder.Preferences.RecentWorkingDir.Should().Be(Find<TextBox>(first, "PathBox").Text);
        first.Close();

        TestAppBuilder.Preferences.StartWithRecentWorkingDir = true;
        MainWindow second = NewWindow();
        second.Show();

        WaitUntil(() => Find<ListBox>(second, "CommitList").ItemCount == 2);
        Find<TextBox>(second, "PathBox").Text.Should().Be(TestAppBuilder.Preferences.RecentWorkingDir);
    }

    [AvaloniaTest]
    public void The_commit_button_shows_the_number_of_changes_only_when_the_setting_asks_for_it()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "changed");
        MainWindow window = OpenWindow();
        Button commit = Find<Button>(window, "CommitDialogButton");
        WaitUntil(() => commit.Content as string == "Commit (1)");

        TestAppBuilder.Preferences.ShowGitStatusInBrowseToolbar = false;
        Click(window, "RefreshMenuItem");

        WaitUntil(() => commit.Content as string == "Commit");
    }

    [AvaloniaTest]
    public void The_clone_window_suggests_a_folder_in_the_default_destination_until_one_is_typed()
    {
        string parent = Path.Combine(Path.GetTempPath(), "clones");
        CloneWindow clone = new(parent);
        clone.Show();
        TextBox url = Find<TextBox>(clone, "UrlBox");
        TextBox target = Find<TextBox>(clone, "TargetBox");

        url.Text = "https://example.com/owner/project.git";
        target.Text.Should().Be(Path.Combine(parent, "project"));

        target.Text = "/somewhere/else";
        url.Text = "https://example.com/owner/other.git";
        target.Text.Should().Be("/somewhere/else");
        clone.Close();
    }

    [AvaloniaTest]
    public void The_settings_window_saves_the_general_and_appearance_choices()
    {
        MainWindow window = NewWindow();
        window.Show();
        Click(window, "SettingsMenuItem");
        WaitUntil(() => window.OwnedWindows.OfType<SettingsWindow>().Any());
        SettingsWindow settings = window.OwnedWindows.OfType<SettingsWindow>().Single();
        WaitUntil(() => settings.IsGitConfigLoaded);

        Find<CheckBox>(settings, "RelativeDateCheck").IsChecked.Should().BeTrue();
        Find<CheckBox>(settings, "StartWithRecentWorkingDirCheck").IsChecked = true;
        Find<CheckBox>(settings, "ShowCommitCountCheck").IsChecked = false;
        Find<CheckBox>(settings, "RelativeDateCheck").IsChecked = false;
        Find<TextBox>(settings, "DefaultCloneDestinationBox").Text = " /src ";
        Click(settings, "SaveButton");
        WaitUntil(() => !window.OwnedWindows.OfType<SettingsWindow>().Any());

        TestAppBuilder.Preferences.StartWithRecentWorkingDir.Should().BeTrue();
        TestAppBuilder.Preferences.ShowGitStatusInBrowseToolbar.Should().BeFalse();
        TestAppBuilder.Preferences.RelativeDate.Should().BeFalse();
        TestAppBuilder.Preferences.DefaultCloneDestinationPath.Should().Be("/src");
        WaitUntil(() => Find<MenuItem>(window, "ShowRelativeDateMenuItem").IsChecked == false);
    }

    private static void Reset()
    {
        TestAppBuilder.Preferences.RelativeDate = true;
        TestAppBuilder.Preferences.ShowAuthorDate = true;
        TestAppBuilder.Preferences.StartWithRecentWorkingDir = false;
        TestAppBuilder.Preferences.RecentWorkingDir = null;
        TestAppBuilder.Preferences.DefaultCloneDestinationPath = "";
        TestAppBuilder.Preferences.ShowGitStatusInBrowseToolbar = true;
    }

    private static DateTime UnixDate(string seconds)
        => DateTimeOffset.FromUnixTimeSeconds(long.Parse(seconds.Trim(), CultureInfo.InvariantCulture)).LocalDateTime;

    private static string FirstRowDate(MainWindow window)
        => (Find<ListBox>(window, "CommitList").Items[0] as CommitListItem)?.Row.Date ?? "";

    private static MainWindow NewWindow() =>
        new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));

    private MainWindow OpenWindow()
    {
        MainWindow window = NewWindow();
        window.Show();
        Find<TextBox>(window, "PathBox").Text = _repo.Path;
        Click(window, "OpenButton");
        WaitUntil(() => Find<Button>(window, "OpenButton").IsEnabled &&
                        Find<ListBox>(window, "CommitList").ItemCount > 0);
        return window;
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

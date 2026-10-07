using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using GitCommands;
using GitExtensions.Xplat.Core.CommitHistory;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

internal sealed class BlameViewerSettingsTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(20);

    private TestRepository _repo = null!;

    [SetUp]
    public void Setup()
    {
        _repo = new TestRepository();
        TestAppBuilder.Preferences.BlameOptions = new BlameOptions();
    }

    [TearDown]
    public void TearDown()
    {
        TestAppBuilder.Preferences.BlameOptions = new BlameOptions();
        _repo.Dispose();
    }

    [AvaloniaTest]
    public void The_blame_menu_changes_how_git_blames_and_what_the_rows_show()
    {
        string second = _repo.Run("rev-parse", "HEAD").Trim();
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "two   ");
        _repo.Run("commit", "-q", "-am", "whitespace only");
        string third = _repo.Run("rev-parse", "HEAD").Trim();
        BlameWindow window = new(_repo.Path, third, "a.txt");
        window.Show();
        ListBox list = Find<ListBox>(window, "BlameList");

        // Upstream ignores whitespace by default, so the line still belongs to the commit that wrote "two".
        WaitUntil(() => Rows(list).FirstOrDefault()?.Hash == second[..8]);
        Rows(list)[0].LineNumberText.Should().BeEmpty();
        Rows(list)[0].AuthorLine.Should().EndWith(" - Test");

        Click(window, "IgnoreWhitespaceMenuItem");
        WaitUntil(() => Rows(list).FirstOrDefault()?.Hash == third[..8]);
        TestAppBuilder.Preferences.BlameOptions.IgnoreWhitespace.Should().BeFalse();

        Click(window, "ShowLineNumbersMenuItem");
        Click(window, "DisplayAuthorFirstMenuItem");
        WaitUntil(() => Rows(list)[0].LineNumberText == "1");
        Rows(list)[0].AuthorLine.Should().StartWith("Test - ");

        Click(window, "ShowAuthorDateMenuItem");
        WaitUntil(() => Rows(list)[0].AuthorLine == "Test");
        Find<MenuItem>(window, "ShowAuthorTimeMenuItem").IsEnabled.Should().BeFalse();
        Click(window, "ShowAuthorMenuItem");
        TestAppBuilder.Preferences.BlameOptions.Should().Be(new BlameOptions(IgnoreWhitespace: false,
            DisplayAuthorFirst: true, ShowAuthor: false, ShowAuthorDate: true, ShowLineNumbers: true),
            "turning off the author brings the date back, as upstream's menu does");
        window.Close();
    }

    [AvaloniaTest]
    public void The_blame_viewer_tab_saves_upstreams_settings()
    {
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        window.Show();
        Click(window, "SettingsMenuItem");
        WaitUntil(() => window.OwnedWindows.OfType<SettingsWindow>().Any());
        SettingsWindow settings = window.OwnedWindows.OfType<SettingsWindow>().Single();
        WaitUntil(() => settings.IsGitConfigLoaded);

        Find<CheckBox>(settings, "BlameIgnoreWhitespaceCheck").IsChecked.Should().BeTrue();
        ToolTip.GetTip(Find<CheckBox>(settings, "BlameDetectMoveInFileCheck")).Should()
            .Be(SettingsWindow.BlameMoveWarning);
        Find<CheckBox>(settings, "BlameIgnoreWhitespaceCheck").IsChecked = false;
        Find<CheckBox>(settings, "BlameDetectMoveInAllFilesCheck").IsChecked = true;
        Find<CheckBox>(settings, "BlameShowLineNumbersCheck").IsChecked = true;
        Find<CheckBox>(settings, "BlameShowOriginalFilePathCheck").IsChecked = false;
        Click(settings, "SaveButton");
        WaitUntil(() => !window.OwnedWindows.OfType<SettingsWindow>().Any());

        TestAppBuilder.Preferences.BlameOptions.Should().Be(new BlameOptions(IgnoreWhitespace: false,
            DetectMoveInAllFiles: true, ShowLineNumbers: true, ShowOriginalFilePath: false));
    }

    private static IReadOnlyList<BlameRow> Rows(ListBox list) => list.ItemsSource as IReadOnlyList<BlameRow> ?? [];

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

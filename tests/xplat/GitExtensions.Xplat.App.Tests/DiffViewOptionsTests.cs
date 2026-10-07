using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using GitCommands;
using GitCommands.Settings;
using GitExtensions.Xplat.Core.Diff;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

internal sealed class DiffViewOptionsTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(20);

    private TestRepository _repo = null!;

    [SetUp]
    public void Setup()
    {
        _repo = new TestRepository();
        TestAppBuilder.Preferences.ResetDiffOptions();
        Hotkeys.Load(null);
    }

    [TearDown]
    public void TearDown()
    {
        TestAppBuilder.Preferences.ResetDiffOptions();
        _repo.Dispose();
    }

    [AvaloniaTest]
    public void The_toolbar_changes_whitespace_context_and_entire_file_and_the_diff_follows()
    {
        string[] original = [.. Enumerable.Range(1, 20).Select(i => $"line {i}")];
        File.WriteAllLines(Path.Combine(_repo.Path, "a.txt"), original);
        _repo.Run("commit", "-q", "-am", "twenty lines");
        string[] changed = [.. original];
        changed[9] = "line  10";
        File.WriteAllLines(Path.Combine(_repo.Path, "a.txt"), changed);
        DiffView diff = Show();

        WaitUntil(() => Added(diff) == 1);
        diff.Lines.Count(line => line.Text.StartsWith(' ')).Should().Be(6);

        diff.FindControl<ComboBox>("WhitespaceBox")!.SelectedItem =
            WhitespaceOption.All.Single(option => option.Kind == IgnoreWhitespaceKind.Change);
        WaitUntil(() => Added(diff) == 0);

        diff.FindControl<ComboBox>("WhitespaceBox")!.SelectedIndex = 0;
        Click(diff, "IncreaseContextButton");
        WaitUntil(() => diff.Lines.Count(line => line.Text.StartsWith(' ')) == 8);
        diff.FindControl<TextBlock>("ContextText")!.Text.Should().Be("4");

        diff.FindControl<ToggleButton>("EntireFileToggle")!.IsChecked = true;
        WaitUntil(() => diff.Lines.Count(line => line.Text.StartsWith(' ')) == 19);
        diff.FindControl<Button>("IncreaseContextButton")!.IsEnabled.Should().BeFalse();

        // Kept for the next view in this run, as upstream's runtime settings.
        TestAppBuilder.Preferences.InitialDiffOptions().Should()
            .Be(new DiffOptions(IgnoreWhitespaceKind.None, ContextLines: 3, ShowEntireFile: false));
        diff.Options.Should().Be(new DiffOptions(IgnoreWhitespaceKind.None, ContextLines: 4, ShowEntireFile: true));
    }

    [AvaloniaTest]
    public void Upstreams_hotkeys_change_the_options_while_the_diff_has_the_focus()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "changed");
        DiffView diff = Show();
        ListBox list = diff.FindControl<ListBox>("DiffList")!;

        Press(list, Key.E, KeyModifiers.Control);
        Press(list, Key.W, KeyModifiers.Control | KeyModifiers.Shift);
        diff.Options.Should().Be(new DiffOptions(IgnoreWhitespaceKind.AllSpace, ShowEntireFile: true));

        Press(list, Key.E, KeyModifiers.Control);
        Press(list, Key.OemMinus, KeyModifiers.Control);
        Press(list, Key.OemMinus, KeyModifiers.Control);
        diff.Options.ContextLines.Should().Be(1);
    }

    [AvaloniaTest]
    public void The_diff_viewer_tab_saves_the_remember_choices_and_the_current_view_as_default()
    {
        TestAppBuilder.Preferences.SetDiffOptions(new DiffOptions(IgnoreWhitespaceKind.Eol, ShowEntireFile: true));
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        window.Show();
        Find<MenuItem>(window, "SettingsMenuItem").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        WaitUntil(() => window.OwnedWindows.OfType<SettingsWindow>().Any());
        SettingsWindow settings = window.OwnedWindows.OfType<SettingsWindow>().Single();
        WaitUntil(() => settings.IsGitConfigLoaded);

        Find<CheckBox>(settings, "RememberIgnoreWhitespaceCheck").IsChecked.Should().BeTrue();
        Find<Button>(settings, "SaveDiffDefaultsButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Find<CheckBox>(settings, "RememberIgnoreWhitespaceCheck").IsChecked = false;
        Find<CheckBox>(settings, "RememberContextLinesCheck").IsChecked = true;
        Find<Button>(settings, "SaveButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        WaitUntil(() => !window.OwnedWindows.OfType<SettingsWindow>().Any());

        TestAppBuilder.Preferences.RememberIgnoreWhiteSpacePreference.Should().BeFalse();
        TestAppBuilder.Preferences.RememberNumberOfContextLines.Should().BeTrue();

        // Not remembered, so a new view starts from the saved default rather than this run's choice.
        TestAppBuilder.Preferences.SetDiffOptions(new DiffOptions(IgnoreWhitespaceKind.AllSpace));
        TestAppBuilder.Preferences.InitialDiffOptions().IgnoreWhitespace.Should().Be(IgnoreWhitespaceKind.Eol);
    }

    private DiffView Show()
    {
        DiffView diff = new();
        Window window = new() { Content = diff, Width = 900, Height = 600 };
        window.Show();
        DiffView shown = diff;
        Dispatcher.UIThread.RunJobs();
        Task load = shown.ShowAsync(_repo.Path, commitHash: null, "a.txt", staged: false, "a.txt");
        WaitUntil(() => load.IsCompleted);
        return shown;
    }

    // Added lines, without the "+++ b/file" header.
    private static int Added(DiffView diff)
        => diff.Lines.Count(line =>
            line.Text.StartsWith('+') && !line.Text.StartsWith("+++", StringComparison.Ordinal));

    private static void Press(Control control, Key key, KeyModifiers modifiers)
    {
        control.RaiseEvent(new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent, Key = key, KeyModifiers = modifiers
        });
        Dispatcher.UIThread.RunJobs();
    }

    private static void Click(Control parent, string name)
        => parent.FindControl<Button>(name)!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

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

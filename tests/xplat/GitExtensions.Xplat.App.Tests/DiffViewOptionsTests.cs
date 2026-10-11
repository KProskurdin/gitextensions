using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
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
        TestAppBuilder.Preferences.DiffVerticalRulerPosition = 0;
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

    [AvaloniaTest]
    public void A_code_files_diff_has_upstreams_syntax_colors_and_a_text_file_has_none()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "Program.cs"), "int x = 1;\n");
        _repo.Run("add", "Program.cs");
        _repo.Run("commit", "-q", "-m", "code");
        File.WriteAllText(Path.Combine(_repo.Path, "Program.cs"), "int x = 1;\n// changed\n");
        DiffView diff = new();
        Window window = new() { Content = diff, Width = 900, Height = 600 };
        window.Show();
        Task load = diff.ShowAsync(_repo.Path, commitHash: null, "Program.cs", staged: false, "Program.cs");
        WaitUntil(() => load.IsCompleted);

        DiffLineItem added = diff.Lines.Single(line => line.Text == "+// changed");
        string.Concat(added.Runs!.Select(run => run.Text)).Should().Be("+// changed");
        added.Runs![0].Foreground.Should().BeNull("the + keeps the line's color");
        added.Runs!.Skip(1).Select(run => run.Foreground).Should()
            .OnlyContain(brush => brush == added.Runs![1].Foreground)
            .And.NotContainNulls("the comment has the definition's comment color");
        diff.Lines.Single(line => line.Text.StartsWith("@@", StringComparison.Ordinal)).Runs.Should().BeNull();
        Dispatcher.UIThread.RunJobs();
        diff.GetVisualDescendants().OfType<SyntaxTextBlock>().Should().Contain(block => block.Inlines!.Count > 0);

        diff.ShowText("notes.txt", "plain text");
        diff.Lines.Single().Runs.Should().BeNull();
        diff.ShowText("Program.cs", "int x;");
        diff.Lines.Single().Runs!.Select(run => run.Text).Should().Equal("int", " ", "x", ";");
    }

    [AvaloniaTest]
    public void Nonprinting_characters_syntax_highlighting_and_the_ruler_follow_the_view_and_its_settings()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "Program.cs"), "int x = 1;\n");
        _repo.Run("add", "Program.cs");
        _repo.Run("commit", "-q", "-m", "code");
        File.WriteAllText(Path.Combine(_repo.Path, "Program.cs"), "int x = 1;\n\tint y;\n");
        TestAppBuilder.Preferences.DiffVerticalRulerPosition = 80;
        DiffView diff = new();
        Window window = new() { Content = diff, Width = 900, Height = 600 };
        window.Show();
        Task load = diff.ShowAsync(_repo.Path, commitHash: null, "Program.cs", staged: false, "Program.cs");
        WaitUntil(() => load.IsCompleted);
        diff.Lines.Single(line => line.Text.StartsWith("+\t", StringComparison.Ordinal)).Runs.Should().NotBeNull();
        Dispatcher.UIThread.RunJobs();
        Avalonia.Controls.Shapes.Line ruler = diff.FindControl<Avalonia.Controls.Shapes.Line>("VerticalRuler")!;
        ruler.IsVisible.Should().BeTrue();
        ruler.StartPoint.X.Should().BeGreaterThan(88 + (79 * 4), "column 80 is past the line numbers");

        // As upstream: the markers for spaces, tabs and line ends, not on the diff's own + or space; no new git diff.
        diff.FindControl<ToggleButton>("NonPrintingToggle")!.IsChecked = true;
        DiffLineItem added = diff.Lines.Single(line => line.Text.StartsWith("+»", StringComparison.Ordinal));
        string.Concat(added.Runs!.Select(run => run.Text)).Should().Be("+»\tint·y;¶");
        diff.Lines.Single(line => line.Text.StartsWith(' ')).Text.Should().Be(" int·x·=·1;¶");
        TestAppBuilder.Preferences.InitialDiffOptions().ShowNonPrintingChars.Should().BeFalse(
            "not remembered by default, so a new view starts without them");

        diff.FindControl<ToggleButton>("SyntaxHighlightingToggle")!.IsChecked = false;
        diff.Lines.Should().OnlyContain(line => line.Runs == null);
        TestAppBuilder.Preferences.InitialDiffOptions().ShowSyntaxHighlighting.Should().BeFalse(
            "remembered by default, as upstream");

        diff.ShowText("Program.cs", "int x;");
        diff.Lines.Single().Runs.Should().NotBeNull("a file's text is always highlighted");
        window.Close();
    }

    [AvaloniaTest]
    public void The_diff_viewer_tab_saves_the_new_settings_and_the_diff_tab_lists_every_diff_tool()
    {
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        window.Show();
        Find<MenuItem>(window, "SettingsMenuItem").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        WaitUntil(() => window.OwnedWindows.OfType<SettingsWindow>().Any());
        SettingsWindow settings = window.OwnedWindows.OfType<SettingsWindow>().Single();
        WaitUntil(() => settings.IsGitConfigLoaded);
        Find<CheckBox>(settings, "ShowAllDiffToolsCheck").IsChecked.Should().BeTrue();
        Find<CheckBox>(settings, "RememberSyntaxHighlightingCheck").IsChecked.Should().BeTrue();
        Find<CheckBox>(settings, "RememberNonPrintingCheck").IsChecked = true;
        Find<NumericUpDown>(settings, "VerticalRulerBox").Value = 72;
        Find<Button>(settings, "SaveButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        WaitUntil(() => !window.OwnedWindows.OfType<SettingsWindow>().Any());
        TestAppBuilder.Preferences.RememberShowNonPrintingCharsPreference.Should().BeTrue();
        TestAppBuilder.Preferences.DiffVerticalRulerPosition.Should().Be(72);

        Button choices = Find<Button>(window, "CommitDiffToolChoiceButton");
        choices.IsVisible.Should().BeTrue();
        MenuFlyout flyout = (MenuFlyout)choices.Flyout!;
        flyout.ShowAt(choices);
        WaitUntil(() => flyout.Items.Count == 2);
        flyout.Items.OfType<MenuItem>().Select(item => ((TextBlock)item.Header!).Text).Should().Equal("meld", "vscode");
        flyout.Hide();
        window.Close();
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

using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using GitCommands;
using GitCommands.Settings;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Diff;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;
using GitExtUtils.GitUI.Theming;
using GitUI.Theming;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

/// <summary>
///  Upstream's git coloring and "Git word diff" in the viewer, with real git output.
/// </summary>
internal sealed class GitColoringViewTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private TestRepository _repo = null!;
    private Window? _window;

    [SetUp]
    public void SetUp()
    {
        _repo = new TestRepository();
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "keep\nold word here\n");
        _repo.Run("commit", "-q", "-am", "base");
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "keep\nnew word here\n");
    }

    [TearDown]
    public void TearDown()
    {
        TestAppBuilder.Preferences.UseGitColoring = false;
        TestAppBuilder.Preferences.DiffDisplayAppearance = DiffDisplayAppearance.Patch;
        _window?.Close();
        _repo.Dispose();
    }

    [AvaloniaTest]
    public void With_git_coloring_a_diff_has_gits_colors_and_no_escape_codes()
    {
        TestAppBuilder.Preferences.UseGitColoring = true;

        DiffView diff = Show();

        DiffLineItem added = diff.Lines.Single(line => line.Text == "+new word here");
        diff.Lines.Should().NotContain(line => line.Text.Contains('\u001b'));
        added.Background.Should().BeNull("git's colors take the place of the line's");
        added.Runs.Should().NotBeNull();
        string.Concat(added.Runs!.Select(run => run.Text)).Should().Be("+new word here");
        added.Runs!.Should().Contain(run => run.Background != null || run.Foreground != null);
        added.NewNumber.Should().Be("2");
        diff.Lines.Single(line => line.Text == " keep").Runs.Should().BeNull("git colors nothing there, and a.txt has no syntax");
    }

    [AvaloniaTest]
    public void What_a_changed_line_shares_with_the_one_it_replaces_is_dimmed_with_and_without_git_coloring()
    {
        foreach (bool gitColoring in new[] { false, true })
        {
            TestAppBuilder.Preferences.UseGitColoring = gitColoring;
            _window?.Close();
            DiffView diff = Show();

            foreach (string text in new[] { "+new word here", "-old word here" })
            {
                DiffLineItem line = diff.Lines.Single(item => item.Text == text);
                string.Concat(line.Runs!.Select(run => run.Text)).Should().Be(text);
                DiffTextRun shared = line.Runs!.Single(run => run.Text == " word here");
                DiffTextRun changed = line.Runs!.First(run => run.Text.Contains(text[1..4], StringComparison.Ordinal));
                shared.Background.Should().NotBeNull();
                shared.Background.Should().NotBe(changed.Background, "the changed word stands out");
            }

            diff.Lines.Single(item => item.Text == " keep").Runs.Should().BeNull("a context line has nothing to compare");
        }
    }

    [AvaloniaTest]
    public void The_dimmed_parts_keep_their_place_with_nonprinting_marks()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "keep\nold\tword here\n");
        _repo.Run("commit", "-q", "-am", "tab");
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "keep\nnew\tword here\n");
        DiffView diff = Show();

        diff.FindControl<Avalonia.Controls.Primitives.ToggleButton>("NonPrintingToggle")!.IsChecked = true;

        DiffLineItem line = diff.Lines.Single(item => item.Text.StartsWith("+new", StringComparison.Ordinal));
        string.Concat(line.Runs!.Select(run => run.Text)).Should().Be(line.Text);
        line.Runs!.Should().Contain(run => run.Text.Contains("word·here", StringComparison.Ordinal) && run.Background != null);
        TestAppBuilder.Preferences.InitialDiffOptions();
        diff.FindControl<Avalonia.Controls.Primitives.ToggleButton>("NonPrintingToggle")!.IsChecked = false;
    }

    [AvaloniaTest]
    public void A_dark_theme_reaches_upstreams_color_helpers()
    {
        System.Drawing.Color white = System.Drawing.Color.White;
        ThemeId theme = TestAppBuilder.Preferences.Theme;
        try
        {
            TestAppBuilder.Preferences.Theme = ThemeId.DefaultLight;
            ApplyTheme();
            byte light = white.DimColor().R;

            TestAppBuilder.Preferences.Theme = ThemeId.DefaultDark;
            ApplyTheme();

            white.DimColor().R.Should().BeLessThan(light, "DimColor blends with the theme's editor background");
        }
        finally
        {
            TestAppBuilder.Preferences.Theme = theme;
            ApplyTheme();
        }
    }

    [AvaloniaTest]
    public void Without_git_coloring_the_line_keeps_its_kinds_color()
    {
        DiffView diff = Show();

        DiffLineItem added = diff.Lines.Single(line => line.Text == "+new word here");

        added.Background.Should().NotBeNull();
    }

    [AvaloniaTest]
    public void The_lines_of_a_colored_diff_still_stage_and_copy_as_a_patch()
    {
        TestAppBuilder.Preferences.UseGitColoring = true;
        DiffView diff = Show();
        ListBox lines = diff.FindControl<ListBox>("DiffList")!;
        lines.SelectedIndex = diff.Lines.ToList().FindIndex(line => line.Text == "+new word here");

        (string text, _, _) = diff.SelectedRange()!.Value;

        text.Should().Contain("\n+new word here\n").And.NotContain("\u001b");
    }

    [AvaloniaTest]
    public void Git_word_diff_shows_the_changed_words_and_offers_no_line_patching()
    {
        DiffView diff = Show();
        ListBox lines = diff.FindControl<ListBox>("DiffList")!;

        ToggleWordDiff(diff);

        WaitUntil(() => diff.Lines.Any(line => line.Text.Contains("oldnew", StringComparison.Ordinal)));
        DiffLineItem changed = diff.Lines.Single(line => line.Text.Contains("oldnew", StringComparison.Ordinal));
        changed.Text.Should().Be("oldnew word here", "a word diff has no + or - column");
        changed.Runs!.Count(run => run.Background != null || run.Foreground != null).Should().BeGreaterThanOrEqualTo(2);
        (changed.OldNumber, changed.NewNumber).Should().Be(("2", "2"));
        TestAppBuilder.Preferences.DiffDisplayAppearance.Should().Be(DiffDisplayAppearance.GitWordDiff);

        lines.SelectedIndex = diff.Lines.ToList().IndexOf(changed);
        diff.SelectedRange().Should().BeNull("upstream's SupportLinePatching");
        lines.RaiseEvent(new ContextRequestedEventArgs());
        Dispatcher.UIThread.RunJobs();
        lines.ContextMenu!.Close();
        Item(diff, "CopyPatchMenuItem").IsVisible.Should().BeFalse();
        Item(diff, "ShowGitWordColoringMenuItem").IsChecked.Should().BeTrue();
        Item(diff, "ShowPatchMenuItem").IsChecked.Should().BeFalse();

        UiClicks.Raise(Item(diff, "ShowPatchMenuItem"), MenuItem.ClickEvent);

        WaitUntil(() => diff.Lines.Any(line => line.Text == "+new word here"));
        TestAppBuilder.Preferences.DiffDisplayAppearance.Should().Be(DiffDisplayAppearance.Patch);
    }

    [AvaloniaTest]
    public void The_hotkey_toggles_the_word_diff_and_a_new_viewer_starts_from_the_stored_appearance()
    {
        DiffView diff = Show();
        diff.RunHotkey(new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent, Key = Key.D, KeyModifiers = Hotkeys.CommandModifier,
        }).Should().BeTrue();
        WaitUntil(() => diff.Lines.Any(line => line.Text.Contains("oldnew", StringComparison.Ordinal)));

        _ = new DiffView();

        TestAppBuilder.Preferences.DiffDisplayAppearance.Should().Be(DiffDisplayAppearance.Patch, "not remembered by default");

        TestAppBuilder.Preferences.DiffDisplayAppearance = DiffDisplayAppearance.GitWordDiff;
        TestAppBuilder.Preferences.RememberDiffDisplayAppearance = true;
        try
        {
            _ = new DiffView();

            TestAppBuilder.Preferences.DiffDisplayAppearance.Should().Be(DiffDisplayAppearance.GitWordDiff);
        }
        finally
        {
            TestAppBuilder.Preferences.RememberDiffDisplayAppearance = false;
        }
    }

    [AvaloniaTest]
    public void A_diff_between_two_revisions_has_gits_colors_too()
    {
        TestAppBuilder.Preferences.UseGitColoring = true;
        _repo.Run("commit", "-q", "-am", "change");
        GitModule module = GitModules.Open(_repo.Path);
        ObjectId second = module.RevParse("HEAD");
        ObjectId first = module.RevParse("HEAD~1");
        RevisionDiffFile file = new("M", "a.txt", OldPath: null, IsTracked: true);
        DiffView diff = new();
        _window = new Window { Content = diff, Width = 900, Height = 600 };
        _window.Show();

        Task show = diff.ShowDiffAsync(_repo.Path, "a.txt", "a.txt",
            options => RevisionDiff.DiffAsync(module, first, second, file, options));
        WaitUntil(() => show.IsCompleted);
        Dispatcher.UIThread.RunJobs();

        diff.Lines.Should().NotContain(line => line.Text.Contains('\u001b'));
        DiffLineItem added = diff.Lines.Single(line => line.Text == "+new word here");
        added.Runs.Should().NotBeNull();
        added.Background.Should().BeNull();
    }

    [AvaloniaTest]
    public void Difftastic_needs_gits_tool_and_then_shows_its_columns()
    {
        DiffView disabled = Show();
        WaitUntil(() => !disabled.IsLoading);
        Dispatcher.UIThread.RunJobs();
        Item(disabled, "ShowDifftasticMenuItem").IsEnabled.Should().BeFalse();
        disabled.RunHotkey(new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent, Key = Key.T, KeyModifiers = Hotkeys.CommandModifier,
        }).Should().BeFalse();
        _window?.Close();

        // A second repository, as whether git knows the tool is read once per repository.
        using TestRepository repo = new();
        File.WriteAllText(Path.Combine(repo.Path, "a.txt"), "changed\n");
        repo.Run("config", "difftool.difftastic.cmd", "printf 'a.txt --- Text\\n1 same\\n2 more\\n'");
        DiffView diff = new();
        _window = new Window { Content = diff, Width = 900, Height = 600 };
        _window.Show();
        Task load = diff.ShowAsync(repo.Path, commitHash: null, "a.txt", staged: false, "a.txt");
        WaitUntil(() => load.IsCompleted && Item(diff, "ShowDifftasticMenuItem").IsEnabled);

        UiClicks.Raise(Item(diff, "ShowDifftasticMenuItem"), MenuItem.ClickEvent);

        WaitUntil(() => diff.Lines.Any(line => line.Text == "same"));
        diff.Lines.Select(line => line.Text).Should().Equal("a.txt --- Text", "same", "more");
        diff.Lines[2].OldNumber.Should().Be("2");
        TestAppBuilder.Preferences.DiffDisplayAppearance.Should().Be(DiffDisplayAppearance.Difftastic);
        diff.SelectedRange().Should().BeNull();
    }

    // The theme applier is internal to the app.
    private static void ApplyTheme()
        => typeof(DiffView).Assembly.GetType("GitExtensions.Xplat.App.ThemeApplier")!
            .GetMethod("Apply")!.Invoke(null, [TestAppBuilder.Preferences]);

    private DiffView Show()
    {
        DiffView diff = new();
        _window = new Window { Content = diff, Width = 900, Height = 600 };
        _window.Show();
        Dispatcher.UIThread.RunJobs();
        Task load = diff.ShowAsync(_repo.Path, commitHash: null, "a.txt", staged: false, "a.txt");
        WaitUntil(() => load.IsCompleted && diff.Lines.Count > 0);
        return diff;
    }

    private static void ToggleWordDiff(DiffView diff)
        => UiClicks.Raise(Item(diff, "ShowGitWordColoringMenuItem"), MenuItem.ClickEvent);

    private static MenuItem Item(DiffView diff, string name)
        => diff.FindControl<MenuItem>(name)
           ?? diff.GetLogicalDescendants().OfType<MenuItem>().First(item => item.Name == name);

    private static void WaitUntil(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow + _timeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail("Timed out waiting for the viewer");
            }

            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(10);
        }
    }
}

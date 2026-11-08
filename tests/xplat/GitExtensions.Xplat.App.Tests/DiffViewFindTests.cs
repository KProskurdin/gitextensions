using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using GitCommands;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Diff;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

/// <summary>
///  Upstream FileViewer's find (FindAndReplaceForm) and go to line (FormGoToLine) in the viewer.
/// </summary>
internal sealed class DiffViewFindTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(20);

    [SetUp]
    public void SetUp() => Hotkeys.Load(null);

    [AvaloniaTest]
    public void Find_next_selects_each_occurrences_line_marks_it_and_wraps_around_as_upstream()
    {
        (Window window, DiffView diff) = Show("alpha\nbeta foo\ngamma\nfoo delta");
        ListBox lines = diff.FindControl<ListBox>("DiffList")!;

        Press(lines, Key.F, Hotkeys.CommandModifier);

        FindWindow find = diff.FindWindow!;
        find.IsVisible.Should().BeTrue();
        find.Title.Should().Be("Find - notes.txt");
        find.LookFor = "foo";
        Click(find, "btnFindNext");
        lines.SelectedIndex.Should().Be(1);
        diff.Lines[1].Runs!.Single(run => run.Background is not null).Text.Should()
            .Be("foo", "the occurrence is marked");

        Press(lines, Key.F3, KeyModifiers.None);
        lines.SelectedIndex.Should().Be(3);
        diff.Lines[1].Runs.Should().BeNull("only the occurrence found last is marked");
        Press(lines, Key.F3, KeyModifiers.None);
        lines.SelectedIndex.Should().Be(1, "the search starts again at the top");
        Press(lines, Key.F3, KeyModifiers.Shift);
        lines.SelectedIndex.Should().Be(3, "Shift+F3 searches backward, around the top");

        find.LookFor = "zzz";
        Click(find, "btnFindNext");
        WaitUntil(() => find.OwnedWindows.OfType<MessageBoxWindow>().Any());
        find.OwnedWindows.OfType<MessageBoxWindow>().Single().Close();
        lines.SelectedIndex.Should().Be(3);
        find.Close();
        find.IsVisible.Should().BeFalse("closing hides the window for the next search, as upstream's");
        window.Close();
    }

    [AvaloniaTest]
    public void Whole_word_case_and_the_selected_lines_limit_the_search_and_highlight_all_marks_every_occurrence()
    {
        (Window window, DiffView diff) = Show("Foo foo_bar\nfoo\nFoo");
        ListBox lines = diff.FindControl<ListBox>("DiffList")!;
        diff.Find();
        FindWindow find = diff.FindWindow!;
        find.LookFor = "foo";
        find.FindControl<CheckBox>("chkMatchWholeWord")!.IsChecked = true;
        find.FindControl<CheckBox>("chkMatchCase")!.IsChecked = true;

        Click(find, "btnHighlightAll");

        diff.Lines.Select(line => line.Runs?.Count(run => run.Background is not null) ?? 0).Should()
            .Equal([0, 1, 0], "foo_bar is one word and Foo has another case");
        find.IsVisible.Should().BeFalse("upstream closes the window when it marked some");

        find.FindControl<CheckBox>("chkMatchCase")!.IsChecked = false;
        lines.SelectedItems!.Add(diff.Lines[1]);
        lines.SelectedItems.Add(diff.Lines[2]);
        diff.Find();
        find.Title.Should().EndWith("(selection only)");
        Click(find, "btnFindNext");
        lines.SelectedIndex.Should().Be(1);
        Click(find, "btnFindNext");
        lines.SelectedIndex.Should().Be(2);
        Click(find, "btnFindNext");
        lines.SelectedIndex.Should().Be(1, "the search keeps to lines 2 and 3, from the top of them again");
        find.Close();
        window.Close();
    }

    [AvaloniaTest]
    public void Go_to_line_selects_the_files_line_or_the_new_line_number_of_a_diff()
    {
        (Window window, DiffView diff) = Show("one\ntwo\nthree\nfour\nfive");
        ListBox lines = diff.FindControl<ListBox>("DiffList")!;
        diff.MaxLineNumber.Should().Be(5);

        Press(lines, Key.G, Hotkeys.CommandModifier);
        WaitUntil(() => window.OwnedWindows.OfType<GoToLineWindow>().Any());
        GoToLineWindow goTo = window.OwnedWindows.OfType<GoToLineWindow>().Single();
        goTo.FindControl<Label>("lineLabel")!.Content.Should().Be("Line number (1 - 5):");
        goTo.FindControl<NumericUpDown>("_NO_TRANSLATE_LineNumberUpDown")!.Value = 3;
        Click(goTo, "okBtn");
        WaitUntil(() => lines.SelectedIndex == 2);

        diff.ShowPatch("a.txt", "@@ -1,2 +10,3 @@\n a\n-b\n+c\n+d\n");
        diff.MaxLineNumber.Should().Be(12, "the new file's numbers go up to 12");
        diff.GoToLine(11);
        diff.Lines[lines.SelectedIndex].NewNumber.Should().Be("11");
        window.Close();
    }

    [AvaloniaTest]
    public void In_the_diff_tab_a_search_that_wraps_around_goes_on_in_the_lists_next_file_as_upstreams_file_loader()
    {
        using TestRepository repo = new();
        File.WriteAllText(Path.Combine(repo.Path, "first.txt"), "needle one\n");
        File.WriteAllText(Path.Combine(repo.Path, "second.txt"), "hay\nneedle two\n");
        repo.Run("add", "-A");
        repo.Run("commit", "-q", "-m", "needles");
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        window.Show();
        window.OpenRepository(repo.Path);
        WaitUntil(() => !window.IsBusy);
        window.FindControl<TabControl>("DetailTabs")!.SelectedItem = window.FindControl<TabItem>("DiffTab");
        ListBox commits = window.FindControl<ListBox>("CommitList")!;
        WaitUntil(() => commits.Items.Cast<CommitListItem>().Any(item => item.Row.Subject == "needles"));
        commits.SelectedItem = commits.Items.Cast<CommitListItem>().First(item => item.Row.Subject == "needles");
        ListBox files = window.FindControl<ListBox>("CommitFilesList")!;
        WaitUntil(() => files.Items.OfType<CommitFile>().Any(file => file.Path == "second.txt"));
        files.SelectedItem = files.Items.OfType<CommitFile>().Single(file => file.Path == "first.txt");
        DiffView diff = window.FindControl<DiffView>("CommitDiff")!;
        WaitUntil(() => !diff.IsLoading && diff.Lines.Any(line => line.Text.Contains("needle one", StringComparison.Ordinal)));
        diff.Find();
        FindWindow find = diff.FindWindow!;
        find.LookFor = "needle";

        Click(find, "btnFindNext");
        diff.Lines[diff.FindControl<ListBox>("DiffList")!.SelectedIndex].Text.Should().Contain("needle one");
        Click(find, "btnFindNext");

        WaitUntil(() => files.SelectedItem is CommitFile { Path: "second.txt" } && !diff.IsLoading
                        && diff.FindControl<ListBox>("DiffList")!.SelectedIndex is >= 0 and var line
                        && diff.Lines[line].Text.Contains("needle two", StringComparison.Ordinal));
        find.Close();
        window.Close();
    }

    [AvaloniaTest]
    public async Task The_next_and_previous_buttons_move_between_git_greps_matches()
    {
        DiffView diff = new();
        Window window = new() { Content = diff, Width = 900, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        GrepLine[] grep =
        [
            new(1, GrepLineKind.Context, "before", []),
            new(2, GrepLineKind.Match, "first", []),
            new(3, GrepLineKind.Match, "second", []),
            new(null, GrepLineKind.Separator, "--", []),
            new(9, GrepLineKind.Match, "third", []),
        ];
        await diff.ShowGrepAsync("notes.txt", _ => Task.FromResult<(IReadOnlyList<GrepLine>, string?)>((grep, null)));
        Dispatcher.UIThread.RunJobs();
        ListBox lines = diff.FindControl<ListBox>("DiffList")!;
        Button next = diff.FindControl<ToolButton>("NextChangeButton")!;
        Button previous = diff.FindControl<ToolButton>("PreviousChangeButton")!;

        next.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        lines.SelectedIndex.Should().Be(1);
        next.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        lines.SelectedIndex.Should().Be(4, "lines that match one after the other are one stop, as upstream's");
        previous.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        lines.SelectedIndex.Should().Be(1);
        window.Close();
    }

    private static (Window Window, DiffView Diff) Show(string text)
    {
        DiffView diff = new();
        Window window = new() { Content = diff, Width = 900, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        diff.ShowText("notes.txt", text);
        Dispatcher.UIThread.RunJobs();
        return (window, diff);
    }

    private static void Press(Control control, Key key, KeyModifiers modifiers)
    {
        control.RaiseEvent(new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent, Key = key, KeyModifiers = modifiers
        });
        Dispatcher.UIThread.RunJobs();
    }

    private static void Click(Control parent, string name)
    {
        parent.FindControl<Button>(name)!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
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

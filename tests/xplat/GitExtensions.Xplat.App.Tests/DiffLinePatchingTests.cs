using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using GitCommands;
using GitExtensions.Xplat.Core.CommitHistory;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

/// <summary>
///  Upstream FileViewer's line patching beyond staging (<see cref="DiffLinePatching"/>): reset of selected lines in the
///  commit window, and a commit's lines applied, undone or cherry picked whole from the Diff tab.
/// </summary>
internal sealed class DiffLinePatchingTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private TestRepository _repo = null!;
    private string _file = null!;

    [SetUp]
    public void SetUp()
    {
        _repo = new TestRepository();
        _file = Path.Combine(_repo.Path, "lines.txt");
        File.WriteAllLines(_file, Original());
        _repo.Run("add", "lines.txt");
        _repo.Run("commit", "-q", "-m", "lines");
    }

    [TearDown]
    public void TearDown() => _repo.Dispose();

    [AvaloniaTest]
    public void Reset_selected_lines_in_the_commit_window_undoes_only_the_chosen_edit_after_upstreams_question()
    {
        File.WriteAllLines(_file, Edited());
        MainWindow window = OpenWindow();
        WaitUntil(() => Find<Button>(window, "CommitDialogButton").IsEnabled);
        Click(window, "CommitDialogButton");
        WaitUntil(() => window.OwnedWindows.OfType<CommitWindow>().Any());
        CommitWindow commit = window.OwnedWindows.OfType<CommitWindow>().Single();
        ListBox unstaged = Find<ListBox>(commit, "UnstagedList");
        WaitUntil(() => unstaged.ItemCount == 1);
        unstaged.SelectedIndex = 0;
        DiffView diff = Find<DiffView>(commit, "Diff");
        WaitUntil(() => diff.Lines.Any(line => line.Text == "+line 2 changed"));
        SelectDiffLines(diff, "-line 2", "+line 2 changed");

        ClickDiffMenuItem(diff, "ResetLinesMenuItem");

        WaitUntil(() => commit.OwnedWindows.OfType<ConfirmWindow>().Any());
        ConfirmWindow question = commit.OwnedWindows.OfType<ConfirmWindow>().Single();
        Find<TextBlock>(question, "MessageText").Text.Should()
            .Be("Are you sure you want to reset the changes to the selected lines?");
        Click(question, "ConfirmButton");
        WaitUntil(() => ReadLines().ElementAtOrDefault(1) == "line 2");
        ReadLines()[18].Should().Be("line 19 changed", "only the selected lines are reset");
        commit.Close();
        window.Close();
    }

    [AvaloniaTest]
    public void Stage_selected_lines_of_a_commits_diff_applies_them_to_the_working_directory_and_the_index()
    {
        CommitEditThenRevert();
        MainWindow window = OpenWindow();
        DiffView diff = ShowCommitFile(window, "edit");
        SelectDiffLines(diff, "-line 2", "+line 2 changed");

        ClickDiffMenuItem(diff, "StageLinesMenuItem");

        WaitUntil(() => ReadLines().ElementAtOrDefault(1) == "line 2 changed");
        ReadLines()[18].Should().Be("line 19", "only the selected lines are applied");
        _repo.Run("diff", "--cached", "lines.txt").Should().Contain("+line 2 changed", "upstream applies with --index");
        window.Close();
    }

    [AvaloniaTest]
    public void Reset_selected_lines_of_a_commits_diff_undoes_them_in_the_working_directory()
    {
        File.WriteAllLines(_file, Edited());
        _repo.Run("commit", "-q", "-am", "edit");
        MainWindow window = OpenWindow();
        DiffView diff = ShowCommitFile(window, "edit");
        SelectDiffLines(diff, "-line 19", "+line 19 changed");

        ClickDiffMenuItem(diff, "ResetLinesMenuItem");

        WaitUntil(() => ReadLines().ElementAtOrDefault(18) == "line 19");
        ReadLines()[1].Should().Be("line 2 changed");
        window.Close();
    }

    [AvaloniaTest]
    public void Cherry_pick_changes_in_the_file_menu_applies_the_files_whole_change()
    {
        CommitEditThenRevert();
        MainWindow window = OpenWindow();
        ShowCommitFile(window, "edit");
        ListBox files = Find<ListBox>(window, "CommitFilesList");
        files.RaiseEvent(new ContextRequestedEventArgs());
        Dispatcher.UIThread.RunJobs();
        files.ContextMenu!.Close();
        MenuItem cherryPick = Find<MenuItem>(files.ContextMenu, "tsmiCherryPickChanges");
        cherryPick.IsVisible.Should().BeTrue("the viewer can apply the commit's lines");

        UiClicks.Raise(cherryPick, MenuItem.ClickEvent);

        WaitUntil(() => ReadLines().SequenceEqual(Edited()));
        window.Close();
    }

    private static string[] Original() => [.. Enumerable.Range(1, 20).Select(i => $"line {i}")];

    private static string[] Edited() =>
        [.. Enumerable.Range(1, 20).Select(i => i is 2 or 19 ? $"line {i} changed" : $"line {i}")];

    // "edit" changes lines 2 and 19, "revert" puts them back, so the working directory has neither.
    private void CommitEditThenRevert()
    {
        File.WriteAllLines(_file, Edited());
        _repo.Run("commit", "-q", "-am", "edit");
        File.WriteAllLines(_file, Original());
        _repo.Run("commit", "-q", "-am", "revert");
    }

    // While git rewrites the file it can be read empty or cut short, so waits read lines with ElementAtOrDefault.
    private string[] ReadLines() =>
        _repo.ReadFile("lines.txt")?.ReplaceLineEndings("\n").TrimEnd('\n').Split('\n') ?? [];

    private MainWindow OpenWindow()
    {
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        window.Show();
        window.OpenRepository(_repo.Path);
        WaitUntil(() => !window.IsBusy);
        return window;
    }

    // The commit's lines.txt in the Diff tab's viewer.
    private static DiffView ShowCommitFile(MainWindow window, string subject)
    {
        Find<TabControl>(window, "DetailTabs").SelectedItem = Find<TabItem>(window, "DiffTab");
        ListBox commits = Find<ListBox>(window, "CommitList");
        WaitUntil(() => commits.Items.Cast<CommitListItem>().Any(item => item.Row.Subject == subject));
        commits.SelectedItem = commits.Items.Cast<CommitListItem>().First(item => item.Row.Subject == subject);
        ListBox files = Find<ListBox>(window, "CommitFilesList");
        WaitUntil(() => files.Items.OfType<CommitFile>().Any(file => file.Path == "lines.txt"));
        files.SelectedItem = files.Items.OfType<CommitFile>().First(file => file.Path == "lines.txt");
        DiffView diff = Find<DiffView>(window, "CommitDiff");
        WaitUntil(() => diff.Lines.Any(line => line.Text == "+line 2 changed"));
        return diff;
    }

    private static void SelectDiffLines(DiffView diff, params string[] texts)
    {
        ListBox diffList = diff.FindControl<ListBox>("DiffList")!;
        List<DiffLineItem> lines = [.. diff.Lines];
        foreach (string text in texts)
        {
            diffList.Selection.Select(lines.FindIndex(line => line.Text == text));
        }
    }

    private static void ClickDiffMenuItem(DiffView diff, string name)
    {
        ListBox list = diff.FindControl<ListBox>("DiffList")!;
        list.RaiseEvent(new ContextRequestedEventArgs());
        Dispatcher.UIThread.RunJobs();
        list.ContextMenu!.Close();
        MenuItem item = diff.FindControl<MenuItem>(name)!;
        (item.IsVisible, item.IsEnabled).Should().Be((true, true));
        UiClicks.Raise(item, MenuItem.ClickEvent);
    }

    private static void Click(Window window, string name)
    {
        Control control = Find<Control>(window, name);
        UiClicks.Raise(control, control is MenuItem ? MenuItem.ClickEvent : Button.ClickEvent);
    }

    private static T Find<T>(Control root, string name)
        where T : Control
        => root.FindControl<T>(name)
           ?? root.GetLogicalDescendants().OfType<T>().FirstOrDefault(control => control.Name == name)
           ?? throw new InvalidOperationException($"No control named {name}");

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

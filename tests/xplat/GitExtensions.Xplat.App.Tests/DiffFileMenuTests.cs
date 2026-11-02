using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using GitCommands;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Diff;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

/// <summary>
///  Upstream FileStatusList's item menu (<see cref="FileStatusListMenu"/>) in the browse window's Diff tab and in the
///  compare window (FormDiff).
/// </summary>
internal sealed class DiffFileMenuTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private TestRepository _repo = null!;

    [SetUp]
    public void SetUp()
    {
        _repo = new TestRepository();
        File.WriteAllText(Path.Combine(_repo.Path, "z.md"), "z");
        _repo.Run("add", "z.md");
        _repo.Run("commit", "-q", "-m", "add z");
        File.WriteAllText(Path.Combine(_repo.Path, "z.md"), "changed");
        _repo.Run("add", "-A");
        _repo.Run("commit", "-q", "-m", "change z");
    }

    [TearDown]
    public void TearDown()
    {
        TestAppBuilder.Preferences.RevisionGraphShowArtificialCommits = false;
        _repo.Dispose();
    }

    [AvaloniaTest]
    public void A_commits_file_gets_upstreams_items_for_a_commit()
    {
        MainWindow window = OpenWindow();
        ListBox files = SelectCommitFile(window, "change z", "z.md");

        OpenMenu(files);

        Item(files, "tsmiStageFile").IsVisible.Should().BeFalse();
        Item(files, "tsmiUnstageFile").IsVisible.Should().BeFalse();
        Item(files, "tsmiResetFileTo").IsEnabled.Should().BeTrue();
        string branch = _repo.Run("branch", "--show-current").Trim();
        Item(files, "tsmiResetFileToSelected").Header.Should()
            .Be($"Second: B {Short("HEAD")}: {branch}", "upstream names a commit by its first branch");
        Item(files, "tsmiResetFileToParent").Header.Should().Be("First: A " + Short("HEAD~1") + ": add z");
        Item(files, "tsmiOpenRevisionFile").IsVisible.Should().BeTrue();
        Item(files, "tsmiSaveAs").IsVisible.Should().BeTrue();
        Item(files, "tsmiDeleteFile").IsVisible.Should().BeFalse();
        Item(files, "tsmiFileHistory").IsEnabled.Should().BeTrue();
        Item(files, "tsmiAddFileToGitIgnore").IsVisible.Should().BeFalse();
        Item(files, "tsmiStopTracking").IsVisible.Should().BeTrue();
        window.Close();
    }

    [AvaloniaTest]
    public void Reset_file_to_first_restores_the_parents_content_after_upstreams_question()
    {
        MainWindow window = OpenWindow();
        ListBox files = SelectCommitFile(window, "change z", "z.md");
        OpenMenu(files);

        Click(Item(files, "tsmiResetFileToParent"));

        WaitUntil(() => window.OwnedWindows.OfType<ResetChangesWindow>().Any());
        ResetChangesWindow question = window.OwnedWindows.OfType<ResetChangesWindow>().Single();
        Find<TextBlock>(question, "MessageText").Text.Should()
            .Be($"Are you sure you want to reset all selected files to First: A {Short("HEAD~1")}: add z?");
        Click(Find<Button>(question, "ResetButton"));
        WaitUntil(() => _repo.ReadFile("z.md") == "z");
        window.Close();
    }

    [AvaloniaTest]
    public void Filter_file_in_grid_shows_only_the_commits_that_changed_the_file()
    {
        MainWindow window = OpenWindow();
        ListBox files = SelectCommitFile(window, "change z", "z.md");
        OpenMenu(files);

        Click(Item(files, "tsmiFilterFileInGrid"));

        ListBox commits = Find<ListBox>(window, "CommitList");
        WaitUntil(() => commits.Items.Cast<CommitListItem>().Select(item => item.Row.Subject)
            .SequenceEqual(["change z", "add z"]));
        window.Close();
    }

    [AvaloniaTest]
    public void Open_this_revision_saves_the_file_as_it_is_in_the_commit()
    {
        string temp = Path.Combine(Path.GetTempPath(), "z.md");
        File.Delete(temp);
        MainWindow window = OpenWindow();
        ListBox files = SelectCommitFile(window, "add z", "z.md");
        OpenMenu(files);

        Click(Item(files, "tsmiOpenRevisionFile"));

        WaitUntil(() => File.Exists(temp) && File.ReadAllText(temp) == "z");
        File.Delete(temp);
        window.Close();
    }

    [AvaloniaTest]
    public void An_untracked_file_in_the_working_directory_can_be_staged_or_deleted()
    {
        TestAppBuilder.Preferences.RevisionGraphShowArtificialCommits = true;
        File.WriteAllText(Path.Combine(_repo.Path, "new.txt"), "n");
        MainWindow window = OpenWindow();
        ListBox files = SelectCommitFile(window, ArtificialCommits.WorkTreeSubject, "new.txt");
        OpenMenu(files);

        Item(files, "tsmiStageFile").IsVisible.Should().BeTrue();
        Item(files, "tsmiDeleteFile").IsVisible.Should().BeTrue();
        Item(files, "tsmiAddFileToGitIgnore").IsVisible.Should().BeTrue();
        Item(files, "tsmiResetFileTo").IsEnabled.Should().BeFalse();
        Item(files, "tsmiOpenRevisionFile").IsVisible.Should().BeFalse();

        Click(Item(files, "tsmiDeleteFile"));
        WaitUntil(() => window.OwnedWindows.OfType<ConfirmWindow>().Any());
        Click(Find<Button>(window.OwnedWindows.OfType<ConfirmWindow>().Single(), "ConfirmButton"));
        WaitUntil(() => _repo.ReadFile("new.txt") is null);
        window.Close();
    }

    [AvaloniaTest]
    public void Several_selected_files_are_reset_together_as_upstreams_multi_selection()
    {
        TestAppBuilder.Preferences.RevisionGraphShowArtificialCommits = true;
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "edited");
        File.WriteAllText(Path.Combine(_repo.Path, "z.md"), "edited");
        MainWindow window = OpenWindow();
        ListBox files = SelectCommitFile(window, ArtificialCommits.WorkTreeSubject, "a.txt");
        files.SelectedItems!.Add(files.Items.Cast<CommitFile>().First(item => item.Path == "z.md"));
        Dispatcher.UIThread.RunJobs();
        OpenMenu(files);

        Item(files, "tsmiOpenWorkingDirectoryFile").IsVisible.Should().BeFalse("it opens one file");
        Click(Item(files, "tsmiResetFileToParent"));
        WaitUntil(() => window.OwnedWindows.OfType<ResetChangesWindow>().Any());
        Click(Find<Button>(window.OwnedWindows.OfType<ResetChangesWindow>().Single(), "ResetButton"));

        WaitUntil(() => _repo.ReadFile("a.txt") == "two" && _repo.ReadFile("z.md") == "changed");
        window.Close();
    }

    [AvaloniaTest]
    public void The_compare_window_has_upstreams_menu_without_the_file_tree_and_the_grid_filter()
    {
        MainWindow window = OpenWindow();
        RevisionDiffWindow diff = CompareParentWithHead(window);
        ListBox files = SelectDiffFile(diff, "z.md");

        OpenMenu(files);

        Item(files, "tsmiShowInFileTree").IsVisible.Should().BeFalse("upstream's FormDiff does not bind the file tree");
        Item(files, "tsmiFilterFileInGrid").IsVisible.Should().BeFalse("FormDiff has no grid");
        Item(files, "tsmiFileHistory").IsEnabled.Should().BeTrue();
        Item(files, "tsmiFileHistory").FontWeight.Should().Be(FontWeight.Normal);
        Item(files, "tsmiResetFileToSelected").Header.Should().Be("Second: B " + Short("HEAD"),
            "upstream's FormDiff list names a commit by its short hash");
        Item(files, "tsmiResetFileToParent").Header.Should().Be("First: A " + Short("HEAD~1"));
        Item(files, "tsmiStageFile").IsVisible.Should().BeFalse();
        diff.Close();
        window.Close();
    }

    [AvaloniaTest]
    public void The_compare_windows_menu_resets_a_file_and_opens_its_history()
    {
        MainWindow window = OpenWindow();
        RevisionDiffWindow diff = CompareParentWithHead(window);
        ListBox files = SelectDiffFile(diff, "z.md");
        OpenMenu(files);

        Click(Item(files, "tsmiResetFileToParent"));
        WaitUntil(() => diff.OwnedWindows.OfType<ResetChangesWindow>().Any());
        Click(Find<Button>(diff.OwnedWindows.OfType<ResetChangesWindow>().Single(), "ResetButton"));
        WaitUntil(() => _repo.ReadFile("z.md") == "z");

        OpenMenu(files);
        Click(Item(files, "tsmiFileHistory"));
        WaitUntil(() => diff.OwnedWindows.OfType<FileHistoryWindow>().Any());
        diff.OwnedWindows.OfType<FileHistoryWindow>().Single().Close();
        diff.Close();
        window.Close();
    }

    private MainWindow OpenWindow()
    {
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        window.Show();
        window.OpenRepository(_repo.Path);
        WaitUntil(() => !window.IsBusy);
        return window;
    }

    // The grid's Compare > Compare to BASE from "add z" to "change z": upstream's FormDiff.
    private static RevisionDiffWindow CompareParentWithHead(MainWindow window)
    {
        ListBox commits = Find<ListBox>(window, "CommitList");
        WaitUntil(() => commits.Items.Cast<CommitListItem>().Any(item => item.Row.Subject == "add z"));
        commits.SelectedItem = commits.Items.Cast<CommitListItem>().Single(item => item.Row.Subject == "add z");
        Click(Find<MenuItem>(window, "SelectAsBaseMenuItem"));
        commits.SelectedItem = commits.Items.Cast<CommitListItem>().Single(item => item.Row.Subject == "change z");
        Click(Find<MenuItem>(window, "CompareToBaseMenuItem"));
        WaitUntil(() => window.OwnedWindows.OfType<RevisionDiffWindow>().SingleOrDefault()?.DiffLoaded.IsCompleted
                        == true);
        return window.OwnedWindows.OfType<RevisionDiffWindow>().Single();
    }

    private static ListBox SelectDiffFile(RevisionDiffWindow diff, string file)
    {
        ListBox files = Find<ListBox>(diff, "DiffFiles");
        WaitUntil(() => files.Items.Cast<RevisionDiffFile>().Any(item => item.Path == file));
        files.SelectedItem = files.Items.Cast<RevisionDiffFile>().First(item => item.Path == file);
        Dispatcher.UIThread.RunJobs();
        return files;
    }

    private static ListBox SelectCommitFile(MainWindow window, string subject, string file)
    {
        ListBox commits = Find<ListBox>(window, "CommitList");
        WaitUntil(() => commits.Items.Cast<CommitListItem>().Any(item => item.Row.Subject == subject));
        commits.SelectedItem = commits.Items.Cast<CommitListItem>().First(item => item.Row.Subject == subject);
        ListBox files = Find<ListBox>(window, "CommitFilesList");
        WaitUntil(() => files.Items.Cast<CommitFile>().Any(item => item.Path == file));
        files.SelectedItem = files.Items.Cast<CommitFile>().First(item => item.Path == file);
        Dispatcher.UIThread.RunJobs();
        return files;
    }

    private static MenuItem Item(ListBox list, string name) => Find<MenuItem>(list.ContextMenu!, name);

    private string Short(string revision) => _repo.Run("rev-parse", "--short=8", revision).Trim();

    // The menu updates itself for the selection when it opens, as upstream's ItemContextMenu_Opening.
    private static void OpenMenu(ListBox list)
    {
        list.RaiseEvent(new ContextRequestedEventArgs());
        Dispatcher.UIThread.RunJobs();
        list.ContextMenu!.Close();
    }

    private static void Click(Control control)
        => control.RaiseEvent(new RoutedEventArgs(control is MenuItem ? MenuItem.ClickEvent : Button.ClickEvent));

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

using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using GitCommands;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Diff;
using GitExtensions.Xplat.Core.Platform;
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

    [AvaloniaTest]
    public void With_the_setting_Blame_shows_in_the_diff_tab_until_clicked_again_or_another_file_is_selected()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "one.txt"), "one\ntwo\nthree\n");
        File.WriteAllText(Path.Combine(_repo.Path, "other.txt"), "other\n");
        _repo.Run("add", "-A");
        _repo.Run("commit", "-q", "-m", "two files");
        TestAppBuilder.Preferences.UseDiffViewerForBlame = true;
        try
        {
            MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
            window.Show();
            window.OpenRepository(_repo.Path);
            WaitUntil(() => !window.IsBusy);
            Avalonia.Controls.TabControl tabs = Find<Avalonia.Controls.TabControl>(window, "DetailTabs");
            tabs.SelectedItem = Find<Avalonia.Controls.TabItem>(window, "DiffTab");
            ListBox files = Find<ListBox>(window, "CommitFilesList");
            WaitUntil(() => files.Items.OfType<CommitFile>().Any(file => file.Path == "one.txt"));
            files.SelectedItem = files.Items.OfType<CommitFile>().Single(file => file.Path == "one.txt");
            DiffView diff = Find<DiffView>(window, "CommitDiff");
            BlameView blame = Find<BlameView>(window, "DiffTabBlame");
            WaitUntil(() => diff.Lines.Any(line => line.Text == "+three"));
            diff.FindControl<ListBox>("DiffList")!.SelectedItem = diff.Lines.Single(line => line.Text == "+three");

            Find<MenuItem>(files.ContextMenu!, "tsmiBlame").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

            WaitUntil(() => blame.IsVisible && blame.Lines.Count == 3 && blame.CurrentFileLine == 3);
            diff.IsVisible.Should().BeFalse();
            tabs.SelectedItem.Should().Be(Find<Avalonia.Controls.TabItem>(window, "DiffTab"), "not the File tree tab");

            Find<MenuItem>(files.ContextMenu!, "tsmiBlame").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

            WaitUntil(() => diff.IsVisible && !blame.IsVisible && diff.CurrentFileLine == 3);

            Find<MenuItem>(files.ContextMenu!, "tsmiBlame").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            WaitUntil(() => blame.IsVisible);
            files.SelectedItem = files.Items.OfType<CommitFile>().Single(file => file.Path == "other.txt");

            WaitUntil(() => diff.IsVisible && !blame.IsVisible && diff.Lines.Any(line => line.Text == "+other"));
            window.Close();
        }
        finally
        {
            TestAppBuilder.Preferences.UseDiffViewerForBlame = false;
        }
    }

    [TearDown]
    public void TearDown()
    {
        DiffFileRemember.Forget();
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
        Item(files, "tsmiOpenRevisionFileWith").IsVisible.Should().Be(OperatingSystem.IsWindows(),
            "upstream's \"Open with\" dialog is Windows'");
        Item(files, "tsmiSaveAs").IsVisible.Should().BeTrue();
        Item(files, "tsmiDeleteFile").IsVisible.Should().BeFalse();
        Item(files, "tsmiFileHistory").IsEnabled.Should().BeTrue();
        Item(files, "tsmiAddFileToGitIgnore").IsVisible.Should().BeFalse();
        Item(files, "tsmiStopTracking").IsVisible.Should().BeTrue();
        Item(files, "tsmiInteractiveAdd").IsVisible.Should().BeFalse("a commit's file is not the working directory's");
        Item(files, "tsmiResetChunkOfFile").IsVisible.Should().BeFalse();
        if (OperatingSystem.IsWindows())
        {
            Task init = VisualStudio.InitAsync();
            WaitUntil(() => init.IsCompleted);
            OpenMenu(files);
            Item(files, "tsmiOpenInVisualStudio").IsVisible.Should().Be(VisualStudio.IsInstalled,
                "upstream offers the working directory file in Visual Studio when it is installed");
        }

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
    public void Upstreams_F_hotkey_in_the_diff_tabs_list_filters_the_grid_by_the_file()
    {
        MainWindow window = OpenWindow();
        Find<TabControl>(window, "DetailTabs").SelectedItem = Find<TabItem>(window, "DiffTab");
        ListBox files = SelectCommitFile(window, "change z", "z.md");
        WaitUntil(() => files.ContainerFromItem(files.SelectedItem!) is not null);
        files.ContainerFromItem(files.SelectedItem!)!.Focus();

        window.KeyPress(Key.F, RawInputModifiers.None, PhysicalKey.F, keySymbol: null);

        ListBox commits = Find<ListBox>(window, "CommitList");
        WaitUntil(() => commits.Items.Cast<CommitListItem>().Select(item => item.Row.Subject)
            .SequenceEqual(["change z", "add z"]));
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

        WaitUntil(() => ReadWritten(temp) == "z");
        File.Delete(temp);
        window.Close();
        return;

        // Git may still be writing the file, or the program it opens with may be reading it.
        static string? ReadWritten(string path)
        {
            try
            {
                return File.Exists(path) ? File.ReadAllText(path) : null;
            }
            catch (IOException)
            {
                return null;
            }
        }
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
        Item(files, "tsmiInteractiveAdd").IsVisible.Should().BeTrue(
            "upstream offers git's interactive add for one working directory file");
        Item(files, "tsmiResetChunkOfFile").IsVisible.Should().BeTrue();

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

    [AvaloniaTest]
    public void Rename_move_asks_for_the_new_name_and_moves_the_file_with_git()
    {
        MainWindow window = OpenWindow();
        ListBox files = SelectCommitFile(window, "change z", "z.md");
        OpenMenu(files);
        Item(files, "tsmiMove").IsVisible.Should().BeTrue();

        Click(Item(files, "tsmiMove"));

        WaitUntil(() => window.OwnedWindows.OfType<PromptWindow>().Any());
        PromptWindow prompt = window.OwnedWindows.OfType<PromptWindow>().Single();
        prompt.Title.Should().Be("Rename / move");
        TextBox name = Find<TextBox>(prompt, "ValueBox");
        name.Text.Should().Be("z.md");
        name.Text = "docs/y.md";
        Click(Find<Button>(prompt, "OkButton"));
        WaitUntil(() => _repo.ReadFile("docs/y.md") == "changed" && _repo.ReadFile("z.md") is null);
        _repo.Run("status", "--porcelain").Should().Contain("R  z.md -> docs/y.md");
        window.Close();
    }

    [AvaloniaTest]
    public void F2_in_the_diff_tabs_file_list_renames_the_file_as_upstreams_hotkey()
    {
        MainWindow window = OpenWindow();
        Find<TabControl>(window, "DetailTabs").SelectedItem = Find<TabItem>(window, "DiffTab");
        ListBox files = SelectCommitFile(window, "change z", "z.md");
        WaitUntil(() => files.ContainerFromItem(files.SelectedItem!) is not null);
        files.ContainerFromItem(files.SelectedItem!)!.Focus();

        window.KeyPress(Key.F2, RawInputModifiers.None, PhysicalKey.F2, keySymbol: null);

        WaitUntil(() => window.OwnedWindows.OfType<PromptWindow>().Any());
        Click(Find<Button>(window.OwnedWindows.OfType<PromptWindow>().Single(), "CancelButton"));
        WaitUntil(() => !window.OwnedWindows.OfType<PromptWindow>().Any());
        _repo.ReadFile("z.md").Should().Be("changed");
        window.Close();
    }

    [AvaloniaTest]
    public void A_remembered_file_is_offered_for_a_diff_with_another_revision()
    {
        MainWindow window = OpenWindow();
        ListBox files = SelectCommitFile(window, "change z", "z.md");
        OpenMenu(files);
        Item(files, "tsmiDiffWithRemembered").IsVisible.Should().BeFalse("nothing is remembered yet");
        Item(files, "tsmiRememberSecondRevDiff").IsEnabled.Should().BeTrue();

        Click(Item(files, "tsmiRememberSecondRevDiff"));
        OpenMenu(files);
        Item(files, "tsmiDiffWithRemembered").IsEnabled.Should().BeFalse("upstream does not diff a file with itself");
        files = SelectCommitFile(window, "add z", "z.md");
        OpenMenu(files);

        MenuItem diffWithRemembered = Item(files, "tsmiDiffWithRemembered");
        diffWithRemembered.IsVisible.Should().BeTrue();
        diffWithRemembered.IsEnabled.Should().BeTrue();
        diffWithRemembered.Header.Should().Be("_Diff with \"z.md\"");
        Item(files, "tsmiDiffTwoSelected").IsVisible.Should().BeFalse("one file is selected");
        window.Close();
    }

    [AvaloniaTest]
    public void Two_selected_files_are_offered_for_a_diff_with_each_other()
    {
        TestAppBuilder.Preferences.RevisionGraphShowArtificialCommits = true;
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "edited");
        File.WriteAllText(Path.Combine(_repo.Path, "z.md"), "edited");
        MainWindow window = OpenWindow();
        ListBox files = SelectCommitFile(window, ArtificialCommits.WorkTreeSubject, "a.txt");
        files.SelectedItems!.Add(files.Items.Cast<CommitFile>().First(item => item.Path == "z.md"));
        Dispatcher.UIThread.RunJobs();

        OpenMenu(files);

        Item(files, "tsmiDiffTwoSelected").IsVisible.Should().BeTrue();
        Item(files, "tsmiDiffTwoSelected").IsEnabled.Should().BeTrue();
        Item(files, "tsmiRememberSecondRevDiff").IsVisible.Should().BeFalse();
        Item(files, "tsmiMove").IsVisible.Should().BeFalse("upstream renames one file");
        window.Close();
    }

    [AvaloniaTest]
    public void A_selected_file_is_selected_once_after_the_list_is_filled_again()
    {
        MainWindow window = OpenWindow();
        ListBox files = SelectCommitFile(window, "change z", "z.md");
        FileFilterBox filter = Find<FileFilterBox>(window, "DiffFilesFilterBox");

        filter.Text = "z";
        Dispatcher.UIThread.RunJobs();
        filter.Text = "";
        Dispatcher.UIThread.RunJobs();

        files.SelectedItems!.Cast<CommitFile>().Select(file => file.Path).Should().Equal("z.md");
        OpenMenu(files);
        Item(files, "tsmiMove").IsVisible.Should().BeTrue("one file is selected");
        window.Close();
    }

    [AvaloniaTest]
    public void Ctrl_A_in_the_diff_tabs_tree_selects_all_its_files_and_expands_their_folders()
    {
        DiffListSortType sorting = TestAppBuilder.Preferences.DiffListSorting;
        TestAppBuilder.Preferences.DiffListSorting = DiffListSortType.FilePath;
        try
        {
            Directory.CreateDirectory(Path.Combine(_repo.Path, "src"));
            File.WriteAllText(Path.Combine(_repo.Path, "src", "a.cs"), "a");
            File.WriteAllText(Path.Combine(_repo.Path, "src", "b.cs"), "b");
            File.WriteAllText(Path.Combine(_repo.Path, "c.md"), "c");
            _repo.Run("add", "-A");
            _repo.Run("commit", "-q", "-m", "add src");
            MainWindow window = OpenWindow();
            Find<TabControl>(window, "DetailTabs").SelectedItem = Find<TabItem>(window, "DiffTab");
            ListBox commits = Find<ListBox>(window, "CommitList");
            WaitUntil(() => commits.Items.Cast<CommitListItem>().Any(item => item.Row.Subject == "add src"));
            commits.SelectedItem = commits.Items.Cast<CommitListItem>().First(item => item.Row.Subject == "add src");
            ListBox files = Find<ListBox>(window, "CommitFilesList");
            WaitUntil(() => files.Items.OfType<CommitFile>().Count() == 3);
            files.SelectedItem = files.Items.OfType<FileTreeRow>().Single();
            Click(Item(files, "_collapseAll"));
            WaitUntil(() => files.Items.OfType<FileTreeRow>().Single().IsExpanded == false);
            CommitFile last = files.Items.OfType<CommitFile>().Single();
            files.SelectedItem = last;
            WaitUntil(() => files.ContainerFromItem(last) is not null);
            files.ContainerFromItem(last)!.Focus();

            window.KeyPress(Key.A, TestKeys.Command, PhysicalKey.A, keySymbol: null);

            files.SelectedItems!.Cast<object>().Should().AllBeOfType<CommitFile>("upstream selects files only")
                .And.Subject.Cast<CommitFile>().Select(file => file.Path).Should()
                .BeEquivalentTo(["src/a.cs", "src/b.cs", "c.md"]);
            files.Items.OfType<FileTreeRow>().Single().IsExpanded.Should().BeTrue("the folder's files are rows again");
            window.Close();
        }
        finally
        {
            TestAppBuilder.Preferences.DiffListSorting = sorting;
        }
    }

    [AvaloniaTest]
    public void The_compare_windows_list_has_upstreams_file_list_hotkeys()
    {
        MainWindow window = OpenWindow();
        RevisionDiffWindow diff = CompareParentWithHead(window);
        ListBox files = SelectDiffFile(diff, "z.md");
        WaitUntil(() => files.ContainerFromItem(files.SelectedItem!) is not null);
        files.ContainerFromItem(files.SelectedItem!)!.Focus();

        diff.KeyPress(Key.R, RawInputModifiers.None, PhysicalKey.R, keySymbol: null);

        WaitUntil(() => diff.OwnedWindows.OfType<ResetChangesWindow>().Any());
        Click(Find<Button>(diff.OwnedWindows.OfType<ResetChangesWindow>().Single(), "ResetButton"));
        WaitUntil(() => _repo.ReadFile("z.md") == "z");

        files.SelectedItems!.Clear();
        files.ContainerFromIndex(0)!.Focus();
        diff.KeyPress(Key.A, TestKeys.Command, PhysicalKey.A, keySymbol: null);
        files.SelectedItems.Cast<RevisionDiffFile>().Select(file => file.Path).Should().Equal("z.md");
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

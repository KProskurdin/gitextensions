using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;
using GitCommands;
using GitExtensions.Xplat.Core.Repository;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

internal sealed class CommitFileListsTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(20);

    private TestRepository _repo = null!;

    [SetUp]
    public void Setup()
    {
        _repo = new TestRepository();
        TestAppBuilder.Preferences.DiffListSorting = DiffListSortType.FilePath;
    }

    [TearDown]
    public void TearDown()
    {
        TestAppBuilder.Preferences.DiffListSorting = DiffListSortType.FilePath;
        _repo.Dispose();
    }

    [AvaloniaTest]
    public void Empty_lists_show_upstreams_placeholders()
    {
        CommitWindow commit = OpenCommitWindow();

        Find<TextBlock>(commit, "UnstagedNoFilesText").IsVisible.Should().BeTrue();
        Find<TextBlock>(commit, "UnstagedNoFilesText").Text.Should().Be("There are no unstaged changes");
        Find<TextBlock>(commit, "StagedNoFilesText").IsVisible.Should().BeTrue();
        Find<TextBlock>(commit, "StagedNoFilesText").Text.Should().Be("There are no staged changes");
        commit.Close();
    }

    [AvaloniaTest]
    public void The_filter_box_shows_the_files_the_regular_expression_matches()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "changed");
        File.WriteAllText(Path.Combine(_repo.Path, "notes.md"), "new");
        CommitWindow commit = OpenCommitWindow();
        ListBox unstaged = Find<ListBox>(commit, "UnstagedList");
        WaitUntil(() => unstaged.ItemCount == 2);

        Find<FileFilterBox>(commit, "UnstagedFilterBox").Text = @"\.MD$";

        WaitUntil(() => Paths(unstaged).SequenceEqual(["notes.md"]));
        Find<TextBlock>(commit, "UnstagedNoFilesText").IsVisible.Should().BeFalse();

        Find<FileFilterBox>(commit, "UnstagedFilterBox").Text = "(";

        WaitUntil(() => Paths(unstaged).Count == 2);
        Find<FileFilterBox>(commit, "UnstagedFilterBox").Error.Should().NotBeNull();
        commit.Close();
    }

    [AvaloniaTest]
    public void Stage_all_stages_only_the_filtered_files_and_clears_the_filter()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "changed");
        File.WriteAllText(Path.Combine(_repo.Path, "notes.md"), "new");
        CommitWindow commit = OpenCommitWindow();
        ListBox unstaged = Find<ListBox>(commit, "UnstagedList");
        WaitUntil(() => unstaged.ItemCount == 2);
        ToolButton stageAll = Find<ToolButton>(commit, "StageAllButton");
        ToolTip.GetTip(stageAll).Should().Be("Stage all");

        Find<FileFilterBox>(commit, "UnstagedFilterBox").Text = @"\.md$";

        WaitUntil(() => Paths(unstaged).SequenceEqual(["notes.md"]));
        ToolTip.GetTip(stageAll).Should().Be("Stage filtered");
        stageAll.Image.Should().Be("StageAllFiltered");

        Click(commit, "StageAllButton");

        WaitUntil(() => Paths(Find<ListBox>(commit, "StagedList")).SequenceEqual(["notes.md"]));
        WaitUntil(() => Paths(unstaged).SequenceEqual(["a.txt"]));
        Find<FileFilterBox>(commit, "UnstagedFilterBox").Text.Should().BeEmpty();
        ToolTip.GetTip(stageAll).Should().Be("Stage all");
        commit.Close();
    }

    [AvaloniaTest]
    public void Double_click_stages_an_unstaged_file_and_unstages_a_staged_one()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "changed");
        CommitWindow commit = OpenCommitWindow();
        ListBox unstaged = Find<ListBox>(commit, "UnstagedList");
        ListBox staged = Find<ListBox>(commit, "StagedList");
        WaitUntil(() => unstaged.ItemCount == 1);
        unstaged.SelectedIndex = 0;

        unstaged.RaiseEvent(new TappedEventArgs(InputElement.DoubleTappedEvent, null!));

        WaitUntil(() => Paths(staged).SequenceEqual(["a.txt"]) && unstaged.ItemCount == 0);
        staged.SelectedIndex = 0;

        staged.RaiseEvent(new TappedEventArgs(InputElement.DoubleTappedEvent, null!));

        WaitUntil(() => Paths(unstaged).SequenceEqual(["a.txt"]) && staged.ItemCount == 0);
        commit.Close();
    }

    [AvaloniaTest]
    public void Group_by_status_orders_both_lists_and_keeps_the_choice()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "changed");
        File.WriteAllText(Path.Combine(_repo.Path, "0-new.txt"), "new");
        CommitWindow commit = OpenCommitWindow();
        ListBox unstaged = Find<ListBox>(commit, "UnstagedList");
        WaitUntil(() => unstaged.ItemCount == 2);
        Paths(unstaged).Should().Equal("0-new.txt", "a.txt");

        Click(commit, "StagedByStatusButton");

        Paths(unstaged).Should().Equal("a.txt", "0-new.txt");
        TestAppBuilder.Preferences.DiffListSorting.Should().Be(DiffListSortType.FileStatus);
        Find<ToolToggleButton>(commit, "UnstagedByStatusButton").IsChecked.Should().BeTrue();
        Find<ToolToggleButton>(commit, "UnstagedByPathButton").IsChecked.Should().BeFalse();
        commit.Close();
    }

    [AvaloniaTest]
    public void Show_untracked_files_off_hides_them()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "changed");
        File.WriteAllText(Path.Combine(_repo.Path, "notes.md"), "new");
        CommitWindow commit = OpenCommitWindow();
        ListBox unstaged = Find<ListBox>(commit, "UnstagedList");
        WaitUntil(() => unstaged.ItemCount == 2);
        MenuItem showUntracked = Find<MenuItem>(commit, "UnstagedShowUntrackedMenuItem");

        showUntracked.IsChecked = false;
        showUntracked.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

        Paths(unstaged).Should().Equal("a.txt");
        commit.Close();
    }

    [AvaloniaTest]
    public void The_menus_show_upstreams_items_for_each_list()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "notes.md"), "new");
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "changed");
        _repo.Run("add", "a.txt");
        CommitWindow commit = OpenCommitWindow();
        ListBox unstaged = Find<ListBox>(commit, "UnstagedList");
        ListBox staged = Find<ListBox>(commit, "StagedList");
        WaitUntil(() => unstaged.ItemCount == 1 && staged.ItemCount == 1);

        unstaged.SelectedIndex = 0;
        OpenMenu(unstaged);

        Find<MenuItem>(commit, "UnstagedStageFileMenuItem").IsVisible.Should().BeTrue();
        Find<MenuItem>(commit, "UnstagedResetFileMenuItem").IsEnabled.Should().BeFalse("an untracked file has no version to reset to");
        Find<MenuItem>(commit, "UnstagedAddToGitIgnoreMenuItem").IsVisible.Should().BeTrue();
        Find<MenuItem>(commit, "UnstagedStopTrackingMenuItem").IsVisible.Should().BeFalse();
        Find<MenuItem>(commit, "UnstagedDeleteFileMenuItem").Header.Should().Be("Delete file");

        staged.SelectedIndex = 0;
        OpenMenu(staged);

        Find<MenuItem>(commit, "StagedUnstageFileMenuItem").IsVisible.Should().BeTrue();
        Find<MenuItem>(commit, "StagedResetFileToFirstMenuItem").Header.Should().Be("First: A HEAD");
        Find<MenuItem>(commit, "StagedStopTrackingMenuItem").IsVisible.Should().BeTrue();
        commit.Close();
    }

    [AvaloniaTest]
    public void Reset_file_in_the_staged_list_restores_the_file_from_HEAD()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "changed");
        _repo.Run("add", "a.txt");
        CommitWindow commit = OpenCommitWindow();
        ListBox staged = Find<ListBox>(commit, "StagedList");
        WaitUntil(() => staged.ItemCount == 1);
        staged.SelectedIndex = 0;

        Click(commit, "StagedResetFileToFirstMenuItem");
        WaitUntil(() => commit.OwnedWindows.OfType<ConfirmWindow>().Any());
        Click(commit.OwnedWindows.OfType<ConfirmWindow>().Single(), "ConfirmButton");

        WaitUntil(() => staged.ItemCount == 0 && Find<ListBox>(commit, "UnstagedList").ItemCount == 0);
        File.ReadAllText(Path.Combine(_repo.Path, "a.txt")).Should().Be("two");
        commit.Close();
    }

    [AvaloniaTest]
    public void Delete_file_asks_and_removes_the_file_from_disk()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "notes.md"), "new");
        CommitWindow commit = OpenCommitWindow();
        ListBox unstaged = Find<ListBox>(commit, "UnstagedList");
        WaitUntil(() => unstaged.ItemCount == 1);
        unstaged.SelectedIndex = 0;

        Click(commit, "UnstagedDeleteFileMenuItem");
        WaitUntil(() => commit.OwnedWindows.OfType<ConfirmWindow>().Any());
        Click(commit.OwnedWindows.OfType<ConfirmWindow>().Single(), "ConfirmButton");

        WaitUntil(() => unstaged.ItemCount == 0);
        File.Exists(Path.Combine(_repo.Path, "notes.md")).Should().BeFalse();
        commit.Close();
    }

    [AvaloniaTest]
    public void Stop_tracking_takes_the_file_out_of_the_index_and_keeps_it_on_disk()
    {
        CommitWindow commit = OpenCommitWindow();
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "changed");
        Click(commit, "UnstagedRefreshButton");
        ListBox unstaged = Find<ListBox>(commit, "UnstagedList");
        WaitUntil(() => unstaged.ItemCount == 1);
        unstaged.SelectedIndex = 0;

        Click(commit, "UnstagedStopTrackingMenuItem");

        WaitUntil(() => Paths(Find<ListBox>(commit, "StagedList")).SequenceEqual(["a.txt"]));
        _repo.Run("status", "--porcelain").Should().Contain("D  a.txt");
        File.Exists(Path.Combine(_repo.Path, "a.txt")).Should().BeTrue();
        commit.Close();
    }

    [AvaloniaTest]
    public void Both_lists_are_upstreams_folder_tree_and_one_tree_button_makes_both_flat()
    {
        Directory.CreateDirectory(Path.Combine(_repo.Path, "src"));
        File.WriteAllText(Path.Combine(_repo.Path, "src", "a.cs"), "a");
        File.WriteAllText(Path.Combine(_repo.Path, "src", "b.cs"), "b");
        _repo.Run("add", "src/b.cs");
        CommitWindow commit = OpenCommitWindow();
        ListBox unstaged = Find<ListBox>(commit, "UnstagedList");
        ListBox staged = Find<ListBox>(commit, "StagedList");
        WaitUntil(() => Paths(unstaged).Count == 1 && Paths(staged).Count == 1);

        unstaged.Items.OfType<FileTreeRow>().Select(row => row.Text).Should().Equal("src");
        staged.Items.OfType<FileTreeRow>().Select(row => row.Text).Should().Equal("src");
        Find<ToolSplitButton>(commit, "StagedAsTreeButton").RaiseEvent(new RoutedEventArgs(SplitButton.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        TestAppBuilder.Preferences.DiffListSorting.Should().Be(DiffListSortType.FilePathFlat);
        unstaged.Items.OfType<FileTreeRow>().Should().BeEmpty("upstream's sorting is one setting for every list");
        staged.Items.OfType<FileTreeRow>().Should().BeEmpty();
        Find<ToolSplitButton>(commit, "UnstagedAsTreeButton").Image.Should().Be("DocumentTree");
        commit.Close();
    }

    [AvaloniaTest]
    public void The_working_directory_lists_settings_show_ignored_and_assumed_unchanged_files_when_checked()
    {
        File.WriteAllText(Path.Combine(_repo.Path, ".gitignore"), "*.log\n");
        _repo.Run("add", ".gitignore");
        _repo.Run("commit", "-q", "-m", "ignore logs");
        File.WriteAllText(Path.Combine(_repo.Path, "build.log"), "log");
        _repo.Run("update-index", "--assume-unchanged", "a.txt");
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "hidden change");
        File.WriteAllText(Path.Combine(_repo.Path, "new.txt"), "new");
        CommitWindow commit = OpenCommitWindow();
        ListBox unstaged = Find<ListBox>(commit, "UnstagedList");
        WaitUntil(() => Paths(unstaged).SequenceEqual(["new.txt"]));

        Click(commit, "UnstagedShowIgnoredMenuItem", check: true);
        WaitUntil(() => Paths(unstaged).Contains("build.log"));

        Click(commit, "UnstagedShowAssumeUnchangedMenuItem", check: true);
        WaitUntil(() => Paths(unstaged).Contains("a.txt"));

        Click(commit, "UnstagedShowIgnoredMenuItem", check: false);
        Click(commit, "UnstagedShowAssumeUnchangedMenuItem", check: false);
        WaitUntil(() => Paths(unstaged).SequenceEqual(["new.txt"]));
        Find<MenuItem>(commit, "StagedShowSkipWorktreeMenuItem").IsEnabled.Should().BeFalse("only the working directory's list");
        commit.Close();
    }

    [AvaloniaTest]
    public void Show_untracked_files_starts_as_gits_status_showuntrackedfiles_says()
    {
        _repo.Run("config", "status.showuntrackedfiles", "no");
        File.WriteAllText(Path.Combine(_repo.Path, "new.txt"), "new");
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "changed");
        CommitWindow commit = OpenCommitWindow();
        ListBox unstaged = Find<ListBox>(commit, "UnstagedList");

        WaitUntil(() => Find<MenuItem>(commit, "UnstagedShowUntrackedMenuItem").IsChecked == false
                        && Paths(unstaged).SequenceEqual(["a.txt"]));
        commit.Close();
    }

    [AvaloniaTest]
    public void Copy_relative_paths_posix_puts_the_selected_paths_on_the_clipboard()
    {
        Directory.CreateDirectory(Path.Combine(_repo.Path, "docs"));
        File.WriteAllText(Path.Combine(_repo.Path, "docs", "notes.md"), "new");
        CommitWindow commit = OpenCommitWindow();
        ListBox unstaged = Find<ListBox>(commit, "UnstagedList");
        WaitUntil(() => Paths(unstaged).Count == 1);
        unstaged.Items.OfType<FileTreeRow>().Single().Text.Should().Be("docs", "upstream's tree shows the file's folder");
        unstaged.SelectedItem = unstaged.Items.OfType<FileChange>().Single();

        ((MenuItem)Find<MenuItem>(commit, "UnstagedCopyPathsMenuItem").ItemsSource!.Cast<object>().First())
            .RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Task<string?> text = TopLevel.GetTopLevel(commit)!.Clipboard!.TryGetTextAsync();
        WaitUntil(() => text.IsCompleted);

        text.Result.Should().Be("docs/notes.md");
        commit.Close();
    }

    private static IReadOnlyList<string> Paths(ListBox list)
        => [.. (list.ItemsSource?.OfType<FileChange>() ?? []).Select(change => change.Path)];

    // The menu updates itself for the selection when it opens, as upstream's ItemContextMenu_Opening.
    private static void OpenMenu(ListBox list)
    {
        list.RaiseEvent(new ContextRequestedEventArgs());
        Dispatcher.UIThread.RunJobs();
        list.ContextMenu!.Close();
    }

    private CommitWindow OpenCommitWindow()
    {
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        window.Show();
        window.OpenRepository(_repo.Path);
        WaitUntil(() => !window.IsBusy);
        WaitUntil(() => Find<Button>(window, "CommitDialogButton").IsEnabled);
        Click(window, "CommitDialogButton");
        WaitUntil(() => window.OwnedWindows.OfType<CommitWindow>().Any());
        return window.OwnedWindows.OfType<CommitWindow>().Single();
    }

    // A check box item as a click leaves it: checked or not, then its click.
    private static void Click(Window window, string name, bool check)
    {
        MenuItem item = Find<MenuItem>(window, name);
        item.IsChecked = check;
        item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
    }

    private static void Click(Window window, string name)
    {
        Control control = Find<Control>(window, name);
        control.RaiseEvent(new RoutedEventArgs(control is MenuItem ? MenuItem.ClickEvent : Button.ClickEvent));
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

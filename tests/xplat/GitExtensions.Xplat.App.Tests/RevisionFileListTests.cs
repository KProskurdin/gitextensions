using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using GitCommands;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Operations;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

/// <summary>
///  Upstream's FileStatusList in the browse window's Diff tab (group by, filter, NoFiles) and the commit picker's button.
/// </summary>
internal sealed class RevisionFileListTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private TestRepository _repo = null!;
    private DiffListSortType _sorting;

    [SetUp]
    public void SetUp()
    {
        _sorting = TestAppBuilder.Preferences.DiffListSorting;
        _repo = new TestRepository();
        File.WriteAllText(Path.Combine(_repo.Path, "z.md"), "z");
        _repo.Run("add", "z.md");
        _repo.Run("commit", "-q", "-m", "add z");
        File.WriteAllText(Path.Combine(_repo.Path, "z.md"), "changed");
        File.WriteAllText(Path.Combine(_repo.Path, "b.txt"), "b");
        _repo.Run("add", "-A");
        _repo.Run("commit", "-q", "-m", "change z add b");
    }

    [TearDown]
    public void TearDown()
    {
        TestAppBuilder.Preferences.DiffListSorting = _sorting;
        TestAppBuilder.Preferences.RevisionGraphShowArtificialCommits = false;
        _repo.Dispose();
    }

    [AvaloniaTest]
    public void The_diff_tabs_files_are_grouped_and_filtered_as_upstreams_file_list()
    {
        TestAppBuilder.Preferences.DiffListSorting = DiffListSortType.FilePath;
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        window.Show();
        window.OpenRepository(_repo.Path);
        WaitUntil(() => !window.IsBusy);
        ListBox commits = Find<ListBox>(window, "CommitList");
        commits.SelectedItem = commits.Items.Cast<CommitListItem>().Single(item => item.Row.Subject == "change z add b");
        ListBox files = Find<ListBox>(window, "CommitFilesList");
        WaitUntil(() => files.ItemCount == 2);
        Paths(files).Should().Equal("b.txt", "z.md");
        files.Items.Cast<CommitFile>().Select(file => file.Image).Should().Equal("FileStatusAdded", "FileStatusModified");

        Find<ToolToggleButton>(window, "DiffFilesByStatusButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Paths(files).Should().Equal(["z.md", "b.txt"], "a modified file comes before an added one");

        TextBox filter = Find<TextBox>(window, "DiffFilesFilterBox");
        filter.Text = "\\.md$";
        Dispatcher.UIThread.RunJobs();
        Paths(files).Should().Equal("z.md");
        filter.Text = "nothing";
        Dispatcher.UIThread.RunJobs();
        TextBlock noFiles = Find<TextBlock>(window, "DiffFilesNoFilesText");
        noFiles.IsVisible.Should().BeTrue();
        noFiles.Text.Should().Be("No changes");
        filter.Text = "(";
        Dispatcher.UIThread.RunJobs();
        files.ItemCount.Should().Be(2, "an invalid expression filters nothing");
        ToolTip.GetTip(filter).Should().NotBeNull();
        window.Close();
    }

    [AvaloniaTest]
    public void The_diff_tabs_files_are_upstreams_folder_tree_whose_tree_button_switches_to_the_flat_list()
    {
        TestAppBuilder.Preferences.DiffListSorting = DiffListSortType.FilePath;
        Directory.CreateDirectory(Path.Combine(_repo.Path, "src"));
        File.WriteAllText(Path.Combine(_repo.Path, "src", "a.cs"), "a");
        File.WriteAllText(Path.Combine(_repo.Path, "src", "b.cs"), "b");
        _repo.Run("add", "-A");
        _repo.Run("commit", "-q", "-m", "add src");
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        window.Show();
        window.OpenRepository(_repo.Path);
        WaitUntil(() => !window.IsBusy);
        Find<TabControl>(window, "DetailTabs").SelectedItem = Find<TabItem>(window, "DiffTab");
        ListBox commits = Find<ListBox>(window, "CommitList");
        commits.SelectedItem = commits.Items.Cast<CommitListItem>().Single(item => item.Row.Subject == "add src");
        ListBox files = Find<ListBox>(window, "CommitFilesList");
        WaitUntil(() => files.ItemCount == 3);

        Rows(files).Should().Equal("> src", "a.cs", "b.cs");
        Dispatcher.UIThread.RunJobs();
        files.GetVisualDescendants().OfType<ListBoxItem>().Where(item => item.DataContext is CommitFile)
            .Select(FileRows.GetIndent).Should().OnlyContain(indent => indent.Left > 0, "a folder's files are indented");

        FileTreeRow folder = files.Items.OfType<FileTreeRow>().Single();
        ListBoxItem container = files.GetVisualDescendants().OfType<ListBoxItem>().Single(item => item.DataContext == folder);
        Point point = container.TranslatePoint(new Point(20, container.Bounds.Height / 2), window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
        Rows(files).Should().Equal(["+ src"], "a click collapses the folder");

        Find<ToolSplitButton>(window, "DiffFilesAsTreeButton").RaiseEvent(new RoutedEventArgs(SplitButton.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        TestAppBuilder.Preferences.DiffListSorting.Should().Be(DiffListSortType.FilePathFlat);
        Find<ToolSplitButton>(window, "DiffFilesAsTreeButton").Image.Should().Be("DocumentTree");
        Rows(files).Should().Equal("src/a.cs", "src/b.cs");
        window.Close();
    }

    [AvaloniaTest]
    public void The_settings_menus_show_untracked_files_hides_the_working_directorys_untracked_files()
    {
        TestAppBuilder.Preferences.RevisionGraphShowArtificialCommits = true;
        File.WriteAllText(Path.Combine(_repo.Path, "z.md"), "changed again");
        File.WriteAllText(Path.Combine(_repo.Path, "new.txt"), "new");
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        window.Show();
        window.OpenRepository(_repo.Path);
        WaitUntil(() => !window.IsBusy);
        ListBox commits = Find<ListBox>(window, "CommitList");
        WaitUntil(() => commits.Items.Cast<CommitListItem>().Any(item => item.Row.Hash == ArtificialCommits.WorkTreeHash));
        commits.SelectedItem = commits.Items.Cast<CommitListItem>().Single(item => item.Row.Hash == ArtificialCommits.WorkTreeHash);
        ListBox files = Find<ListBox>(window, "CommitFilesList");
        WaitUntil(() => Paths(files).OrderBy(path => path, StringComparer.Ordinal).SequenceEqual(["new.txt", "z.md"]));

        MenuItem showUntracked = Find<MenuItem>(window, "DiffFilesShowUntrackedMenuItem");
        showUntracked.IsChecked.Should().BeTrue("git's status.showuntrackedfiles is not \"no\"");
        showUntracked.IsChecked = false;
        showUntracked.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

        Paths(files).Should().Equal("z.md");
        window.Close();
    }

    [AvaloniaTest]
    public void The_commit_pickers_button_picks_a_commit_in_the_choose_commit_window()
    {
        Window owner = new();
        owner.Show();
        CreateBranchWindow create = new(_repo.Path, new RepositoryOperationsViewModel(new GitOperations()),
            TestAppBuilder.Preferences);
        _ = create.ShowDialog<bool>(owner);
        WaitUntil(() => create.BranchLoaded.IsCompleted);
        CommitPickerView picker = Find<CommitPickerView>(create, "CommitPicker");

        picker.GetLogicalDescendants().OfType<Button>().Single(button => button.Name == "buttonPickCommit")
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        WaitUntil(() => create.OwnedWindows.OfType<ChooseCommitWindow>().SingleOrDefault()?.CommitsLoaded.IsCompleted == true);
        ChooseCommitWindow choose = create.OwnedWindows.OfType<ChooseCommitWindow>().Single();
        ListBox grid = Find<ListBox>(choose, "revisionGrid");
        grid.SelectedItem = grid.Items.Cast<CommitListItem>().Single(item => item.Row.Subject == "first");
        Find<Button>(choose, "btnOK").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        string first = _repo.Run("rev-parse", "HEAD~3").Trim();
        WaitUntil(() => picker.Selected?.ToString() == first);
        owner.Close();
    }

    private static IEnumerable<string> Paths(ListBox files) => files.Items.Cast<CommitFile>().Select(file => file.Path);

    // The rows as shown: a folder with its state ("> " expanded, "+ " collapsed), a file with its text below its folder.
    private static List<string> Rows(ListBox files)
    {
        Dispatcher.UIThread.RunJobs();
        List<ListBoxItem> containers = [.. files.GetVisualDescendants().OfType<ListBoxItem>()];
        return
        [
            .. files.Items.Cast<object>().Select(item => item switch
            {
                FileTreeRow row => (row.IsExpanded ? "> " : "+ ") + row.Text,
                _ => containers.Single(container => container.DataContext == item) is { } container
                    ? FileRows.GetText(container) ?? ""
                    : "",
            })
        ];
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
        }
    }
}

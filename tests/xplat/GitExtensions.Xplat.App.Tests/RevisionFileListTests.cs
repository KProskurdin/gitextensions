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

        UiClicks.Raise(Find<ToolToggleButton>(window, "DiffFilesByStatusButton"), Button.ClickEvent);
        Paths(files).Should().Equal(["z.md", "b.txt"], "a modified file comes before an added one");

        FileFilterBox filter = Find<FileFilterBox>(window, "DiffFilesFilterBox");
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
        filter.Error.Should().NotBeNull();
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
        ClickRow(window, files, folder, x: 20);
        Rows(files).Should().Equal(["> src", "a.cs", "b.cs"], "a click on the node selects it, as upstream's tree");
        files.SelectedItem.Should().Be(folder);

        ClickRow(window, files, folder, x: 4);
        Rows(files).Should().Equal(["+ src"], "a click on the glyph collapses the folder");
        files.SelectedItem.Should().BeOfType<FileTreeRow>("the folder stays selected");

        RowContainer(files, files.Items.OfType<FileTreeRow>().Single())
            .RaiseEvent(new TappedEventArgs(InputElement.DoubleTappedEvent, null!));
        Dispatcher.UIThread.RunJobs();
        Rows(files).Should().Equal(["> src", "a.cs", "b.cs"], "a double-click expands it again");

        UiClicks.Raise(Find<ToolSplitButton>(window, "DiffFilesAsTreeButton"), SplitButton.ClickEvent);
        Dispatcher.UIThread.RunJobs();

        TestAppBuilder.Preferences.DiffListSorting.Should().Be(DiffListSortType.FilePathFlat);
        Find<ToolSplitButton>(window, "DiffFilesAsTreeButton").Image.Should().Be("DocumentTree");
        Rows(files).Should().Equal("src/a.cs", "src/b.cs");
        window.Close();
    }

    [AvaloniaTest]
    public void A_selected_folder_stands_for_its_files_in_the_menu_as_upstreams_selected_folder()
    {
        TestAppBuilder.Preferences.DiffListSorting = DiffListSortType.FilePath;
        Directory.CreateDirectory(Path.Combine(_repo.Path, "src", "sub"));
        File.WriteAllText(Path.Combine(_repo.Path, "src", "a.cs"), "a");
        File.WriteAllText(Path.Combine(_repo.Path, "src", "sub", "b.cs"), "b");
        File.WriteAllText(Path.Combine(_repo.Path, "src", "sub", "c.cs"), "c");
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
        WaitUntil(() => files.ItemCount == 5);
        files.SelectedItem = files.Items.OfType<FileTreeRow>().First(row => row.Folder == "src");
        DiffView viewer = Find<DiffView>(window, "CommitDiff");
        WaitUntil(() => viewer.Lines.Any(line => line.Text == "(3) src/"));
        viewer.Lines.Select(line => line.Text).Should().Contain(["a.cs", "sub/b.cs", "sub/c.cs"],
            "upstream shows the files below a selected folder");
        OpenMenu(files);
        MenuItem Item(string name) => Find<MenuItem>(files.ContextMenu!, name);
        Item("_selectAll").IsVisible.Should().BeTrue();
        Item("_collapseAll").IsVisible.Should().BeTrue();
        Item("_collapseRootFolders").IsVisible.Should().BeFalse("upstream offers it in the File tree tab only");
        Item("tsmiMove").IsVisible.Should().BeTrue("upstream renames a selected folder");
        Item("tsmiFileHistory").IsEnabled.Should().BeTrue("upstream shows a folder's history");
        Item("tsmiBlame").IsEnabled.Should().BeFalse();

        UiClicks.Raise(Item("_collapseAll"), MenuItem.ClickEvent);
        Dispatcher.UIThread.RunJobs();
        Rows(files).Should().Equal(["+ src"]);
        files.SelectedItem.Should().BeOfType<FileTreeRow>().Which.Folder.Should().Be("src");

        UiClicks.Raise(Item("_selectAll"), MenuItem.ClickEvent);
        Dispatcher.UIThread.RunJobs();
        Rows(files).Should().Equal("> src", "> sub", "b.cs", "c.cs", "a.cs");
        files.SelectedItems!.OfType<CommitFile>().Select(file => file.Path).Should()
            .BeEquivalentTo("src/a.cs", "src/sub/b.cs", "src/sub/c.cs");

        files.SelectedItem = files.Items.OfType<FileTreeRow>().First(row => row.Folder == "src");
        OpenMenu(files);
        UiClicks.Raise(Item("tsmiMove"), MenuItem.ClickEvent);
        WaitUntil(() => window.OwnedWindows.OfType<PromptWindow>().Any());
        PromptWindow prompt = window.OwnedWindows.OfType<PromptWindow>().Single();
        TextBox name = Find<TextBox>(prompt, "ValueBox");
        name.Text.Should().Be("src");
        name.Text = "lib";
        UiClicks.Raise(Find<Button>(prompt, "OkButton"), Button.ClickEvent);
        WaitUntil(() => _repo.ReadFile("lib/sub/b.cs") == "b" && _repo.ReadFile("src/a.cs") is null);
        window.Close();
    }

    [AvaloniaTest]
    public void The_filter_is_upstreams_combo_box_with_its_samples_the_filters_that_matched_and_a_delete_button()
    {
        TestAppBuilder.Preferences.DiffListSorting = DiffListSortType.FilePathFlat;
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        window.Show();
        window.OpenRepository(_repo.Path);
        WaitUntil(() => !window.IsBusy);
        ListBox commits = Find<ListBox>(window, "CommitList");
        commits.SelectedItem = commits.Items.Cast<CommitListItem>().Single(item => item.Row.Subject == "change z add b");
        ListBox files = Find<ListBox>(window, "CommitFilesList");
        WaitUntil(() => files.ItemCount == 2);
        FileFilterBox filter = Find<FileFilterBox>(window, "DiffFilesFilterBox");
        ComboBox box = filter.GetLogicalDescendants().OfType<ComboBox>().Single();
        Button delete = filter.GetLogicalDescendants().OfType<Button>().Single();
        box.Items.Cast<string>().Should().Equal("^(?!.*NotThisWord)", @"^(?!.*\bg?tests?/)");
        delete.IsVisible.Should().BeFalse();

        box.Text = @"\.md$";

        Paths(files).Should().HaveCount(2, "upstream filters 250 ms after the typing stops");
        WaitUntil(() => Paths(files).SequenceEqual(["z.md"]));
        delete.IsVisible.Should().BeTrue();
        box.Background.Should().NotBeNull("upstream colors a box that filters");
        box.Items.Cast<string>().First().Should().Be(@"\.md$", "a filter that matched files goes first");

        filter.Text = "nothing";
        box.Items.Cast<string>().Should().NotContain("nothing", "a filter that matched nothing is not kept");

        box.Text = Path.Combine(_repo.Path, "b.txt");
        WaitUntil(() => Paths(files).SequenceEqual(["b.txt"]));
        box.Text.Should().Be("b.txt", "a full path in the working directory is cut to the path in it");

        UiClicks.Raise(delete, Button.ClickEvent);
        filter.Text.Should().BeEmpty();
        Paths(files).Should().HaveCount(2);
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
        UiClicks.Raise(showUntracked, MenuItem.ClickEvent);

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
        UiClicks.Raise(Find<Button>(choose, "btnOK"), Button.ClickEvent);

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

    private static ListBoxItem RowContainer(ListBox files, object row)
    {
        Dispatcher.UIThread.RunJobs();
        return files.GetVisualDescendants().OfType<ListBoxItem>().Single(item => item.DataContext == row);
    }

    // A left click at x on the row's container, in window coordinates.
    private static void ClickRow(Window window, ListBox files, object row, double x)
    {
        ListBoxItem container = RowContainer(files, row);
        Point point = container.TranslatePoint(new Point(x, container.Bounds.Height / 2), window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    // The menu updates itself for the selection when it opens, as upstream's ItemContextMenu_Opening.
    private static void OpenMenu(ListBox list)
    {
        list.RaiseEvent(new ContextRequestedEventArgs());
        Dispatcher.UIThread.RunJobs();
        list.ContextMenu!.Close();
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

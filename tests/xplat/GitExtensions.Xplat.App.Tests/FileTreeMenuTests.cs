using Avalonia.Controls;
using Avalonia.Headless;
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
///  The File tree tab's item menu: upstream's FileStatusList menu in file tree mode (<see cref="FileStatusListMenu"/>), with
///  the tree items and Blame in place of the file's content.
/// </summary>
internal sealed class FileTreeMenuTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private TestRepository _repo = null!;

    [SetUp]
    public void SetUp()
    {
        _repo = new TestRepository();
        Directory.CreateDirectory(Path.Combine(_repo.Path, "docs", "sub"));
        File.WriteAllText(Path.Combine(_repo.Path, "docs", "guide.md"), "g1\ng2\n");
        File.WriteAllText(Path.Combine(_repo.Path, "docs", "sub", "deep.md"), "d");
        File.WriteAllText(Path.Combine(_repo.Path, "notes.txt"), "n");
        _repo.Run("add", "-A");
        _repo.Run("commit", "-q", "-m", "add files");
    }

    [TearDown]
    public void TearDown() => _repo.Dispose();

    [AvaloniaTest]
    public void A_file_in_the_file_tree_gets_upstreams_menu_in_file_tree_mode()
    {
        MainWindow window = OpenWindow();
        TreeView tree = ShowFileTree(window);
        tree.SelectedItem = Node(tree, "notes.txt");

        OpenMenu(tree);

        Item(tree, "tsmiShowInFileTree").IsVisible.Should().BeFalse("the tab is the file tree");
        Item(tree, "tsmiFindFile").IsVisible.Should().BeTrue();
        Item(tree, "tsmiFileHistory").IsEnabled.Should().BeTrue();
        Item(tree, "tsmiOpenRevisionFile").IsVisible.Should().BeTrue();
        Item(tree, "tsmiDeleteFile").IsVisible.Should().BeFalse();
        Item(tree, "tsmiStageFile").IsVisible.Should().BeFalse();
        Item(tree, "tsmiResetFileToParent").IsVisible.Should().BeFalse("the file tree has no first revision");
        Item(tree, "tsmiMove").IsVisible.Should().BeTrue();
        MenuItem blame = Item(tree, "tsmiBlame");
        blame.IsEnabled.Should().BeTrue();
        blame.ToggleType.Should().Be(MenuItemToggleType.CheckBox);
        blame.IsChecked.Should().BeFalse();
        Item(tree, "_collapseAll").IsVisible.Should().BeFalse("a file has no nodes below it");
        window.Close();
    }

    [AvaloniaTest]
    public void Blame_shows_the_selected_files_blame_in_place_of_its_content_until_clicked_again()
    {
        MainWindow window = OpenWindow();
        TreeView tree = ShowFileTree(window);
        tree.SelectedItem = Node(tree, "docs/guide.md");
        BlameView blame = Find<BlameView>(window, "FileTreeBlame");
        DiffView content = Find<DiffView>(window, "FileContentView");
        OpenMenu(tree);

        Click(Item(tree, "tsmiBlame"));

        WaitUntil(() => blame.IsVisible && blame.Lines.Count == 2);
        content.IsVisible.Should().BeFalse();
        OpenMenu(tree);
        Item(tree, "tsmiBlame").IsChecked.Should().BeTrue();

        tree.SelectedItem = Node(tree, "notes.txt");
        WaitUntil(() => blame.Lines.Count == 1);
        blame.IsVisible.Should().BeTrue("upstream keeps Blame checked in the file tree");

        OpenMenu(tree);
        Click(Item(tree, "tsmiBlame"));
        WaitUntil(() => content.IsVisible && content.Lines.Count == 1);
        blame.IsVisible.Should().BeFalse();
        window.Close();
    }

    [AvaloniaTest]
    public void A_folder_gets_upstreams_tree_items_and_select_all_selects_the_files_below_it()
    {
        MainWindow window = OpenWindow();
        TreeView tree = ShowFileTree(window);
        FileTreeNode docs = Node(tree, "docs");
        tree.SelectedItem = docs;
        DiffView content = Find<DiffView>(window, "FileContentView");
        WaitUntil(() => content.Lines.Any(line => line.Text == "(2) docs/"));
        content.Lines.Select(line => line.Text).Should().Contain(["guide.md", "sub/deep.md"],
            "upstream shows the files below a selected folder");
        OpenMenu(tree);
        Item(tree, "_selectAll").IsVisible.Should().BeTrue();
        Item(tree, "_expandAll").IsVisible.Should().BeTrue();
        Item(tree, "tsmiMove").IsVisible.Should().BeTrue("upstream renames a selected folder");
        Item(tree, "tsmiFileHistory").IsEnabled.Should().BeTrue("upstream shows a folder's history");
        Item(tree, "tsmiBlame").IsEnabled.Should().BeFalse();

        Click(Item(tree, "_selectAll"));

        tree.SelectedItems!.OfType<FileTreeNode>().Select(node => node.Path).Should()
            .BeEquivalentTo("docs/guide.md", "docs/sub/deep.md");
        docs.IsExpanded.Should().BeTrue();
        Node(tree, "docs/sub").IsExpanded.Should().BeTrue();
        OpenMenu(tree);
        Item(tree, "_collapseRootFolders").IsVisible.Should().BeTrue("a root folder is expanded");

        Click(Item(tree, "_collapseRootFolders"));

        docs.IsExpanded.Should().BeFalse();
        Node(tree, "docs/sub").IsExpanded.Should().BeTrue("upstream collapses only the root folders");
        tree.SelectedItem.Should().Be(docs);
        window.Close();
    }

    [AvaloniaTest]
    public void F2_in_the_file_tree_renames_the_selected_folder_with_git()
    {
        MainWindow window = OpenWindow();
        TreeView tree = ShowFileTree(window);
        FileTreeNode docs = Node(tree, "docs");
        tree.SelectedItem = docs;
        WaitUntil(() => tree.TreeContainerFromItem(docs) is not null);
        tree.TreeContainerFromItem(docs)!.Focus();

        window.KeyPress(Key.F2, RawInputModifiers.None, PhysicalKey.F2, keySymbol: null);

        WaitUntil(() => window.OwnedWindows.OfType<PromptWindow>().Any());
        PromptWindow prompt = window.OwnedWindows.OfType<PromptWindow>().Single();
        TextBox name = Find<TextBox>(prompt, "ValueBox");
        name.Text.Should().Be("docs");
        name.Text = "manual";
        Click(Find<Button>(prompt, "OkButton"));
        WaitUntil(() => _repo.ReadFile("manual/sub/deep.md") == "d" && _repo.ReadFile("docs/guide.md") is null);
        _repo.Run("status", "--porcelain").Should().Contain("R  docs/guide.md -> manual/guide.md");
        window.Close();
    }

    [AvaloniaTest]
    public void Blame_in_the_diff_tab_shows_the_file_as_blame_in_the_file_tree()
    {
        MainWindow window = OpenWindow();
        TabControl tabs = Find<TabControl>(window, "DetailTabs");
        tabs.SelectedItem = Find<TabItem>(window, "DiffTab");
        ListBox files = Find<ListBox>(window, "CommitFilesList");
        WaitUntil(() => files.Items.OfType<CommitFile>().Any(file => file.Path == "docs/guide.md"));
        files.SelectedItem = files.Items.OfType<CommitFile>().First(file => file.Path == "docs/guide.md");
        Dispatcher.UIThread.RunJobs();
        OpenMenu(files);

        Click(Find<MenuItem>(files.ContextMenu!, "tsmiBlame"));

        TreeView tree = Find<TreeView>(window, "FileTreeView");
        BlameView blame = Find<BlameView>(window, "FileTreeBlame");
        WaitUntil(() => tree.SelectedItem is FileTreeNode { Path: "docs/guide.md" } && blame.Lines.Count == 2);
        tabs.SelectedItem.Should().Be(Find<TabItem>(window, "FileTreeTab"));
        blame.IsVisible.Should().BeTrue();
        window.OwnedWindows.OfType<FileHistoryWindow>().Should().BeEmpty("upstream blames in the File tree tab");
        window.Close();
    }

    [AvaloniaTest]
    public void The_filter_box_shows_the_files_whose_paths_match_as_upstreams_file_tree_mode()
    {
        MainWindow window = OpenWindow();
        TreeView tree = ShowFileTree(window);
        tree.SelectedItem = Node(tree, "docs/sub/deep.md");
        FileFilterBox filter = Find<FileFilterBox>(window, "FileTreeFilterBox");

        filter.Text = "DEEP";

        WaitUntil(() => Paths(tree).SequenceEqual(["docs/sub/deep.md"]));
        Node(tree, "docs/sub").IsExpanded.Should().BeTrue("upstream expands a filtered list of few files");
        tree.SelectedItem.Should().BeOfType<FileTreeNode>().Which.Path.Should().Be("docs/sub/deep.md",
            "the selected file stays selected");

        filter.Text = "(";

        WaitUntil(() => Paths(tree).Contains("notes.txt") && Paths(tree).Contains("docs/guide.md"));
        filter.Error.Should().NotBeNull("an invalid expression shows its error and filters nothing");
        window.Close();

        static IEnumerable<string> Paths(TreeView tree)
            => ((IReadOnlyList<FileTreeNode>)tree.ItemsSource!).SelectMany(root => root.Files()).Select(file => file.Path);
    }

    [AvaloniaTest]
    public void Ctrl_A_in_the_file_tree_selects_every_file_and_leaves_the_folders_collapsed()
    {
        MainWindow window = OpenWindow();
        TreeView tree = ShowFileTree(window);
        FileTreeNode notes = Node(tree, "notes.txt");
        tree.SelectedItem = notes;
        WaitUntil(() => tree.TreeContainerFromItem(notes) is not null);
        tree.TreeContainerFromItem(notes)!.Focus();

        window.KeyPress(Key.A, TestKeys.Command, PhysicalKey.A, keySymbol: null);

        List<FileTreeNode> files = [.. ((IReadOnlyList<FileTreeNode>)tree.ItemsSource!).SelectMany(root => root.Descendants())
            .Where(node => !node.IsFolder)];
        files.Select(file => file.Path).Should().Contain(["docs/guide.md", "docs/sub/deep.md", "notes.txt"]);
        tree.SelectedItems!.Cast<FileTreeNode>().Should().BeEquivalentTo(files, "upstream's FirstGroupItems in file tree mode");
        Node(tree, "docs").IsExpanded.Should().BeFalse("upstream selects the files without expanding their folders");
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

    private static TreeView ShowFileTree(MainWindow window)
    {
        Find<TabControl>(window, "DetailTabs").SelectedItem = Find<TabItem>(window, "FileTreeTab");
        TreeView tree = Find<TreeView>(window, "FileTreeView");
        WaitUntil(() => tree.ItemsSource is IReadOnlyList<FileTreeNode> { Count: > 0 });
        return tree;
    }

    private static FileTreeNode Node(TreeView tree, string path)
        => ((IReadOnlyList<FileTreeNode>)tree.ItemsSource!).SelectMany(root => root.Descendants())
            .First(node => node.Path == path);

    private static MenuItem Item(Control list, string name) => Find<MenuItem>(list.ContextMenu!, name);

    // The menu updates itself for the selection when it opens, as upstream's ItemContextMenu_Opening.
    private static void OpenMenu(Control list)
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

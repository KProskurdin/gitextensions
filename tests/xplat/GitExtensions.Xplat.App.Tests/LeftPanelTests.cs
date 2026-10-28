using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using GitCommands;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

internal sealed class LeftPanelTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(20);

    private TestRepository _repo = null!;

    [SetUp]
    public void Setup()
    {
        _repo = new TestRepository();
        Reset();
    }

    [TearDown]
    public void TearDown()
    {
        Reset();
        _repo.Dispose();
    }

    [AvaloniaTest]
    public void The_tree_shows_upstreams_trees_and_a_toolstrip_toggle_hides_one()
    {
        MainWindow window = OpenWindow();
        TreeView tree = Find<TreeView>(window, "BranchTreeView");

        Roots(tree).Select(root => root.Text).Should()
            .Equal("Branches", "Remotes", "Worktrees", "Tags", "Submodules", "Stashes");
        Find<ToolToggleButton>(window, "ShowTagsTreeButton").IsChecked.Should().BeTrue();

        Click(window, "ShowTagsTreeButton");

        Roots(tree).Select(root => root.Kind).Should().NotContain(RepoObjectKind.TagsRoot);
        Find<ToolToggleButton>(window, "ShowTagsTreeButton").IsChecked.Should().BeFalse();
        TestAppBuilder.Preferences.ShowsRepoObjectsTree(RepoObjectKind.TagsRoot).Should().BeFalse();

        Click(window, "ShowTagsTreeButton");

        Roots(tree).Select(root => root.Kind).Should().Contain(RepoObjectKind.TagsRoot);
    }

    [AvaloniaTest]
    public void The_menu_of_the_checked_out_branch_offers_only_to_branch_from_it_and_rename_it()
    {
        MainWindow window = OpenWindow();
        TreeView tree = Find<TreeView>(window, "BranchTreeView");
        RepoObjectNode current = Roots(tree)[0].Children.Single(node => node.Kind == RepoObjectKind.LocalBranch);
        current.IsBold.Should().BeTrue();
        tree.SelectedItem = current;

        OpenMenu(window);

        MenuItem checkout = Find<MenuItem>(window, "TreeCheckoutMenuItem");
        checkout.IsVisible.Should().BeTrue();
        checkout.IsEnabled.Should().BeFalse();
        checkout.Header.Should().Be("Chec_kout branch...");
        Find<MenuItem>(window, "TreeCreateBranchMenuItem").IsEnabled.Should().BeTrue();
        Find<MenuItem>(window, "TreeRenameBranchMenuItem").IsEnabled.Should().BeTrue();
        Find<MenuItem>(window, "TreeFetchAndCheckoutMenuItem").IsVisible.Should().BeFalse();
        Find<MenuItem>(window, "TreeOpenStashMenuItem").IsVisible.Should().BeFalse();
        Find<MenuItem>(window, "SortByMenuItem").IsVisible.Should().BeTrue();

        // A tree root offers what upstream offers on it: here the stash actions and moving the tree.
        tree.SelectedItem = Roots(tree).Single(root => root.Kind == RepoObjectKind.StashesRoot);
        OpenMenu(window);

        Find<MenuItem>(window, "TreeStashAllMenuItem").IsVisible.Should().BeTrue();
        Find<MenuItem>(window, "TreeCheckoutMenuItem").IsVisible.Should().BeFalse();
        Find<MenuItem>(window, "TreeMoveUpMenuItem").IsEnabled.Should().BeTrue();
        Find<MenuItem>(window, "TreeMoveDownMenuItem").IsEnabled.Should().BeFalse();
    }

    [AvaloniaTest]
    public void Filter_for_selected_shows_only_that_branch_in_the_grid()
    {
        string head = _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim();
        _repo.Run("checkout", "-q", "-b", "side");
        File.WriteAllText(Path.Combine(_repo.Path, "s.txt"), "x");
        _repo.Run("add", "s.txt");
        _repo.Run("commit", "-q", "-m", "side work");
        _repo.Run("checkout", "-q", head);
        MainWindow window = OpenWindow();
        TreeView tree = Find<TreeView>(window, "BranchTreeView");
        ListBox commits = Find<ListBox>(window, "CommitList");
        WaitUntil(() => commits.ItemCount == 3);
        tree.SelectedItem = BranchNode(tree, head);

        Click(window, "TreeFilterMenuItem");

        WaitUntil(() => commits.ItemCount == 2);
        Find<ToolSplitButton>(window, "ShowBranchesButton").Text.Should().Be("Filtered branches");
        Find<AutoCompleteBox>(window, "BranchFilterBox").Text.Should().Be(head);
    }

    [AvaloniaTest]
    public void Delete_asks_before_deleting_a_branch_that_is_not_merged()
    {
        string head = _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim();
        _repo.Run("checkout", "-q", "-b", "unmerged");
        File.WriteAllText(Path.Combine(_repo.Path, "u.txt"), "x");
        _repo.Run("add", "u.txt");
        _repo.Run("commit", "-q", "-m", "unmerged work");
        _repo.Run("checkout", "-q", head);
        MainWindow window = OpenWindow();
        TreeView tree = Find<TreeView>(window, "BranchTreeView");
        tree.SelectedItem = BranchNode(tree, "unmerged");

        Click(window, "TreeDeleteMenuItem");
        WaitUntil(() => window.OwnedWindows.OfType<DeleteBranchWindow>().Any());
        DeleteBranchWindow delete = window.OwnedWindows.OfType<DeleteBranchWindow>().Single();
        Find<ComboBox>(delete, "Branches").Text.Should().Be("unmerged", "upstream's window opens with the branch");
        Click(delete, "Delete");

        WaitUntil(() => delete.OwnedWindows.OfType<ConfirmWindow>().Any());
        ConfirmWindow question = delete.OwnedWindows.OfType<ConfirmWindow>().Single();
        question.Title.Should().Be("Delete Confirmation");
        Click(question, "ConfirmButton");
        WaitUntil(() => BranchNode(tree, "unmerged") is null);
        _repo.Run("branch", "--list", "unmerged").Trim().Should().BeEmpty();
    }

    [AvaloniaTest]
    public void Move_down_puts_the_tree_below_the_next_one_and_keeps_the_order()
    {
        MainWindow window = OpenWindow();
        TreeView tree = Find<TreeView>(window, "BranchTreeView");
        tree.SelectedItem = Roots(tree)[0];

        Click(window, "TreeMoveDownMenuItem");

        Roots(tree).Select(root => root.Kind).Take(2).Should()
            .Equal(RepoObjectKind.RemotesRoot, RepoObjectKind.BranchesRoot);
        TestAppBuilder.Preferences.RepoObjectsTreeOrder.Take(2).Should()
            .Equal(RepoObjectKind.RemotesRoot, RepoObjectKind.BranchesRoot);
    }

    [AvaloniaTest]
    public void Search_selects_the_next_node_that_matches_and_shows_it()
    {
        _repo.Run("branch", "team/found");
        MainWindow window = OpenWindow();
        TreeView tree = Find<TreeView>(window, "BranchTreeView");
        RepoObjectNode folder = Roots(tree)[0].Children.Single(node => node.Kind == RepoObjectKind.Folder);
        folder.IsExpanded = false;

        Find<AutoCompleteBox>(window, "LeftPanelSearchBox").Text = "FOUND";
        Click(window, "LeftPanelSearchButton");

        WaitUntil(() => tree.SelectedItem is RepoObjectNode { Branch.Name: "team/found" });
        folder.IsExpanded.Should().BeTrue();
    }

    [AvaloniaTest]
    public void Deactivate_moves_a_remote_to_the_inactive_folder_and_activate_brings_it_back()
    {
        _repo.Run("remote", "add", "origin", "/srv/origin.git");
        MainWindow window = OpenWindow();
        TreeView tree = Find<TreeView>(window, "BranchTreeView");
        tree.SelectedItem = RemoteNode(tree, "origin");
        OpenMenu(window);

        Find<MenuItem>(window, "TreeActivateRemoteMenuItem").IsVisible.Should().BeFalse();
        Find<MenuItem>(window, "TreeFetchRemoteMenuItem").IsVisible.Should().BeTrue();
        Click(window, "TreeDeactivateRemoteMenuItem");

        WaitUntil(() => RemoteNode(tree, "origin") is { IsInactive: true });
        Roots(tree)[1].Children.Single().Kind.Should().Be(RepoObjectKind.InactiveRemotes);
        _repo.Run("remote").Trim().Should().BeEmpty();
        tree.SelectedItem = RemoteNode(tree, "origin");
        OpenMenu(window);
        Find<MenuItem>(window, "TreeFetchRemoteMenuItem").IsVisible.Should().BeFalse();
        Find<MenuItem>(window, "TreeDeactivateRemoteMenuItem").IsVisible.Should().BeFalse();

        Click(window, "TreeActivateRemoteMenuItem");

        WaitUntil(() => RemoteNode(tree, "origin") is { IsInactive: false });
        Roots(tree)[1].Children.Should().NotContain(node => node.Kind == RepoObjectKind.InactiveRemotes);
        _repo.Run("remote").Trim().Should().Be("origin");
    }

    [AvaloniaTest]
    public void Open_stash_opens_the_stash_window_on_that_stash()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "older");
        _repo.Run("stash", "push", "-q", "-m", "older");
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "newer");
        _repo.Run("stash", "push", "-q", "-m", "newer");
        MainWindow window = OpenWindow();
        TreeView tree = Find<TreeView>(window, "BranchTreeView");
        tree.SelectedItem = Roots(tree).SelectMany(root => root.Descendants())
            .Single(node => node.Item is StashInfo { Name: "stash@{1}" });

        Click(window, "TreeOpenStashMenuItem");

        WaitUntil(() => window.OwnedWindows.OfType<StashWindow>().SingleOrDefault()?.StashesLoaded.IsCompleted == true);
        StashWindow stash = window.OwnedWindows.OfType<StashWindow>().Single();
        stash.Dialog.Selected!.Name.Should().Be("stash@{1}");
        stash.Close();
    }

    private static void Reset()
    {
        foreach (RepoObjectKind root in RepoObjectsTree.Roots)
        {
            TestAppBuilder.Preferences.ShowRepoObjectsTree(root, true);
        }

        TestAppBuilder.Preferences.RepoObjectsTreeOrder = RepoObjectsTree.Roots;
        TestAppBuilder.Preferences.ShowCurrentBranchOnly = false;
        TestAppBuilder.Preferences.ResetConfirmations();
    }

    private static IReadOnlyList<RepoObjectNode> Roots(TreeView tree)
        => tree.ItemsSource as IReadOnlyList<RepoObjectNode> ?? [];

    private static RepoObjectNode? BranchNode(TreeView tree, string name)
        => Roots(tree).SelectMany(root => root.Descendants()).FirstOrDefault(node => node.Branch?.Name == name);

    private static RepoObjectNode? RemoteNode(TreeView tree, string name)
        => Roots(tree).SelectMany(root => root.Descendants())
            .FirstOrDefault(node => node.Kind == RepoObjectKind.Remote && node.Name == name);

    // The menu fills itself for the selected node when it opens, as upstream's contextMenu_Opening.
    private static void OpenMenu(Window window)
    {
        Find<TreeView>(window, "BranchTreeView").RaiseEvent(new ContextRequestedEventArgs());
        Dispatcher.UIThread.RunJobs();
        Find<ContextMenu>(window, "BranchTreeContextMenu").Close();
    }

    private static MainWindow NewWindow() =>
        new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));

    private MainWindow OpenWindow()
    {
        MainWindow window = NewWindow();
        window.Show();
        window.OpenRepository(_repo.Path);
        WaitUntil(() => !window.IsBusy && Roots(window.FindControl<TreeView>("BranchTreeView")!).Count > 0);
        return window;
    }

    private static void Click(Window window, string name)
    {
        Control control = Find<Control>(window, name);
        RoutedEvent click = control is MenuItem ? MenuItem.ClickEvent : Button.ClickEvent;
        control.RaiseEvent(new RoutedEventArgs(click));
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

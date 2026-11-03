using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using GitCommands;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Scripts;
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
        BranchNode(tree, "team/found")!.IsSearchMatch.Should().BeTrue("upstream highlights every match");

        Click(window, "RefreshButton");
        WaitUntil(() => !window.IsBusy && BranchNode(tree, "team/found") is { IsSearchMatch: true });
        Find<AutoCompleteBox>(window, "LeftPanelSearchBox").Text = "";
        Click(window, "LeftPanelSearchButton");

        WaitUntil(() => BranchNode(tree, "team/found") is { IsSearchMatch: false });
        window.Close();
    }

    [AvaloniaTest]
    public void F3_searches_from_the_search_box_where_Delete_edits_the_text_and_F2_renames_the_branch()
    {
        _repo.Run("branch", "team/found");
        MainWindow window = OpenWindow();
        TreeView tree = Find<TreeView>(window, "BranchTreeView");
        AutoCompleteBox box = Find<AutoCompleteBox>(window, "LeftPanelSearchBox");
        box.Text = "found";
        box.Focus();

        window.KeyPress(Key.F3, RawInputModifiers.None, PhysicalKey.F3, keySymbol: null);

        WaitUntil(() => tree.SelectedItem is RepoObjectNode { Branch.Name: "team/found" });
        box.Focus();
        window.KeyPress(Key.Delete, RawInputModifiers.None, PhysicalKey.Delete, keySymbol: null);
        Dispatcher.UIThread.RunJobs();
        window.OwnedWindows.Should().BeEmpty("Delete edits the search text, as upstream's IsTextEditKey keeps it from the tree");

        tree.Focus();
        window.KeyPress(Key.F2, RawInputModifiers.None, PhysicalKey.F2, keySymbol: null);

        WaitUntil(() => window.OwnedWindows.OfType<RenameBranchWindow>().Any());
        window.OwnedWindows.OfType<RenameBranchWindow>().Single().Close();
        window.Close();
    }

    [AvaloniaTest]
    public void Ctrl_Space_marks_several_branches_whose_menu_filters_the_grid_for_all_of_them()
    {
        _repo.Run("branch", "older", "HEAD~1");
        _repo.Run("tag", "v1", "HEAD~1");
        MainWindow window = OpenWindow();
        TreeView tree = Find<TreeView>(window, "BranchTreeView");
        tree.SelectedItem = BranchNode(tree, "older");
        FocusItem(tree, BranchNode(tree, "older")!);
        window.KeyPress(Key.Space, RawInputModifiers.Control, PhysicalKey.Space, keySymbol: null);
        tree.SelectedItem = TagNode(tree, "v1");
        FocusItem(tree, TagNode(tree, "v1")!);
        window.KeyPress(Key.Space, RawInputModifiers.Control, PhysicalKey.Space, keySymbol: null);

        BranchNode(tree, "older")!.IsMultiSelected.Should().BeTrue();
        TagNode(tree, "v1")!.IsMultiSelected.Should().BeTrue();
        OpenMenu(window);

        Find<MenuItem>(window, "TreeFilterMenuItem").IsVisible.Should().BeTrue();
        Find<MenuItem>(window, "TreeCheckoutMenuItem").IsVisible.Should().BeFalse("upstream offers it for a single node only");
        Find<MenuItem>(window, "TreeDeleteMenuItem").IsVisible.Should().BeFalse();
        Click(window, "TreeFilterMenuItem");

        WaitUntil(() => Find<AutoCompleteBox>(window, "BranchFilterBox").Text == "older v1");
        Click(window, "RefreshButton");
        WaitUntil(() => !window.IsBusy);
        BranchNode(tree, "older")!.IsMultiSelected.Should().BeTrue("upstream keeps the marks across a refresh");
        window.Close();
    }

    [AvaloniaTest]
    public void A_click_marks_the_node_alone_and_a_ctrl_click_adds_one()
    {
        _repo.Run("branch", "older", "HEAD~1");
        string current = _repo.Run("branch", "--show-current").Trim();
        MainWindow window = OpenWindow();
        TreeView tree = Find<TreeView>(window, "BranchTreeView");

        ClickItem(window, tree, BranchNode(tree, "older")!, RawInputModifiers.None);
        ClickItem(window, tree, BranchNode(tree, current)!, RawInputModifiers.Control);

        RepoObjectSelection.Selected(Roots(tree)).Select(node => node.Branch?.Name).Should().BeEquivalentTo("older", current);

        ClickItem(window, tree, BranchNode(tree, "older")!, RawInputModifiers.None);

        RepoObjectSelection.Selected(Roots(tree)).Select(node => node.Branch?.Name).Should().Equal("older");
        window.Close();
    }

    [AvaloniaTest]
    public void The_submodule_tree_shows_the_top_project_its_folders_and_submodules_with_upstreams_menu()
    {
        using TestRepository library = new();
        AddSubmodule(library, "ext/lib");
        MainWindow window = OpenWindow();
        TreeView tree = Find<TreeView>(window, "BranchTreeView");
        WaitUntil(() => SubmoduleTop(tree)?.Children.Count > 0);

        RepoObjectNode top = SubmoduleTop(tree)!;
        (top.Text.StartsWith(Path.GetFileName(_repo.Path), StringComparison.Ordinal), top.IsBold).Should().Be((true, true));
        RepoObjectNode folder = top.Children.Single();
        (folder.Kind, folder.Text, folder.IsItalic).Should().Be((RepoObjectKind.SubmoduleFolder, "ext", true));
        RepoObjectNode lib = folder.Children.Single();
        (lib.Kind, lib.Image, lib.IsBold).Should().Be((RepoObjectKind.Submodule, "FolderSubmodule", false));
        lib.Text.Should().StartWith("lib", "upstream shows the submodule's folder name, not its path");

        tree.SelectedItem = lib;
        OpenMenu(window);
        string[] items =
        [
            "TreeOpenSubmoduleMenuItem", "TreeOpenSubmoduleNewInstanceMenuItem", "TreeManageSubmodulesMenuItem",
            "TreeUpdateSubmoduleMenuItem", "TreeSynchronizeSubmoduleMenuItem", "TreeResetSubmoduleMenuItem",
            "TreeStashSubmoduleMenuItem", "TreeCommitSubmoduleMenuItem",
        ];
        items.Where(name => Find<MenuItem>(window, name).IsVisible).Should().Equal(
            "TreeOpenSubmoduleMenuItem", "TreeOpenSubmoduleNewInstanceMenuItem", "TreeUpdateSubmoduleMenuItem",
            "TreeResetSubmoduleMenuItem", "TreeStashSubmoduleMenuItem", "TreeCommitSubmoduleMenuItem");

        tree.SelectedItem = top;
        OpenMenu(window);
        items.Where(name => Find<MenuItem>(window, name).IsVisible).Should().Equal(
            ["TreeOpenSubmoduleNewInstanceMenuItem", "TreeManageSubmodulesMenuItem", "TreeUpdateSubmoduleMenuItem",
                "TreeSynchronizeSubmoduleMenuItem", "TreeResetSubmoduleMenuItem", "TreeStashSubmoduleMenuItem",
                "TreeCommitSubmoduleMenuItem"],
            "the shown project is not opened again, and only it is managed and synchronized");
        window.Close();
    }

    [AvaloniaTest]
    public void Stash_on_a_submodule_stashes_its_changes_and_the_window_stays_on_its_repository()
    {
        using TestRepository library = new();
        AddSubmodule(library, "lib");
        string libPath = Path.Combine(_repo.Path, "lib");
        File.WriteAllText(Path.Combine(libPath, "a.txt"), "changed");
        MainWindow window = OpenWindow();
        TreeView tree = Find<TreeView>(window, "BranchTreeView");
        WaitUntil(() => SubmoduleTop(tree)?.Children.Count > 0);
        tree.SelectedItem = SubmoduleTop(tree)!.Children.Single();
        OpenMenu(window);

        Click(window, "TreeStashSubmoduleMenuItem");

        WaitUntil(() => GitProcess.Run(libPath, "stash", "list").Length > 0);
        WaitUntil(() => !window.IsBusy);
        File.ReadAllText(Path.Combine(libPath, "a.txt")).Should().Be("two");
        _repo.Run("stash", "list").Should().BeEmpty("the superproject's changes are not stashed");
        Roots(tree).Single(root => root.Kind == RepoObjectKind.WorktreesRoot).Children.Single().Item
            .Should().BeOfType<WorktreeInfo>().Which.Path.Should().Match(path =>
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)) == _repo.Path, "the window did not open the submodule");
        window.Close();
    }

    [AvaloniaTest]
    public void With_upstreams_submodule_status_a_submodule_ahead_of_its_recorded_commit_shows_it()
    {
        TestAppBuilder.Preferences.ShowSubmoduleStatus = true;
        using TestRepository library = new();
        AddSubmodule(library, "lib");
        GitProcess.Run(Path.Combine(_repo.Path, "lib"), "-c", "user.name=Test", "-c", "user.email=test@example.com",
            "commit", "-q", "--allow-empty", "-m", "ahead");
        MainWindow window = OpenWindow();
        TreeView tree = Find<TreeView>(window, "BranchTreeView");
        WaitUntil(() => SubmoduleTop(tree)?.Children.FirstOrDefault()?.Image == "SubmoduleRevisionUp");

        RepoObjectNode top = SubmoduleTop(tree)!;
        RepoObjectNode lib = top.Children.Single();
        lib.Text.Should().EndWith(" (+1-0)", "upstream shows the commits added and removed");
        lib.ToolTip.Should().StartWith("Submodule lib").And.Contain("Fast Forward");
        top.Image.Should().Be("SubmoduleDirty", "upstream marks the projects above a changed submodule dirty");
        top.ToolTip.Should().Contain("1 changed submodule");
        window.Close();
    }

    [AvaloniaTest]
    public void In_a_submodule_the_toolbar_lists_the_superproject_and_its_button_goes_up_to_it()
    {
        using TestRepository library = new();
        AddSubmodule(library, "lib");
        string libPath = Path.Combine(_repo.Path, "lib");
        MainWindow window = NewWindow();
        window.Show();
        window.OpenRepository(libPath);
        TreeView tree = Find<TreeView>(window, "BranchTreeView");
        WaitUntil(() => !window.IsBusy && SubmoduleTop(tree)?.Children.Count > 0);
        ToolSplitButton button = Find<ToolSplitButton>(window, "SubmodulesButton");
        WaitUntil(() => button.Image == "NavigateUp");
        ToolTip.GetTip(button).Should().Be("Go to superproject");
        SubmoduleTop(tree)!.IsBold.Should().BeFalse("the tree starts at the top project");
        SubmoduleTop(tree)!.Children.Single().IsBold.Should().BeTrue();

        MenuFlyout flyout = (MenuFlyout)button.Flyout!;
        flyout.ShowAt(button);
        WaitUntil(() => flyout.Items.Count > 1);
        List<string?> texts = [.. flyout.Items.OfType<MenuItem>().Select(item => (item.Header as TextBlock)?.Text ?? item.Header as string)];
        flyout.Hide();
        texts.Should().HaveCount(5);
        texts[0].Should().Be("No submodules");
        texts[1].Should().StartWith("Superproject: " + Path.GetFileName(_repo.Path), "the superproject is the top project here");
        texts[2].Should().StartWith("lib");
        texts[3..].Should().Equal("_Update all submodules", "Update current submodule");

        button.RaiseEvent(new RoutedEventArgs(SplitButton.ClickEvent));

        WaitUntil(() => !window.IsBusy && button.Image == "SubmodulesManage" && SubmoduleTop(tree)?.IsBold == true);
        window.Close();
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

    [AvaloniaTest]
    public void Selecting_a_branch_or_a_tag_selects_its_commit_in_the_grid()
    {
        _repo.Run("branch", "older", "HEAD~1");
        _repo.Run("tag", "v1", "HEAD~1");
        string current = _repo.Run("branch", "--show-current").Trim();
        MainWindow window = OpenWindow();
        TreeView tree = Find<TreeView>(window, "BranchTreeView");
        ListBox commits = Find<ListBox>(window, "CommitList");

        tree.SelectedItem = BranchNode(tree, "older");
        WaitUntil(() => SelectedSubject(commits) == "first");

        tree.SelectedItem = BranchNode(tree, current);
        WaitUntil(() => SelectedSubject(commits) == "second");

        tree.SelectedItem = TagNode(tree, "v1");
        WaitUntil(() => SelectedSubject(commits) == "first");
        window.Close();
    }

    [AvaloniaTest]
    public void A_refresh_does_not_go_to_the_commit_of_the_node_the_tree_selects_again()
    {
        _repo.Run("branch", "older", "HEAD~1");
        MainWindow window = OpenWindow();
        TreeView tree = Find<TreeView>(window, "BranchTreeView");
        ListBox commits = Find<ListBox>(window, "CommitList");
        tree.SelectedItem = BranchNode(tree, "older");
        WaitUntil(() => SelectedSubject(commits) == "first");
        commits.SelectedItem = commits.Items.Cast<CommitListItem>().Single(item => item.Row.Subject == "second");

        Click(window, "RefreshButton");
        WaitUntil(() => !window.IsBusy);

        // Long enough for a navigation from the tree, which reads git, to have come through.
        DateTime settled = DateTime.UtcNow + TimeSpan.FromMilliseconds(500);
        WaitUntil(() => DateTime.UtcNow > settled);

        (tree.SelectedItem as RepoObjectNode)?.Branch?.Name.Should().Be("older");
        SelectedSubject(commits).Should().Be("second", "upstream ignores the tree's selection while it is filled again");
        window.Close();
    }

    [AvaloniaTest]
    public void A_branch_has_the_copy_submenu_and_a_local_branch_the_grids_scripts()
    {
        TestAppBuilder.Scripts.Save(
        [
            new ScriptDefinition { Name = "Grid script", Command = "git", AddToRevisionGridContextMenu = true },
        ]);
        _repo.Run("tag", "v1");
        string current = _repo.Run("branch", "--show-current").Trim();
        MainWindow window = OpenWindow();
        TreeView tree = Find<TreeView>(window, "BranchTreeView");
        ContextMenu menu = Find<ContextMenu>(window, "BranchTreeContextMenu");
        tree.SelectedItem = BranchNode(tree, current);
        WaitUntil(() => SelectedSubject(Find<ListBox>(window, "CommitList")) == "second");

        OpenMenu(window);

        MenuItem copy = Find<MenuItem>(window, "TreeCopyMenuItem");
        copy.IsVisible.Should().BeTrue();
        copy.Items.OfType<MenuItem>().Should().NotBeEmpty("it lists the selected commit's values, as the grid's");
        menu.Items.OfType<MenuItem>().Select(item => (item.Header as TextBlock)?.Text).Should().Contain("Grid script");

        tree.SelectedItem = TagNode(tree, "v1");
        OpenMenu(window);

        copy.IsVisible.Should().BeFalse("upstream copies for branches and stashes only");
        menu.Items.OfType<MenuItem>().Select(item => (item.Header as TextBlock)?.Text).Should().NotContain("Grid script");
        window.Close();
    }

    [AvaloniaTest]
    public void A_branch_that_the_selected_commit_contains_has_the_merged_image_and_tooltip()
    {
        _repo.Run("branch", "older", "HEAD~1");
        string current = _repo.Run("branch", "--show-current").Trim();
        MainWindow window = OpenWindow();
        TreeView tree = Find<TreeView>(window, "BranchTreeView");
        ListBox commits = Find<ListBox>(window, "CommitList");
        WaitUntil(() => SelectedSubject(commits) == "second" && BranchNode(tree, "older")?.IsMerged is true);

        BranchNode(tree, "older")!.Image.Should().Be("BranchLocalMerged");
        BranchNode(tree, "older")!.ToolTip.Should().Be("'older' is contained in the currently selected commit");
        BranchNode(tree, current)!.IsMerged.Should().BeFalse("upstream leaves out the branches at the selected commit");

        commits.SelectedItem = commits.Items.Cast<CommitListItem>().Single(item => item.Row.Subject == "first");
        WaitUntil(() => BranchNode(tree, "older")?.IsMerged is false);

        BranchNode(tree, current)!.IsMerged.Should().BeFalse();

        Click(window, "RefreshButton");
        WaitUntil(() => !window.IsBusy);
        commits.SelectedItem = commits.Items.Cast<CommitListItem>().Single(item => item.Row.Subject == "second");
        WaitUntil(() => BranchNode(tree, "older")?.IsMerged is true);
        window.Close();
    }

    [AvaloniaTest]
    public void A_branch_whose_commit_the_grid_does_not_show_is_hidden_until_the_grid_shows_it()
    {
        string current = _repo.Run("branch", "--show-current").Trim();
        _repo.Run("checkout", "-q", "-b", "side");
        _repo.Run("commit", "-q", "--allow-empty", "-m", "side work");
        _repo.Run("checkout", "-q", current);
        TestAppBuilder.Preferences.ShowCurrentBranchOnly = true;
        MainWindow window = OpenWindow();
        TreeView tree = Find<TreeView>(window, "BranchTreeView");
        WaitUntil(() => BranchNode(tree, "side")?.IsHidden is true);

        BranchNode(tree, "side")!.Image.Should().Be("EyeClosed");
        BranchNode(tree, "side")!.ToolTip.Should().Be("'side' is not currently visible");
        BranchNode(tree, current)!.IsHidden.Should().BeFalse();

        Click(window, "ShowBranchesAllMenuItem");

        WaitUntil(() => BranchNode(tree, "side")?.IsHidden is false);
        BranchNode(tree, "side")!.Image.Should().Be("BranchLocal");
        window.Close();
    }

    private static void ClickItem(Window window, TreeView tree, RepoObjectNode node, RawInputModifiers modifiers)
    {
        FocusItem(tree, node);
        TreeViewItem item = tree.GetVisualDescendants().OfType<TreeViewItem>().Single(candidate => candidate.DataContext == node);
        TextBlock text = item.GetVisualDescendants().OfType<TextBlock>().First(block => block.Text == node.Text);
        Point point = text.TranslatePoint(new Point(2, text.Bounds.Height / 2), window)!.Value;
        window.MouseDown(point, MouseButton.Left, modifiers);
        window.MouseUp(point, MouseButton.Left, modifiers);
        Dispatcher.UIThread.RunJobs();
    }

    // The tree itself takes no focus; its item does, as a click gives it.
    private static void FocusItem(TreeView tree, RepoObjectNode node)
    {
        foreach (RepoObjectNode parent in RepoObjectsTree.PathTo(Roots(tree), node).SkipLast(1))
        {
            parent.IsExpanded = true;
        }

        Dispatcher.UIThread.RunJobs();
        tree.GetVisualDescendants().OfType<TreeViewItem>().Single(item => item.DataContext == node).Focus();
    }

    private static string? SelectedSubject(ListBox commits) => (commits.SelectedItem as CommitListItem)?.Row.Subject;

    private static RepoObjectNode? TagNode(TreeView tree, string name)
        => Roots(tree).SelectMany(root => root.Descendants())
            .FirstOrDefault(node => node.Kind == RepoObjectKind.Tag && node.Item as string == name);

    private static void Reset()
    {
        TestAppBuilder.Scripts.Save([]);
        foreach (RepoObjectKind root in RepoObjectsTree.Roots)
        {
            TestAppBuilder.Preferences.ShowRepoObjectsTree(root, true);
        }

        TestAppBuilder.Preferences.RepoObjectsTreeOrder = RepoObjectsTree.Roots;
        TestAppBuilder.Preferences.ShowCurrentBranchOnly = false;
        TestAppBuilder.Preferences.ShowSubmoduleStatus = false;
        TestAppBuilder.Preferences.ResetConfirmations();
    }

    // git refuses a submodule from a local path unless the file protocol is allowed.
    private void AddSubmodule(TestRepository library, string path)
    {
        _repo.Run("-c", "protocol.file.allow=always", "submodule", "add", "-q", library.Path, path);
        _repo.Run("commit", "-q", "-m", "add submodule");
    }

    private static RepoObjectNode? SubmoduleTop(TreeView tree)
        => Roots(tree).Single(root => root.Kind == RepoObjectKind.SubmodulesRoot).Children.FirstOrDefault();

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

        // Until the submodule tree is read (in the background) the tree is filled once more.
        WaitUntil(() => !window.IsBusy && Roots(window.FindControl<TreeView>("BranchTreeView")!) is { Count: > 0 } roots
                        && roots.All(root => root.Kind != RepoObjectKind.SubmodulesRoot || root.Children.Count > 0));
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

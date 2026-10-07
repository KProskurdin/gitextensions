using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;
using GitCommands;
using GitCommands.Git;
using GitExtensions.Xplat.App;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Scripts;
using GitExtensions.Xplat.Core.Settings;
using GitExtUtils.GitUI.Theming;
using GitUI.ScriptsEngine;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

internal sealed class MainWindowTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(20);

    private TestRepository _repo = null!;

    [SetUp]
    public void Setup()
    {
        _repo = new TestRepository();
        TestAppBuilder.Preferences.CloseCommitDialogAfterCommit = true;
        TestAppBuilder.Preferences.Theme = ThemeId.DefaultLight;
        TestAppBuilder.Preferences.ThemeVariations = [];
        TestAppBuilder.Preferences.ShowCurrentBranchOnly = false;
        TestAppBuilder.Preferences.SerializedHotkeys = null;
        TestAppBuilder.Preferences.ResetConfirmations();
        Hotkeys.Load(null);
        TestAppBuilder.GitConfig.Clear();
        TestAppBuilder.Scripts.Save([]);
    }

    [TearDown]
    public void TearDown()
    {
        _repo.Dispose();
    }

    [AvaloniaTest]
    public void Shows_install_message_and_disables_open_when_git_is_missing()
    {
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.NotFound, Command: null, Version: null));

        Find<TextBlock>(window, "GitProblemText").Text.Should().Contain("git was not found");
        Find<TextBlock>(window, "GitProblemText").IsVisible.Should().BeTrue();
        Find<Button>(window, "OpenButton").IsEnabled.Should().BeFalse();
        Find<MenuItem>(window, "OpenMenuItem").IsEnabled.Should().BeFalse();
    }

    [AvaloniaTest]
    public void Shows_version_message_when_git_is_too_old()
    {
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.TooOld, "/usr/bin/git",
            new GitVersion("git version 2.20.1")));

        Find<TextBlock>(window, "GitProblemText").Text.Should().Contain("2.20.1");
        Find<Button>(window, "OpenButton").IsEnabled.Should().BeTrue();
    }

    [AvaloniaTest]
    public void Open_lists_commits_and_titles_the_window_after_the_repository()
    {
        MainWindow window = NewWindow();

        Open(window, _repo.Path);

        Find<TextBlock>(window, "StatusText").Text.Should().Be("2 commits");
        Find<ListBox>(window, "CommitList").ItemCount.Should().Be(2);
        window.Title.Should().Be($"{Path.GetFileName(_repo.Path)} - Git Extensions");
        Find<Grid>(window, "BrowsePanel").IsVisible.Should().BeTrue();
        Find<DockPanel>(window, "DashboardPanel").IsVisible.Should().BeFalse();
    }

    [AvaloniaTest]
    public void The_first_row_carries_the_head_and_branch_labels()
    {
        MainWindow window = NewWindow();

        Open(window, _repo.Path);

        string branch = _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim();
        CommitListItem first = ((IReadOnlyList<CommitListItem>)Find<ListBox>(window, "CommitList").ItemsSource!)[0];
        first.Labels.Should().Equal(new RefLabel("HEAD", RefKind.Head), new RefLabel(branch, RefKind.Branch));
    }

    [AvaloniaTest]
    public void Opening_a_repository_adds_it_to_the_recent_list_and_closing_shows_the_dashboard()
    {
        MainWindow window = NewWindow();
        Open(window, _repo.Path);
        ListBox recent = Find<ListBox>(window, "RecentList");
        WaitUntil(() =>
            ((IReadOnlyList<RecentRepository>?)recent.ItemsSource)?.Any(item =>
                RecentRepositoryPaths.Same(item.Path, _repo.Path)) == true);

        Click(window, "CloseRepositoryMenuItem");

        Find<DockPanel>(window, "DashboardPanel").IsVisible.Should().BeTrue();
        Find<Grid>(window, "BrowsePanel").IsVisible.Should().BeFalse();
        window.Title.Should().Be("Git Extensions");
        Find<Button>(window, "CommitDialogButton").IsEnabled.Should().BeFalse();
        Find<MenuItem>(window, "RecentMenu").ItemCount.Should().BeGreaterThan(0);
    }

    [AvaloniaTest]
    public void Enter_on_a_recent_repository_opens_it()
    {
        MainWindow window = NewWindow();
        Open(window, _repo.Path);
        Click(window, "CloseRepositoryMenuItem");
        ListBox recent = Find<ListBox>(window, "RecentList");
        recent.SelectedIndex = ((IReadOnlyList<RecentRepository>)recent.ItemsSource!).ToList()
            .FindIndex(item => RecentRepositoryPaths.Same(item.Path, _repo.Path));

        recent.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });

        WaitUntil(() => Find<ListBox>(window, "CommitList").ItemCount == 2);
        Find<Grid>(window, "BrowsePanel").IsVisible.Should().BeTrue();
    }

    [AvaloniaTest]
    public void A_closed_window_reopens_with_its_size_and_maximized_state()
    {
        MainWindow first = NewWindow();
        first.Show();
        first.Width = 900;
        first.Height = 640;
        Dispatcher.UIThread.RunJobs();
        first.Close();
        TestAppBuilder.WindowPlacements.Load("Xplat.MainWindow").Should().NotBeNull();

        TestAppBuilder.WindowPlacements.Save("Xplat.MainWindow",
            TestAppBuilder.WindowPlacements.Load("Xplat.MainWindow")! with { Maximized = true });
        MainWindow second = NewWindow();

        second.Width.Should().BeApproximately(900, 1);
        second.Height.Should().BeApproximately(640, 1);
        second.WindowState.Should().Be(WindowState.Maximized);
    }

    [AvaloniaTest]
    public void The_grid_shows_every_branch_until_current_branch_is_chosen_and_the_filter_narrows_it()
    {
        TestAppBuilder.Preferences.ShowCurrentBranchOnly = false;
        string baseBranch = _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim();
        _repo.Run("checkout", "-q", "-b", "side");
        File.WriteAllText(Path.Combine(_repo.Path, "s.txt"), "x");
        _repo.Run("add", "s.txt");
        _repo.Run("commit", "-q", "-m", "side work");
        _repo.Run("checkout", "-q", baseBranch);
        MainWindow window = NewWindow();
        window.Show();
        Open(window, _repo.Path);
        ListBox commits = Find<ListBox>(window, "CommitList");
        commits.ItemCount.Should().Be(3);

        Find<ComboBox>(window, "BranchScopeBox").SelectedIndex = 1;
        WaitUntil(() => commits.ItemCount == 2);
        TestAppBuilder.Preferences.ShowCurrentBranchOnly.Should().BeTrue();

        Click(window, "RevisionFilterButton");
        WaitUntil(() => window.OwnedWindows.OfType<FilterWindow>().Any());
        FilterWindow filter = window.OwnedWindows.OfType<FilterWindow>().Single();
        Find<TextBox>(filter, "MessageBox").Text = "FIRST";
        Click(filter, "ApplyButton");

        WaitUntil(() => commits.ItemCount == 1);
        Find<TextBlock>(window, "StatusText").Text.Should().Be("1 commit (filtered)");
        TestAppBuilder.Preferences.ShowCurrentBranchOnly = false;
    }

    [AvaloniaTest]
    public void Opening_a_folder_that_is_not_a_repository_shows_an_error_dialog()
    {
        string folder = Path.Combine(Path.GetTempPath(), "xplat-not-a-repo-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            MainWindow window = NewWindow();
            window.Show();

            Open(window, folder);
            WaitUntil(() => window.OwnedWindows.Count == 1);

            ErrorWindow dialog = (ErrorWindow)window.OwnedWindows[0];
            Find<TextBlock>(dialog, "MessageText").Text.Should().Contain("Not a git repository");
            dialog.Close();
            window.Close();
        }
        finally
        {
            Directory.Delete(folder);
        }
    }

    [AvaloniaTest]
    public void Open_shows_the_current_branch_and_the_number_of_changes_on_the_commit_button()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "new.txt"), "content");
        MainWindow window = NewWindow();

        Open(window, _repo.Path);
        WaitUntil(() => Find<Button>(window, "CommitDialogButton").Content as string == "Commit (1)");

        string current = _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim();
        Find<TextBlock>(window, "BranchText").Text.Should().Be($"Branch: {current}");
    }

    [AvaloniaTest]
    public void Open_folder_and_terminal_need_an_open_repository()
    {
        MainWindow window = NewWindow();
        Find<Button>(window, "OpenFolderButton").IsEnabled.Should().BeFalse();
        Find<Button>(window, "TerminalButton").IsEnabled.Should().BeFalse();

        Open(window, _repo.Path);

        Find<Button>(window, "OpenFolderButton").IsEnabled.Should().BeTrue();
        Find<Button>(window, "TerminalButton").IsEnabled.Should().BeTrue();
    }

    [AvaloniaTest]
    public void Copy_message_puts_the_full_commit_message_on_the_clipboard()
    {
        MainWindow window = NewWindow();
        window.Show();
        Open(window, _repo.Path);
        Find<ListBox>(window, "CommitList").SelectedIndex = 0;
        WaitUntil(() => Find<Button>(window, "CopyMessageButton").IsEnabled);

        Click(window, "CopyMessageButton");
        Task<string?> text = TopLevel.GetTopLevel(window)!.Clipboard!.TryGetTextAsync();
        WaitUntil(() => text.IsCompleted);

        text.Result.Should().Contain("second").And.Contain("body line");
    }

    [AvaloniaTest]
    public void Copy_hash_from_the_context_menu_puts_the_selected_commit_hash_on_the_clipboard()
    {
        MainWindow window = NewWindow();
        window.Show();
        Open(window, _repo.Path);
        Find<ListBox>(window, "CommitList").SelectedIndex = 0;
        WaitUntil(() => Find<Button>(window, "CopyHashButton").IsEnabled);

        Click(window, "CopyHashMenuItem");
        Task<string?> text = TopLevel.GetTopLevel(window)!.Clipboard!.TryGetTextAsync();
        WaitUntil(() => text.IsCompleted);

        text.Result.Should().Be(_repo.Run("rev-parse", "HEAD").Trim());
        window.Close();
    }

    [AvaloniaTest]
    public void Selecting_a_file_of_a_commit_shows_its_diff_in_the_diff_tab()
    {
        MainWindow window = NewWindow();
        window.Show();
        Open(window, _repo.Path);
        Find<ListBox>(window, "CommitList").SelectedIndex = 0;
        ListBox files = Find<ListBox>(window, "CommitFilesList");
        WaitUntil(() => files.ItemCount == 1);

        files.SelectedIndex = 0;

        DiffView diff = Find<DiffView>(window, "CommitDiff");
        WaitUntil(() => diff.Lines.Count > 0);
        diff.Lines.Should().Contain(line => line.Text == "+two");
        window.Close();
    }

    [AvaloniaTest]
    public void File_tree_tab_shows_the_folders_of_the_selected_commit_and_the_content_of_a_file()
    {
        Directory.CreateDirectory(Path.Combine(_repo.Path, "docs"));
        File.WriteAllText(Path.Combine(_repo.Path, "docs", "guide.md"), "line one\nline two\n");
        _repo.Run("add", "docs/guide.md");
        _repo.Run("commit", "-q", "-m", "docs");
        MainWindow window = NewWindow();
        window.Show();
        Open(window, _repo.Path);

        Find<TabControl>(window, "DetailTabs").SelectedItem = Find<TabItem>(window, "FileTreeTab");
        TreeView tree = Find<TreeView>(window, "FileTreeView");
        WaitUntil(() => tree.ItemsSource is IReadOnlyList<FileTreeNode> { Count: 2 });
        IReadOnlyList<FileTreeNode> nodes = (IReadOnlyList<FileTreeNode>)tree.ItemsSource!;
        nodes.Select(node => node.Name).Should().Equal("docs", "a.txt");

        tree.SelectedItem = nodes[0].Children[0];

        DiffView content = Find<DiffView>(window, "FileContentView");
        WaitUntil(() => content.Lines.Count == 2);
        content.Lines.Select(line => (line.NewNumber, line.Text)).Should().Equal(("1", "line one"), ("2", "line two"));
        window.Close();
    }

    [AvaloniaTest]
    public void Show_diff_of_a_commit_file_opens_the_diff_window_with_its_lines()
    {
        MainWindow window = NewWindow();
        window.Show();
        Open(window, _repo.Path);
        Find<ListBox>(window, "CommitList").SelectedIndex = 0;
        ListBox files = Find<ListBox>(window, "CommitFilesList");
        WaitUntil(() => files.ItemCount == 1);

        files.SelectedIndex = 0;
        Click(window, "ShowCommitDiffButton");
        WaitUntil(() => window.OwnedWindows.OfType<DiffWindow>().Any());

        DiffWindow diff = window.OwnedWindows.OfType<DiffWindow>().Single();
        DiffView view = Find<DiffView>(diff, "Diff");
        WaitUntil(() => view.Lines.Count > 0);
        view.Lines.Should().Contain(line => line.Text == "+two");
        diff.Title.Should().EndWith("a.txt");
        diff.Close();
        window.Close();
    }

    [AvaloniaTest]
    public void Files_window_lists_the_files_at_head_and_the_history_of_the_selected_file()
    {
        MainWindow window = NewWindow();
        window.Show();
        Open(window, _repo.Path);

        Click(window, "FilesButton");
        WaitUntil(() => window.OwnedWindows.OfType<FileBrowserWindow>().Any());

        FileBrowserWindow files = window.OwnedWindows.OfType<FileBrowserWindow>().Single();
        ListBox fileList = files.FindControl<ListBox>("FileList")!;
        WaitUntil(() => fileList.ItemCount == 1);
        fileList.SelectedIndex = 0;

        ListBox history = files.FindControl<ListBox>("HistoryList")!;
        WaitUntil(() => history.ItemCount == 2);
        files.Close();
        window.Close();
    }

    [AvaloniaTest]
    public void Blame_from_the_file_browser_opens_a_line_per_file_line()
    {
        MainWindow window = NewWindow();
        window.Show();
        Open(window, _repo.Path);
        Click(window, "FilesButton");
        WaitUntil(() => window.OwnedWindows.OfType<FileBrowserWindow>().Any());
        FileBrowserWindow files = window.OwnedWindows.OfType<FileBrowserWindow>().Single();
        ListBox fileList = files.FindControl<ListBox>("FileList")!;
        WaitUntil(() => fileList.ItemCount == 1);
        fileList.SelectedIndex = 0;
        WaitUntil(() => files.FindControl<Button>("BlameButton")!.IsEnabled);

        Click(files, "BlameButton");
        WaitUntil(() => files.OwnedWindows.OfType<BlameWindow>().Any());
        BlameWindow blame = files.OwnedWindows.OfType<BlameWindow>().Single();
        ListBox lines = blame.FindControl<ListBox>("BlameList")!;
        WaitUntil(() => lines.ItemCount == 1);

        blame.Close();
        files.Close();
        window.Close();
    }

    [AvaloniaTest]
    public void Merge_button_merges_the_selected_branch_into_the_current_one()
    {
        string baseBranch = _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim();
        _repo.Run("checkout", "-q", "-b", "feature");
        File.WriteAllText(Path.Combine(_repo.Path, "f.txt"), "x");
        _repo.Run("add", "f.txt");
        _repo.Run("commit", "-q", "-m", "feature work");
        _repo.Run("checkout", "-q", baseBranch);
        MainWindow window = NewWindow();
        window.Show();
        Open(window, _repo.Path);
        TreeView branches = Find<TreeView>(window, "BranchTreeView");
        WaitUntil(() => BranchNode(branches, "feature") is not null);

        branches.SelectedItem = BranchNode(branches, "feature");
        Find<Button>(window, "MergeButton").IsEnabled.Should().BeTrue();
        Click(window, "MergeButton");
        WaitUntil(() => Find<TextBlock>(window, "OperationStatusText").Text == "Merged feature");

        _repo.Run("log", "-1", "--format=%s").Trim().Should().Be("feature work");
        WaitUntil(() => Find<Button>(window, "OpenButton").IsEnabled);
        window.Close();
    }

    [AvaloniaTest]
    public void Checkout_with_local_changes_asks_and_can_stash_them_across_the_checkout()
    {
        string baseBranch = _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim();
        _repo.Run("branch", "other");
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "local edit");
        MainWindow window = NewWindow();
        window.Show();
        Open(window, _repo.Path);
        TreeView branches = Find<TreeView>(window, "BranchTreeView");
        WaitUntil(() =>
            BranchNode(branches, "other") is not null &&
            Find<Button>(window, "CommitDialogButton").Content as string == "Commit (1)");
        branches.SelectedItem = BranchNode(branches, "other");

        Click(window, "CheckoutButton");
        WaitUntil(() => window.OwnedWindows.OfType<CheckoutWindow>().Any());
        CheckoutWindow dialog = window.OwnedWindows.OfType<CheckoutWindow>().Single();
        Find<RadioButton>(dialog, "StashRadio").IsChecked = true;
        Click(dialog, "CheckoutButton");

        WaitUntil(() => Find<TextBlock>(window, "OperationStatusText").Text == "Checked out other");
        _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim().Should().Be("other").And.NotBe(baseBranch);
        File.ReadAllText(Path.Combine(_repo.Path, "a.txt")).Should().Be("local edit");
        _repo.Run("stash", "list").Trim().Should().BeEmpty();
        TestAppBuilder.Preferences.CheckoutBranchAction.Should().Be(GitCommands.LocalChangesAction.Stash);
        TestAppBuilder.Preferences.CheckoutBranchAction = GitCommands.LocalChangesAction.DontChange;
    }

    [AvaloniaTest]
    public void Checkout_this_commit_detaches_head_at_the_selected_commit()
    {
        MainWindow window = NewWindow();
        window.Show();
        Open(window, _repo.Path);
        ListBox commits = Find<ListBox>(window, "CommitList");
        commits.SelectedIndex = 1;
        WaitUntil(() => Find<MenuItem>(window, "CheckoutCommitMenuItem").IsEnabled);

        Click(window, "CheckoutCommitMenuItem");

        WaitUntil(() =>
            (Find<TextBlock>(window, "OperationStatusText").Text ?? "").StartsWith("Checked out",
                StringComparison.Ordinal));
        _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim().Should().Be("HEAD");
        _repo.Run("log", "-1", "--format=%s").Trim().Should().Be("first");
        WaitUntil(() => Find<TextBlock>(window, "BranchText").Text == "");
    }

    [AvaloniaTest]
    public void Stash_and_pop_from_the_window_restore_the_change()
    {
        MainWindow window = NewWindow();
        Open(window, _repo.Path);
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "changed");
        Click(window, "OpenButton");
        Button commit = Find<Button>(window, "CommitDialogButton");
        WaitUntil(() => commit.Content as string == "Commit (1)");

        Find<TextBox>(window, "StashMessageBox").Text = "wip";
        Click(window, "StashButton");
        WaitUntil(() => Find<TextBlock>(window, "OperationStatusText").Text == "Stashed");
        WaitUntil(() => commit.Content as string == "Commit");
        _repo.Run("stash", "list").Should().Contain("wip");

        ListBox stashes = Find<ListBox>(window, "StashList");
        WaitUntil(() => stashes.ItemCount == 1);
        stashes.SelectedIndex = 0;
        Click(window, "PopStashButton");
        WaitUntil(() => Find<TextBlock>(window, "OperationStatusText").Text == "Stash popped");
        WaitUntil(() => commit.Content as string == "Commit (1)");
        File.ReadAllText(Path.Combine(_repo.Path, "a.txt")).Should().Be("changed");
    }

    [AvaloniaTest]
    public void Refresh_shows_a_branch_created_outside_the_app()
    {
        MainWindow window = NewWindow();
        Open(window, _repo.Path);
        TreeView branches = Find<TreeView>(window, "BranchTreeView");
        WaitUntil(() => BranchNode(branches, _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim()) is not null);

        _repo.Run("branch", "team/external");
        Click(window, "RefreshButton");

        WaitUntil(() => BranchNode(branches, "team/external") is not null);
        ((IReadOnlyList<BranchTreeNode>)branches.ItemsSource!)[0].Children.Should()
            .Contain(node => node.Display == "team (1)");
    }

    [AvaloniaTest]
    public void Reset_hard_from_the_context_menu_asks_first_and_moves_the_branch_back()
    {
        MainWindow window = NewWindow();
        window.Show();
        Open(window, _repo.Path);
        ListBox commits = Find<ListBox>(window, "CommitList");
        WaitUntil(() => commits.ItemCount == 2);
        commits.SelectedIndex = 1;

        Click(window, "ResetHardMenuItem");
        WaitUntil(() => window.OwnedWindows.OfType<ConfirmWindow>().Any());
        Click(window.OwnedWindows.OfType<ConfirmWindow>().Single(), "ConfirmButton");

        WaitUntil(() =>
            (Find<TextBlock>(window, "OperationStatusText").Text ?? "").StartsWith("Reset to",
                StringComparison.Ordinal));
        _repo.Run("log", "-1", "--format=%s").Trim().Should().Be("first");
    }

    [AvaloniaTest]
    public void Reflog_window_lists_the_head_history()
    {
        MainWindow window = NewWindow();
        window.Show();
        Open(window, _repo.Path);
        WaitUntil(() => Find<Button>(window, "ReflogButton").IsEnabled);

        Click(window, "ReflogButton");
        WaitUntil(() => window.OwnedWindows.OfType<ReflogWindow>().Any());
        ReflogWindow reflog = window.OwnedWindows.OfType<ReflogWindow>().Single();
        ListBox entries = Find<ListBox>(reflog, "EntryList");
        WaitUntil(() => entries.ItemCount > 0);

        entries.ItemCount.Should().BeGreaterThan(1);
        reflog.Close(null);
    }

    [AvaloniaTest]
    public void Create_and_delete_a_tag_from_the_window()
    {
        MainWindow window = NewWindow();
        Open(window, _repo.Path);
        ListBox tags = Find<ListBox>(window, "TagList");

        Find<TextBox>(window, "NewTagBox").Text = "v1.0";
        Click(window, "CreateTagButton");
        WaitUntil(() => tags.ItemCount == 1);
        _repo.Run("tag", "--list").Trim().Should().Be("v1.0");

        tags.SelectedIndex = 0;
        Click(window, "DeleteTagButton");
        WaitUntil(() => tags.ItemCount == 0);
        _repo.Run("tag", "--list").Trim().Should().BeEmpty();
    }

    [AvaloniaTest]
    public void Create_branch_here_asks_for_a_name_and_checks_out_the_new_branch_at_the_selected_commit()
    {
        MainWindow window = NewWindow();
        window.Show();
        Open(window, _repo.Path);
        ListBox commits = Find<ListBox>(window, "CommitList");
        commits.SelectedIndex = 1;
        WaitUntil(() => Find<MenuItem>(window, "CreateBranchHereMenuItem").IsEnabled);

        Click(window, "CreateBranchHereMenuItem");
        WaitUntil(() => window.OwnedWindows.OfType<PromptWindow>().Any());
        PromptWindow prompt = window.OwnedWindows.OfType<PromptWindow>().Single();
        Find<TextBox>(prompt, "ValueBox").Text = "from-first";
        Click(prompt, "OkButton");

        WaitUntil(() => Find<TextBlock>(window, "OperationStatusText").Text == "Created from-first");
        _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim().Should().Be("from-first");
        _repo.Run("log", "-1", "--format=%s").Trim().Should().Be("first");
    }

    [AvaloniaTest]
    public void Control_space_opens_the_commit_window()
    {
        MainWindow window = NewWindow();
        window.Show();
        Open(window, _repo.Path);

        window.RaiseEvent(new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space, KeyModifiers = Hotkeys.CommandModifier,
        });

        WaitUntil(() => window.OwnedWindows.OfType<CommitWindow>().Any());
        window.OwnedWindows.OfType<CommitWindow>().Single().Close();
    }

    [AvaloniaTest]
    public void Commit_window_lists_unstaged_and_staged_files_apart_and_shows_the_selected_diff()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "changed");
        File.WriteAllText(Path.Combine(_repo.Path, "staged.txt"), "content");
        _repo.Run("add", "staged.txt");
        MainWindow window = NewWindow();
        window.Show();
        Open(window, _repo.Path);
        CommitWindow commit = OpenCommitWindow(window);
        ListBox unstaged = Find<ListBox>(commit, "UnstagedList");
        ListBox staged = Find<ListBox>(commit, "StagedList");

        WaitUntil(() => unstaged.ItemCount == 1 && staged.ItemCount == 1);
        unstaged.SelectedIndex = 0;

        DiffView diff = Find<DiffView>(commit, "Diff");
        WaitUntil(() => diff.Lines.Any(line => line.Text == "+changed"));
        commit.Close();
    }

    [AvaloniaTest]
    public void Stage_selected_lines_stages_only_the_chosen_edit_of_a_file()
    {
        string file = Path.Combine(_repo.Path, "lines.txt");
        File.WriteAllLines(file, Enumerable.Range(1, 20).Select(i => $"line {i}"));
        _repo.Run("add", "lines.txt");
        _repo.Run("commit", "-q", "-m", "lines");
        File.WriteAllLines(file, Enumerable.Range(1, 20).Select(i => i is 2 or 19 ? $"line {i} changed" : $"line {i}"));
        MainWindow window = NewWindow();
        window.Show();
        Open(window, _repo.Path);
        CommitWindow commit = OpenCommitWindow(window);
        ListBox unstaged = Find<ListBox>(commit, "UnstagedList");
        WaitUntil(() => unstaged.ItemCount == 1);
        unstaged.SelectedIndex = 0;
        DiffView diff = Find<DiffView>(commit, "Diff");
        WaitUntil(() => diff.Lines.Any(line => line.Text == "+line 2 changed"));

        ListBox diffList = diff.FindControl<ListBox>("DiffList")!;
        List<DiffLineItem> lines = [.. diff.Lines];
        diffList.Selection.Select(lines.FindIndex(line => line.Text == "-line 2"));
        diffList.Selection.Select(lines.FindIndex(line => line.Text == "+line 2 changed"));
        WaitUntil(() => Find<Button>(commit, "StageLinesButton").IsEnabled);
        Click(commit, "StageLinesButton");

        WaitUntil(() => Find<TextBlock>(window, "OperationStatusText").Text == "Staged selected lines");
        string staged = _repo.Run("diff", "--cached", "lines.txt");
        staged.Should().Contain("+line 2 changed").And.NotContain("line 19 changed");
        _repo.Run("diff", "lines.txt").Should().Contain("+line 19 changed").And.NotContain("line 2 changed");
        commit.Close();
    }

    [AvaloniaTest]
    public void Stage_selected_lines_of_an_untracked_file_adds_the_file_with_only_those_lines()
    {
        File.WriteAllLines(Path.Combine(_repo.Path, "new.txt"), Enumerable.Range(1, 5).Select(i => $"line {i}"));
        MainWindow window = NewWindow();
        window.Show();
        Open(window, _repo.Path);
        CommitWindow commit = OpenCommitWindow(window);
        ListBox unstaged = Find<ListBox>(commit, "UnstagedList");
        WaitUntil(() => unstaged.ItemCount == 1);
        unstaged.SelectedIndex = 0;
        DiffView diff = Find<DiffView>(commit, "Diff");
        WaitUntil(() => diff.Lines.Any(line => line.Text == "+line 5"));

        SelectDiffLines(diff, "+line 2", "+line 3");
        WaitUntil(() => Find<Button>(commit, "StageLinesButton").IsEnabled);
        Click(commit, "StageLinesButton");

        WaitUntil(() => Find<TextBlock>(window, "OperationStatusText").Text == "Staged selected lines");
        _repo.Run("show", ":new.txt").ReplaceLineEndings("\n").Should().Be("line 2\nline 3\n");
        File.ReadAllLines(Path.Combine(_repo.Path, "new.txt")).Should().HaveCount(5);
        commit.Close();
    }

    [AvaloniaTest]
    public void Unstage_selected_lines_of_an_added_file_keeps_the_file_and_the_other_lines_staged()
    {
        File.WriteAllLines(Path.Combine(_repo.Path, "new.txt"), Enumerable.Range(1, 5).Select(i => $"line {i}"));
        _repo.Run("add", "new.txt");
        MainWindow window = NewWindow();
        window.Show();
        Open(window, _repo.Path);
        CommitWindow commit = OpenCommitWindow(window);
        ListBox staged = Find<ListBox>(commit, "StagedList");
        WaitUntil(() => staged.ItemCount == 1);
        staged.SelectedIndex = 0;
        DiffView diff = Find<DiffView>(commit, "Diff");
        WaitUntil(() => diff.Lines.Any(line => line.Text == "+line 5"));

        SelectDiffLines(diff, "+line 3");
        WaitUntil(() => Find<Button>(commit, "UnstageLinesButton").IsEnabled);
        Click(commit, "UnstageLinesButton");

        WaitUntil(() => Find<TextBlock>(window, "OperationStatusText").Text == "Unstaged selected lines");
        _repo.Run("show", ":new.txt").ReplaceLineEndings("\n").Should().Be("line 1\nline 2\nline 4\nline 5\n");
        commit.Close();
    }

    [AvaloniaTest]
    public void Discard_asks_first_and_restores_the_file_when_confirmed()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "changed");
        MainWindow window = NewWindow();
        window.Show();
        Open(window, _repo.Path);
        CommitWindow commit = OpenCommitWindow(window);
        ListBox unstaged = Find<ListBox>(commit, "UnstagedList");
        WaitUntil(() => unstaged.ItemCount == 1);
        unstaged.SelectedIndex = 0;

        Click(commit, "DiscardButton");
        WaitUntil(() => commit.OwnedWindows.OfType<ConfirmWindow>().Any());
        Click(commit.OwnedWindows.OfType<ConfirmWindow>().Single(), "ConfirmButton");

        WaitUntil(() => unstaged.ItemCount == 0);
        File.ReadAllText(Path.Combine(_repo.Path, "a.txt")).Should().Be("two");
    }

    [AvaloniaTest]
    public void Control_enter_in_the_message_box_commits_the_staged_changes_and_closes_the_window()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "new.txt"), "content");
        _repo.Run("add", "new.txt");
        MainWindow window = NewWindow();
        window.Show();
        Open(window, _repo.Path);
        CommitWindow commit = OpenCommitWindow(window);
        TextBox message = Find<TextBox>(commit, "CommitMessageBox");
        message.Text = "via shortcut";

        message.RaiseEvent(new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter, KeyModifiers = Hotkeys.CommandModifier,
        });

        WaitUntil(() => Find<TextBlock>(window, "OperationStatusText").Text == "Committed");
        _repo.Run("log", "-1", "--format=%s").Trim().Should().Be("via shortcut");
        WaitUntil(() => !window.OwnedWindows.OfType<CommitWindow>().Any());
    }

    [AvaloniaTest]
    public void Stage_all_stages_every_change_in_the_list()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "new.txt"), "content");
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "changed");
        MainWindow window = NewWindow();
        window.Show();
        Open(window, _repo.Path);
        CommitWindow commit = OpenCommitWindow(window);
        WaitUntil(() => Find<ListBox>(commit, "UnstagedList").ItemCount == 2);

        Click(commit, "StageAllButton");

        WaitUntil(() => Find<TextBlock>(window, "OperationStatusText").Text == "Staged");
        WaitUntil(() => Find<ListBox>(commit, "StagedList").ItemCount == 2);
        _repo.Run("diff", "--cached", "--name-only").Trim().Split('\n').Should().BeEquivalentTo(["a.txt", "new.txt"]);
        commit.Close();
    }

    [AvaloniaTest]
    public void Commit_and_push_publishes_the_commit_to_origin()
    {
        string remote = Path.Combine(Path.GetTempPath(), "xplat-remote-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(remote);
        try
        {
            GitProcess.Run(remote, "init", "--bare", "-q");
            string branch = _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim();
            _repo.Run("remote", "add", "origin", remote);
            _repo.Run("push", "-q", "-u", "origin", branch);
            File.WriteAllText(Path.Combine(_repo.Path, "new.txt"), "content");
            _repo.Run("add", "new.txt");
            MainWindow window = NewWindow();
            window.Show();
            Open(window, _repo.Path);
            CommitWindow commit = OpenCommitWindow(window);
            WaitUntil(() => Find<Button>(commit, "CommitAndPushButton").IsEnabled);
            Find<TextBox>(commit, "CommitMessageBox").Text = "pushed change";

            Click(commit, "CommitAndPushButton");

            WaitUntil(() => Find<TextBlock>(window, "OperationStatusText").Text == "Pushed");
            GitProcess.Run(remote, "log", "-1", "--format=%s", branch).Trim().Should().Be("pushed change");
        }
        finally
        {
            GitProcess.DeleteFolder(remote);
        }
    }

    [AvaloniaTest]
    public void Push_shows_git_output_in_the_process_window_and_reports_done()
    {
        string remote = Path.Combine(Path.GetTempPath(), "xplat-remote-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(remote);
        try
        {
            GitProcess.Run(remote, "init", "--bare", "-q");
            _repo.Run("remote", "add", "origin", remote);
            MainWindow window = NewWindow();
            window.Show();
            Open(window, _repo.Path);

            Click(window, "PushButton");

            // The remote has no branch yet, so upstream's "new branch for the remote" question comes first.
            WaitUntil(() => window.OwnedWindows.OfType<ConfirmWindow>().Any());
            Click(window.OwnedWindows.OfType<ConfirmWindow>().Single(), "ConfirmButton");
            WaitUntil(() => window.OwnedWindows.OfType<ProcessWindow>().Any());
            ProcessWindow process = window.OwnedWindows.OfType<ProcessWindow>().Single();
            WaitUntil(() => Find<TextBlock>(process, "StateText").Text == "Done");

            Find<ListBox>(process, "OutputList").ItemCount.Should().BeGreaterThan(0);
            Find<Button>(process, "CloseButton").IsEnabled.Should().BeTrue();
            Find<Button>(process, "AbortButton").IsEnabled.Should().BeFalse();
            process.Title.Should().StartWith("Push ");
            process.Close();
        }
        finally
        {
            GitProcess.DeleteFolder(remote);
        }
    }

    [AvaloniaTest]
    public void Push_dialog_pushes_the_chosen_branch_with_lease()
    {
        string remote = Path.Combine(Path.GetTempPath(), "xplat-remote-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(remote);
        try
        {
            GitProcess.Run(remote, "init", "--bare", "-q");
            string branch = _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim();
            _repo.Run("remote", "add", "origin", remote);
            _repo.Run("push", "-q", "-u", "origin", branch);
            _repo.Run("commit", "-q", "--amend", "-m", "rewritten");
            MainWindow window = NewWindow();
            window.Show();
            Open(window, _repo.Path);
            WaitUntil(() => Find<MenuItem>(window, "PushDialogMenuItem").IsEnabled);

            Click(window, "PushDialogMenuItem");
            WaitUntil(() => window.OwnedWindows.OfType<PushWindow>().Any());
            PushWindow dialog = window.OwnedWindows.OfType<PushWindow>().Single();
            Find<ComboBox>(dialog, "RemoteBox").SelectedItem.Should().Be("origin");
            Find<ComboBox>(dialog, "LocalBranchBox").SelectedItem.Should().Be(branch);
            Find<RadioButton>(dialog, "ForceWithLeaseRadio").IsChecked = true;
            Click(dialog, "PushButton");

            WaitUntil(() => Find<TextBlock>(window, "OperationStatusText").Text == "Pushed");
            GitProcess.Run(remote, "log", "-1", "--format=%s", branch).Trim().Should().Be("rewritten");
        }
        finally
        {
            GitProcess.DeleteFolder(remote);
        }
    }

    [AvaloniaTest]
    public void Staging_and_committing_from_the_commit_window_adds_a_commit()
    {
        TestAppBuilder.Preferences.CloseCommitDialogAfterCommit = false;
        File.WriteAllText(Path.Combine(_repo.Path, "new.txt"), "content");
        MainWindow window = NewWindow();
        window.Show();
        Open(window, _repo.Path);
        CommitWindow commit = OpenCommitWindow(window);
        ListBox unstaged = Find<ListBox>(commit, "UnstagedList");
        WaitUntil(() => unstaged.ItemCount == 1);

        unstaged.SelectedIndex = 0;
        Click(commit, "StageButton");
        WaitUntil(() => Find<TextBlock>(window, "OperationStatusText").Text == "Staged");
        WaitUntil(() => Find<ListBox>(commit, "StagedList").ItemCount == 1);

        Find<TextBox>(commit, "CommitMessageBox").Text = "add new";
        WaitUntil(() => Find<Button>(commit, "CommitButton").IsEnabled);
        Click(commit, "CommitButton");
        WaitUntil(() => Find<TextBlock>(window, "OperationStatusText").Text == "Committed");

        _repo.Run("log", "-1", "--format=%s").Trim().Should().Be("add new");
        Find<TextBox>(commit, "CommitMessageBox").Text.Should().BeEmpty();
        window.OwnedWindows.OfType<CommitWindow>().Should().ContainSingle("the preference keeps the window open");
        commit.Close();
    }

    [AvaloniaTest]
    public void A_message_typed_in_a_closed_commit_window_is_offered_again()
    {
        MainWindow window = NewWindow();
        window.Show();
        Open(window, _repo.Path);
        CommitWindow first = OpenCommitWindow(window);
        Find<TextBox>(first, "CommitMessageBox").Text = "half written";
        first.Close();

        CommitWindow second = OpenCommitWindow(window);

        WaitUntil(() => Find<TextBox>(second, "CommitMessageBox").Text == "half written");
        second.Close();
    }

    [AvaloniaTest]
    public void The_draft_is_kept_where_upstream_keeps_it_and_a_stopped_merge_starts_from_git_s_message()
    {
        string baseBranch = _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim();
        _repo.Run("checkout", "-q", "-b", "other");
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "theirs");
        _repo.Run("commit", "-q", "-am", "theirs");
        _repo.Run("checkout", "-q", baseBranch);
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "ours");
        _repo.Run("commit", "-q", "-am", "ours");
        MainWindow window = NewWindow();
        window.Show();
        Open(window, _repo.Path);
        CommitWindow first = OpenCommitWindow(window);
        Find<TextBox>(first, "CommitMessageBox").Text = "draft text";
        first.Close();
        WaitUntil(() => first.SavedDraft.IsCompleted);
        File.ReadAllText(Path.Combine(_repo.Path, ".git", "COMMITMESSAGE")).Should().Be("draft text");

        GitProcess.RunAllowingFailure(_repo.Path, "merge", "other");
        Click(window, "RefreshButton");
        WaitUntil(() => Find<Border>(window, "StateBanner").IsVisible);
        CommitWindow second = OpenCommitWindow(window);

        WaitUntil(() =>
            (Find<TextBox>(second, "CommitMessageBox").Text ?? "").StartsWith("Merge branch 'other'",
                StringComparison.Ordinal));
        second.Close();
    }

    [AvaloniaTest]
    public void Conflicts_window_resolves_a_conflict_with_their_version()
    {
        string baseBranch = _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim();
        _repo.Run("checkout", "-q", "-b", "other");
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "theirs");
        _repo.Run("commit", "-q", "-am", "theirs");
        _repo.Run("checkout", "-q", baseBranch);
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "ours");
        _repo.Run("commit", "-q", "-am", "ours");
        GitProcess.RunAllowingFailure(_repo.Path, "merge", "other");
        MainWindow window = NewWindow();
        window.Show();
        Open(window, _repo.Path);
        WaitUntil(() => Find<Border>(window, "StateBanner").IsVisible);

        Click(window, "ResolveConflictsButton");
        WaitUntil(() => window.OwnedWindows.OfType<ConflictsWindow>().Any());
        ConflictsWindow conflicts = window.OwnedWindows.OfType<ConflictsWindow>().Single();
        conflicts.Conflicts.Should().Equal("a.txt");
        Find<ListBox>(conflicts, "ConflictList").SelectedIndex = 0;
        Click(conflicts, "UseTheirsButton");

        WaitUntil(() => conflicts.Conflicts.Count == 0);
        File.ReadAllText(Path.Combine(_repo.Path, "a.txt")).Should().Be("theirs");
        Find<TextBlock>(conflicts, "DoneText").Text.Should().Contain("Commit to complete the merge");
        conflicts.Close();
    }

    [AvaloniaTest]
    public void Ignore_in_the_commit_window_adds_the_untracked_file_to_gitignore()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "notes.txt"), "scratch");
        MainWindow window = NewWindow();
        window.Show();
        Open(window, _repo.Path);
        CommitWindow commit = OpenCommitWindow(window);
        ListBox unstaged = Find<ListBox>(commit, "UnstagedList");
        WaitUntil(() => unstaged.ItemCount == 1);
        unstaged.SelectedIndex = 0;

        Click(commit, "IgnoreButton");
        WaitUntil(() => commit.OwnedWindows.OfType<GitIgnoreWindow>().Any());
        GitIgnoreWindow ignore = commit.OwnedWindows.OfType<GitIgnoreWindow>().Single();
        WaitUntil(() => Find<Button>(ignore, "SaveButton").IsEnabled);
        Find<TextBox>(ignore, "ContentBox").Text.Should().Be("/notes.txt\n");
        Click(ignore, "SaveButton");

        WaitUntil(() =>
            unstaged.ItemCount == 1 && ((IReadOnlyList<FileChange>)unstaged.ItemsSource!)[0].Path == ".gitignore");
        File.ReadAllText(Path.Combine(_repo.Path, ".gitignore")).Should().Be("/notes.txt\n");
        commit.Close();
    }

    [AvaloniaTest]
    public void Rename_remote_asks_for_the_new_name_and_renames_it()
    {
        _repo.Run("remote", "add", "old", Path.GetTempPath());
        MainWindow window = NewWindow();
        window.Show();
        Open(window, _repo.Path);
        ListBox remotes = Find<ListBox>(window, "RemoteList");
        WaitUntil(() => remotes.ItemCount == 1);
        remotes.SelectedIndex = 0;

        Click(window, "RenameRemoteButton");
        WaitUntil(() => window.OwnedWindows.OfType<PromptWindow>().Any());
        PromptWindow prompt = window.OwnedWindows.OfType<PromptWindow>().Single();
        Find<TextBox>(prompt, "ValueBox").Text.Should().Be("old");
        Find<TextBox>(prompt, "ValueBox").Text = "renamed";
        Click(prompt, "OkButton");

        WaitUntil(() => Find<TextBlock>(window, "OperationStatusText").Text == "Renamed remote old to renamed");
        _repo.Run("remote").Trim().Should().Be("renamed");
    }

    [AvaloniaTest]
    public void Activating_the_window_picks_up_a_file_changed_outside_the_app()
    {
        MainWindow window = NewWindow();
        window.Show();
        Open(window, _repo.Path);
        Button commit = Find<Button>(window, "CommitDialogButton");
        commit.Content.Should().Be("Commit");
        Thread.Sleep(TimeSpan.FromSeconds(2.1));

        Window other = new();
        other.Show();
        other.Activate();
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "edited elsewhere");
        window.Activate();
        other.Close();

        WaitUntil(() => commit.Content as string == "Commit (1)");
    }

    [AvaloniaTest]
    public void Worktrees_window_adds_and_removes_a_worktree_but_never_offers_the_main_one()
    {
        string folder = Path.Combine(Path.GetTempPath(), "xplat-worktree-" + Guid.NewGuid().ToString("N"));
        try
        {
            MainWindow window = NewWindow();
            window.Show();
            Open(window, _repo.Path);

            Click(window, "WorktreesMenuItem");
            WaitUntil(() => window.OwnedWindows.OfType<WorktreesWindow>().Any());
            WorktreesWindow worktrees = window.OwnedWindows.OfType<WorktreesWindow>().Single();
            WaitUntil(() => worktrees.Worktrees.Count == 1);
            ListBox list = Find<ListBox>(worktrees, "WorktreeList");
            list.SelectedIndex = 0;
            Find<Button>(worktrees, "RemoveWorktreeButton").IsEnabled.Should().BeFalse();

            Click(worktrees, "AddWorktreeButton");
            WaitUntil(() => worktrees.OwnedWindows.OfType<PromptWindow>().Any());
            PromptWindow prompt = worktrees.OwnedWindows.OfType<PromptWindow>().Single();
            Find<TextBox>(prompt, "ValueBox").Text = folder;
            Find<TextBox>(prompt, "SecondValueBox").Text = "wt-branch";
            Click(prompt, "OkButton");

            WaitUntil(() => worktrees.Worktrees.Count == 2);
            worktrees.Worktrees[1].Display.Should().Contain("wt-branch");
            list.SelectedIndex = 1;
            Click(worktrees, "RemoveWorktreeButton");
            WaitUntil(() => worktrees.OwnedWindows.OfType<ConfirmWindow>().Any());
            Click(worktrees.OwnedWindows.OfType<ConfirmWindow>().Single(), "ConfirmButton");

            WaitUntil(() => worktrees.Worktrees.Count == 1);
            Directory.Exists(folder).Should().BeFalse();
            worktrees.Close();
        }
        finally
        {
            GitProcess.DeleteFolder(folder);
        }
    }

    [AvaloniaTest]
    public void Settings_window_saves_the_theme_and_changed_git_config_values()
    {
        MainWindow window = NewWindow();
        window.Show();
        Open(window, _repo.Path);
        TestAppBuilder.GitConfig.Set(ConfigScope.Global, "user.email", "kept@example.com");

        Click(window, "SettingsMenuItem");
        WaitUntil(() => window.OwnedWindows.OfType<SettingsWindow>().Any());
        SettingsWindow settings = window.OwnedWindows.OfType<SettingsWindow>().Single();
        WaitUntil(() => settings.IsGitConfigLoaded);
        Find<TextBox>(settings, "GlobalUserEmailBox").Text.Should().Be("kept@example.com");
        ComboBox themes = Find<ComboBox>(settings, "ThemeBox");
        themes.SelectedItem = themes.Items.OfType<ThemeOption>().Single(option => option.Id == ThemeId.DefaultDark);
        Find<CheckBox>(settings, "ColorblindCheck").IsChecked = true;
        Find<TextBox>(settings, "LocalUserNameBox").Text = "Repo Author";
        Click(settings, "SaveButton");

        WaitUntil(() => !window.OwnedWindows.OfType<SettingsWindow>().Any());
        TestAppBuilder.Preferences.Theme.Should().Be(ThemeId.DefaultDark);
        TestAppBuilder.Preferences.ThemeVariations.Should().Equal(ThemeVariations.Colorblind);
        TestAppBuilder.GitConfig.Get(ConfigScope.Local, "user.name").Should().Be("Repo Author");
        TestAppBuilder.GitConfig.Get(ConfigScope.Global, "user.email").Should().Be("kept@example.com");
        WaitUntil(() => Avalonia.Application.Current!.RequestedThemeVariant == Avalonia.Styling.ThemeVariant.Dark);

        // The colors come from upstream's dark.css with its colorblind variation (.RemoteBranch.colorblind).
        object? remoteLabel = RefLabelBrushConverter.Instance.Convert(RefKind.RemoteBranch, typeof(object), null,
            System.Globalization.CultureInfo.InvariantCulture);
        remoteLabel.Should().BeAssignableTo<Avalonia.Media.ISolidColorBrush>()
            .Which.Color.Should().Be(Avalonia.Media.Color.Parse("#0080ff"));

        // Back to the default theme, so the colors of the next tests do not depend on this one.
        TestAppBuilder.Preferences.Theme = ThemeId.DefaultLight;
        TestAppBuilder.Preferences.ThemeVariations = [];
        Click(window, "SettingsMenuItem");
        WaitUntil(() => window.OwnedWindows.OfType<SettingsWindow>().Any());
        Click(window.OwnedWindows.OfType<SettingsWindow>().Single(), "SaveButton");
        WaitUntil(() => Avalonia.Application.Current!.RequestedThemeVariant == Avalonia.Styling.ThemeVariant.Light);
    }

    [AvaloniaTest]
    public void Settings_lists_upstreams_theme_files_and_follows_the_system_first()
    {
        MainWindow window = NewWindow();
        window.Show();
        Click(window, "SettingsMenuItem");
        WaitUntil(() => window.OwnedWindows.OfType<SettingsWindow>().Any());
        SettingsWindow settings = window.OwnedWindows.OfType<SettingsWindow>().Single();

        List<ThemeId> ids =
            [.. Find<ComboBox>(settings, "ThemeBox").Items.OfType<ThemeOption>().Select(option => option.Id)];

        ids[0].Should().Be(ThemeId.WindowsAppColorModeId);
        ids.Should().Contain([
            ThemeId.DefaultLight, ThemeId.DefaultDark, new ThemeId("dark+", isBuiltin: true),
            new ThemeId("light+", isBuiltin: true)
        ]);
        Find<ComboBox>(settings, "ThemeBox").SelectedItem.Should().Be(new ThemeOption(ThemeId.DefaultLight));
        settings.Close();
    }

    [AvaloniaTest]
    public void Settings_offers_the_tools_found_on_this_computer_and_a_choice_fills_the_global_value()
    {
        MainWindow window = NewWindow();
        window.Show();
        Click(window, "SettingsMenuItem");
        WaitUntil(() => window.OwnedWindows.OfType<SettingsWindow>().Any());
        SettingsWindow settings = window.OwnedWindows.OfType<SettingsWindow>().Single();
        ComboBox mergeChoices = Find<ComboBox>(settings, "MergeToolChoices");
        ComboBox diffChoices = Find<ComboBox>(settings, "DiffToolChoices");
        WaitUntil(() => mergeChoices.ItemCount == 2 && diffChoices.ItemCount == 2);

        mergeChoices.SelectedItem = "kdiff3";
        diffChoices.SelectedItem = "vscode";
        Click(settings, "SaveButton");

        WaitUntil(() => !window.OwnedWindows.OfType<SettingsWindow>().Any());
        TestAppBuilder.GitConfig.Get(ConfigScope.Global, "merge.tool").Should().Be("kdiff3");
        TestAppBuilder.GitConfig.Get(ConfigScope.Global, "diff.tool").Should().Be("vscode");
        TestAppBuilder.GitConfig.Clear();
    }

    [AvaloniaTest]
    public void A_hotkey_changed_in_settings_is_stored_in_the_upstream_setting_and_used_at_once()
    {
        MainWindow window = NewWindow();
        window.Show();
        Open(window, _repo.Path);
        Click(window, "SettingsMenuItem");
        WaitUntil(() => window.OwnedWindows.OfType<SettingsWindow>().Any());
        SettingsWindow settings = window.OwnedWindows.OfType<SettingsWindow>().Single();
        WaitUntil(() => settings.IsGitConfigLoaded);

        settings.HotkeyBoxes[BrowseCommand.Commit].RaiseEvent(new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent, Key = Key.K, KeyModifiers = Hotkeys.CommandModifier
        });
        Click(settings, "SaveButton");
        WaitUntil(() => !window.OwnedWindows.OfType<SettingsWindow>().Any());

        TestAppBuilder.Preferences.SerializedHotkeys.Should().Contain("Name=\"Commit\"").And
            .Contain("KeyData=\"K Control\"");
        window.RaiseEvent(new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent, Key = Key.K, KeyModifiers = Hotkeys.CommandModifier
        });
        WaitUntil(() => window.OwnedWindows.OfType<CommitWindow>().Any());
        window.OwnedWindows.OfType<CommitWindow>().Single().Close();
    }

    [AvaloniaTest]
    public void A_commit_window_hotkey_is_stored_in_upstreams_Commit_section_and_used_by_the_commit_window()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "changed");
        MainWindow window = NewWindow();
        window.Show();
        Open(window, _repo.Path);
        Click(window, "SettingsMenuItem");
        WaitUntil(() => window.OwnedWindows.OfType<SettingsWindow>().Any());
        SettingsWindow settings = window.OwnedWindows.OfType<SettingsWindow>().Single();
        WaitUntil(() => settings.IsGitConfigLoaded);

        settings.CommitHotkeyBoxes[CommitCommand.StageAll].RaiseEvent(new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent, Key = Key.J, KeyModifiers = Hotkeys.CommandModifier
        });
        Click(settings, "SaveButton");
        WaitUntil(() => !window.OwnedWindows.OfType<SettingsWindow>().Any());

        UpstreamHotkeys.Read(TestAppBuilder.Preferences.SerializedHotkeys, "Commit")["StageAll"].KeyData.Should()
            .Be(System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.J);
        CommitWindow commit = OpenCommitWindow(window);
        ListBox staged = Find<ListBox>(commit, "StagedList");
        WaitUntil(() => Find<ListBox>(commit, "UnstagedList").ItemCount == 1);
        commit.RaiseEvent(new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent, Key = Key.J, KeyModifiers = Hotkeys.CommandModifier
        });
        WaitUntil(() => staged.ItemCount == 1);
        commit.Close();
    }

    [AvaloniaTest]
    public void Revision_grid_hotkeys_move_to_parent_and_child_and_toggle_the_merge_filter()
    {
        MainWindow window = NewWindow();
        window.Show();
        Open(window, _repo.Path);
        ListBox grid = Find<ListBox>(window, "CommitList");
        WaitUntil(() => grid.ItemCount == 2);
        grid.SelectedIndex = 0;

        PressIn(grid, Key.P, Hotkeys.CommandModifier);
        grid.SelectedIndex.Should().Be(1, "Ctrl+P goes to the parent");
        PressIn(grid, Key.N, Hotkeys.CommandModifier);
        grid.SelectedIndex.Should().Be(0, "Ctrl+N goes to the child");
        PressIn(grid, Key.M, Hotkeys.CommandModifier | KeyModifiers.Shift);

        WaitUntil(() => Find<Button>(window, "RevisionFilterButton").Content as string == "Filter (on)...");
    }

    [AvaloniaTest]
    public void Pressing_S_in_the_commit_windows_diff_stages_the_selected_lines()
    {
        string file = Path.Combine(_repo.Path, "lines.txt");
        File.WriteAllLines(file, Enumerable.Range(1, 5).Select(i => $"line {i}"));
        MainWindow window = NewWindow();
        window.Show();
        Open(window, _repo.Path);
        CommitWindow commit = OpenCommitWindow(window);
        ListBox unstaged = Find<ListBox>(commit, "UnstagedList");
        WaitUntil(() => unstaged.ItemCount == 1);
        unstaged.SelectedIndex = 0;
        DiffView diff = Find<DiffView>(commit, "Diff");
        WaitUntil(() => diff.Lines.Any(line => line.Text == "+line 5"));
        SelectDiffLines(diff, "+line 1");
        WaitUntil(() => Find<Button>(commit, "StageLinesButton").IsEnabled);

        PressIn(diff.FindControl<ListBox>("DiffList")!, Key.S, KeyModifiers.None);

        WaitUntil(() => Find<TextBlock>(window, "OperationStatusText").Text == "Staged selected lines");
        _repo.Run("show", ":lines.txt").ReplaceLineEndings("\n").Should().Be("line 1\n");
        commit.Close();
    }

    [AvaloniaTest]
    public void A_user_menu_bar_script_runs_git_with_the_selected_commit()
    {
        TestAppBuilder.Scripts.Save(
        [
            new ScriptDefinition
            {
                Name = "&Tag it",
                Command = "git",
                Arguments = "tag from-script {sHash}",
                OnEvent = ScriptEvent.ShowInUserMenuBar
            },
            new ScriptDefinition
            {
                Name = "Disabled", Command = "git", OnEvent = ScriptEvent.ShowInUserMenuBar, Enabled = false
            },
        ]);
        MainWindow window = NewWindow();
        window.Show();
        Open(window, _repo.Path);
        ListBox grid = Find<ListBox>(window, "CommitList");
        WaitUntil(() => grid.ItemCount == 2);
        grid.SelectedIndex = 1;
        StackPanel scripts = Find<StackPanel>(window, "UserScriptsPanel");
        scripts.Children.OfType<Button>().Select(button => button.Content).Should().Equal("Tag it");

        scripts.Children.OfType<Button>().Single().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        WaitUntil(() => _repo.Run("tag", "--list").Contains("from-script", StringComparison.Ordinal));
        _repo.Run("rev-parse", "from-script").Trim().Should().Be(_repo.Run("rev-parse", "HEAD~1").Trim());
    }

    [AvaloniaTest]
    public void The_grid_menu_lists_the_grid_scripts_and_an_after_commit_script_runs_after_a_commit()
    {
        TestAppBuilder.Scripts.Save(
        [
            new ScriptDefinition
            {
                Name = "In grid", Command = "git", Arguments = "status", AddToRevisionGridContextMenu = true
            },
            new ScriptDefinition
            {
                Name = "After commit",
                Command = "git",
                Arguments = "tag after-commit",
                OnEvent = ScriptEvent.AfterCommit
            },
        ]);
        File.WriteAllText(Path.Combine(_repo.Path, "new.txt"), "content");
        _repo.Run("add", "new.txt");
        MainWindow window = NewWindow();
        window.Show();
        Open(window, _repo.Path);
        MenuItem runScript = Find<MenuItem>(window, "RunScriptMenuItem");
        runScript.IsVisible.Should().BeTrue();
        runScript.ItemsSource.Should().BeAssignableTo<IEnumerable<MenuItem>>()
            .Which.Select(item => item.Header).Should().Equal("In grid");

        CommitWindow commit = OpenCommitWindow(window);
        Find<TextBox>(commit, "CommitMessageBox").Text = "with a script";
        Click(commit, "CommitButton");

        WaitUntil(() => _repo.Run("tag", "--list").Contains("after-commit", StringComparison.Ordinal));
        _repo.Run("rev-parse", "after-commit").Trim().Should().Be(_repo.Run("rev-parse", "HEAD").Trim());
    }

    [AvaloniaTest]
    public void Scripts_edited_in_settings_are_stored_on_OK_and_shown_in_the_user_menu_bar()
    {
        TestAppBuilder.Scripts.Save([
            new ScriptDefinition { Name = "Kept", Command = "git", HotkeyCommandIdentifier = 9000 }
        ]);
        MainWindow window = NewWindow();
        window.Show();
        Open(window, _repo.Path);
        SettingsWindow settings = OpenSettings(window);
        ListBox list = Find<ListBox>(settings, "ScriptsList");
        list.SelectedIndex.Should().Be(0);
        Find<TextBox>(settings, "ScriptNameBox").Text.Should().Be("Kept");

        Click(settings, "AddScriptButton");
        Find<TextBox>(settings, "ScriptNameBox").Text = "&Status";
        Find<TextBox>(settings, "ScriptCommandBox").Text = "git";
        Find<TextBox>(settings, "ScriptArgumentsBox").Text = "status";
        Find<ComboBox>(settings, "ScriptEventBox").SelectedItem = ScriptEvent.ShowInUserMenuBar;
        Click(settings, "MoveScriptUpButton");
        Click(settings, "SaveButton");
        WaitUntil(() => !window.OwnedWindows.OfType<SettingsWindow>().Any());

        TestAppBuilder.Scripts.Load().Select(script => (script.Name, script.HotkeyCommandIdentifier)).Should()
            .Equal(("&Status", 9001), ("Kept", 9000));
        TestAppBuilder.Scripts.Load()[0].Should().BeEquivalentTo(new ScriptDefinition
        {
            Name = "&Status",
            Command = "git",
            Arguments = "status",
            OnEvent = ScriptEvent.ShowInUserMenuBar,
            Enabled = true,
            HotkeyCommandIdentifier = 9001
        });
        StackPanel userScripts = Find<StackPanel>(window, "UserScriptsPanel");
        WaitUntil(() => userScripts.Children.Count > 0);
        userScripts.Children.OfType<Button>().Select(button => button.Content).Should().Equal("Status");
    }

    [AvaloniaTest]
    public void Script_edits_are_dropped_when_settings_is_cancelled()
    {
        TestAppBuilder.Scripts.Save([new ScriptDefinition { Name = "Kept", Command = "git" }]);
        MainWindow window = NewWindow();
        window.Show();
        SettingsWindow settings = OpenSettings(window);

        Find<TextBox>(settings, "ScriptNameBox").Text = "Changed";
        Click(settings, "DeleteScriptButton");
        Click(settings, "CancelButton");
        WaitUntil(() => !window.OwnedWindows.OfType<SettingsWindow>().Any());

        TestAppBuilder.Scripts.Load().Should().ContainSingle().Which.Name.Should().Be("Kept");
    }

    [AvaloniaTest]
    public void A_script_hotkey_is_stored_in_upstreams_Scripts_section_and_runs_the_script()
    {
        TestAppBuilder.Scripts.Save(
        [
            new ScriptDefinition
            {
                Name = "Tag by key",
                Command = "git",
                Arguments = "tag from-hotkey",
                HotkeyCommandIdentifier = 9003,
                Enabled = false
            },
        ]);
        MainWindow window = NewWindow();
        window.Show();
        Open(window, _repo.Path);
        SettingsWindow settings = OpenSettings(window);

        settings.ScriptHotkeyBoxes[9003]
            .RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.F7 });
        Click(settings, "SaveButton");
        WaitUntil(() => !window.OwnedWindows.OfType<SettingsWindow>().Any());

        UpstreamHotkeys.ReadByCode(TestAppBuilder.Preferences.SerializedHotkeys, "Scripts")[9003].Should()
            .Be(new UpstreamHotkey(9003, "Tag by key", System.Windows.Forms.Keys.F7));

        // From the filter box too: a function key is not text. As upstream, a disabled script still runs from its hotkey.
        Find<TextBox>(window, "FilterBox")
            .RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.F7 });

        WaitUntil(() => _repo.Run("tag", "--list").Contains("from-hotkey", StringComparison.Ordinal));
    }

    [AvaloniaTest]
    public void Command_log_lists_the_git_commands_the_window_ran()
    {
        MainWindow window = NewWindow();
        window.Show();
        Open(window, _repo.Path);

        Click(window, "CommandLogMenuItem");
        WaitUntil(() => window.OwnedWindows.OfType<CommandLogWindow>().Any());
        CommandLogWindow log = window.OwnedWindows.OfType<CommandLogWindow>().Single();

        log.Entries.Should().Contain(entry => entry.Arguments.Contains("for-each-ref", StringComparison.Ordinal));
        Find<ListBox>(log, "EntryList").SelectedIndex = 0;
        Find<SelectableTextBlock>(log, "DetailText").Text.Should().Contain("Arguments:");
        log.Close();
    }

    [AvaloniaTest]
    public void Selecting_a_commit_shows_its_details()
    {
        MainWindow window = NewWindow();
        Open(window, _repo.Path);
        ListBox list = Find<ListBox>(window, "CommitList");

        list.SelectedIndex = 0;
        WaitUntil(() => Find<SelectableTextBlock>(window, "DetailMessage").Text == "second\n\nbody line");

        Find<TextBlock>(window, "DetailHash").Text.Should().Be(_repo.Run("rev-parse", "HEAD").Trim());
        Find<TextBlock>(window, "DetailAuthor").Text.Should().Be("Test <test@example.com>");
        Find<TextBlock>(window, "DetailParents").Text.Should().StartWith("Parents: ");
    }

    [AvaloniaTest]
    public void Selecting_the_root_commit_shows_no_parents()
    {
        MainWindow window = NewWindow();
        Open(window, _repo.Path);
        ListBox list = Find<ListBox>(window, "CommitList");

        list.SelectedIndex = 1;
        WaitUntil(() => Find<TextBlock>(window, "DetailParents").Text == "No parents");

        Find<SelectableTextBlock>(window, "DetailMessage").Text.Should().Be("first");
    }

    // The tree node of a branch by its full name, e.g. "team/external" or "origin/main".
    private static BranchTreeNode? BranchNode(TreeView tree, string name)
        => (tree.ItemsSource as IReadOnlyList<BranchTreeNode>)?
            .SelectMany(root => root.Descendants())
            .FirstOrDefault(node => node.Branch?.Name == name);

    private static MainWindow NewWindow() =>
        new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));

    private static void Open(MainWindow window, string path)
    {
        Find<TextBox>(window, "PathBox").Text = path;
        Find<Button>(window, "OpenButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        WaitUntil(() => Find<Button>(window, "OpenButton").IsEnabled);
    }

    private static CommitWindow OpenCommitWindow(MainWindow window)
    {
        WaitUntil(() => Find<Button>(window, "CommitDialogButton").IsEnabled);
        Click(window, "CommitDialogButton");
        WaitUntil(() => window.OwnedWindows.OfType<CommitWindow>().Any());
        return window.OwnedWindows.OfType<CommitWindow>().Single();
    }

    // A key pressed while the control has the focus: the event starts at the control, as a real key press does.
    private static void PressIn(Control control, Key key, KeyModifiers modifiers)
    {
        control.RaiseEvent(new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent, Key = key, KeyModifiers = modifiers
        });
        Dispatcher.UIThread.RunJobs();
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

    private static void Click(Window window, string name)
    {
        Control control = Find<Control>(window, name);
        RoutedEvent click = control is MenuItem ? MenuItem.ClickEvent : Button.ClickEvent;
        control.RaiseEvent(new RoutedEventArgs(click));
    }

    private static SettingsWindow OpenSettings(MainWindow window)
    {
        Click(window, "SettingsMenuItem");
        WaitUntil(() => window.OwnedWindows.OfType<SettingsWindow>().Any());
        SettingsWindow settings = window.OwnedWindows.OfType<SettingsWindow>().Single();
        WaitUntil(() => settings.IsGitConfigLoaded);
        return settings;
    }

    private static T Find<T>(Window window, string name) where T : Control
        => window.FindControl<T>(name) ?? throw new InvalidOperationException($"Control {name} not found");

    // Background work posts its results back to the UI thread; run those jobs until the condition holds.
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

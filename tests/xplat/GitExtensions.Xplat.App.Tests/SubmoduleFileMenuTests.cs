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
///  Upstream FileStatusList's submodule items in the Diff tab's working directory list.
/// </summary>
internal sealed class SubmoduleFileMenuTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private TestRepository _repo = null!;
    private TestRepository _library = null!;

    [SetUp]
    public void SetUp()
    {
        _library = new TestRepository();
        _repo = new TestRepository();
        _repo.Run("-c", "protocol.file.allow=always", "submodule", "add", "-q", _library.Path, "lib");
        _repo.Run("commit", "-q", "-m", "add submodule");

        // A change in the submodule's working directory makes it a changed entry of the superproject's.
        File.WriteAllText(Path.Combine(_repo.Path, "lib", "a.txt"), "dirty");
        TestAppBuilder.Preferences.RevisionGraphShowArtificialCommits = true;
    }

    [TearDown]
    public void TearDown()
    {
        TestAppBuilder.Preferences.RevisionGraphShowArtificialCommits = false;
        _repo.Dispose();
        _library.Dispose();
    }

    [AvaloniaTest]
    public void A_changed_submodule_gets_upstreams_submodule_items_and_stash_stashes_its_changes()
    {
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        window.Show();
        window.OpenRepository(_repo.Path);
        WaitUntil(() => !window.IsBusy);
        Find<TabControl>(window, "DetailTabs").SelectedItem = Find<TabItem>(window, "DiffTab");
        ListBox commits = Find<ListBox>(window, "CommitList");
        WaitUntil(() => commits.Items.Cast<CommitListItem>().Any(item => item.Row.Hash == ArtificialCommits.WorkTreeHash));
        commits.SelectedItem = commits.Items.Cast<CommitListItem>().First(item => item.Row.Hash == ArtificialCommits.WorkTreeHash);
        ListBox files = Find<ListBox>(window, "CommitFilesList");
        WaitUntil(() => files.Items.OfType<CommitFile>().Any(file => file.Path == "lib"));
        CommitFile submodule = files.Items.OfType<CommitFile>().Single(file => file.Path == "lib");
        submodule.IsSubmodule.Should().BeTrue("git's raw diff gives the submodule's mode");
        files.SelectedItem = submodule;

        // As in the commit window's test: the viewer's diff of the submodule may hold its index lock.
        DiffView diff = Find<DiffView>(window, "CommitDiff");
        WaitUntil(() => !window.IsBusy && !diff.IsLoading && diff.Lines.Count > 0);
        files.RaiseEvent(new ContextRequestedEventArgs());
        Dispatcher.UIThread.RunJobs();
        files.ContextMenu!.Close();
        MenuItem Item(string name) => Find<MenuItem>(files.ContextMenu, name);
        Item("openSubmoduleMenuItem").IsVisible.Should().BeTrue();
        foreach (string name in new[] { "tsmiUpdateSubmodule", "tsmiResetSubmoduleChanges", "tsmiStashSubmoduleChanges", "tsmiCommitSubmoduleChanges" })
        {
            Item(name).IsVisible.Should().BeTrue(name);
        }

        Item("tsmiStashSubmoduleChanges").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

        WaitUntil(() => ErrorShown(window) is null
            ? GitProcess.Run(Path.Combine(_repo.Path, "lib"), "stash", "list").Length > 0
              && ReadWritten(Path.Combine(_repo.Path, "lib", "a.txt")) is { } text && text != "dirty"
            : throw new AssertionException("The stash failed: " + ErrorShown(window)));
        window.Close();
    }

    [AvaloniaTest]
    public void The_viewer_shows_a_dirty_submodule_as_upstreams_status_not_gits_lines()
    {
        DiffView diff = new();
        Window host = new() { Content = diff };
        host.Show();

        Task show = diff.ShowAsync(_repo.Path, commitHash: null, "lib", staged: false, "lib");
        WaitUntil(() => show.IsCompleted && diff.Lines.Count > 0);

        diff.Lines.Select(line => line.Text).Should().Contain("Submodule lib");
        diff.Lines.Should().NotContain(line => line.Text.Contains("Subproject commit", StringComparison.Ordinal));
        diff.Lines.Should().Contain(line => line.Text.Contains("dirty", StringComparison.OrdinalIgnoreCase));
        diff.SelectedRange().Should().BeNull("no hunks, so no line patching");
        host.Close();
    }

    [AvaloniaTest]
    public void The_viewer_shows_a_submodule_moved_by_a_commit_with_its_two_commits()
    {
        string library = Path.Combine(_repo.Path, "lib");
        string before = GitProcess.Run(library, "rev-parse", "HEAD").Trim();
        GitProcess.Run(library, "config", "user.name", "Test");
        GitProcess.Run(library, "config", "user.email", "test@example.com");
        GitProcess.Run(library, "commit", "-q", "-am", "moved on");
        string after = GitProcess.Run(library, "rev-parse", "HEAD").Trim();
        _repo.Run("add", "lib");
        _repo.Run("commit", "-q", "-m", "move the submodule");
        DiffView diff = new();
        Window host = new() { Content = diff };
        host.Show();

        Task show = diff.ShowAsync(_repo.Path, "HEAD", "lib", staged: false, "lib");
        WaitUntil(() => show.IsCompleted && diff.Lines.Count > 0);

        diff.Lines.Should().Contain(line => line.Text == $"From:\t{before}");
        diff.Lines.Should().Contain(line => line.Text.Contains(after, StringComparison.Ordinal));
        diff.Lines.Should().Contain(line => line.Text.Contains("moved on", StringComparison.Ordinal));

        Task entry = diff.ShowFileAsync(_repo.Path, "HEAD", staged: false, "lib");
        WaitUntil(() => entry.IsCompleted);
        diff.Lines.Select(line => line.Text).Should().Contain("Submodule lib");
        diff.Lines.Should().Contain(line => line.Text.Contains(after, StringComparison.Ordinal),
            "a tree's entry names the commit");

        Task folder = diff.ShowFileAsync(_repo.Path, commitHash: null, staged: false, "lib");
        WaitUntil(() => folder.IsCompleted);
        diff.Lines.Select(line => line.Text).Should().Contain("Submodule lib");
        host.Close();
    }

    [AvaloniaTest]
    public void The_file_tree_tab_offers_the_submodule_items_for_a_submodule_only()
    {
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        window.Show();
        window.OpenRepository(_repo.Path);
        WaitUntil(() => !window.IsBusy);
        Find<TabControl>(window, "DetailTabs").SelectedItem = Find<TabItem>(window, "FileTreeTab");
        TreeView tree = Find<TreeView>(window, "FileTreeView");
        WaitUntil(() => tree.Items.OfType<FileTreeNode>().Any(node => node.Path == "lib"));

        tree.SelectedItem = tree.Items.OfType<FileTreeNode>().Single(node => node.Path == "lib");
        Dispatcher.UIThread.RunJobs();
        tree.RaiseEvent(new ContextRequestedEventArgs());
        Dispatcher.UIThread.RunJobs();
        tree.ContextMenu!.Close();
        Find<MenuItem>(tree.ContextMenu!, "openSubmoduleMenuItem").IsVisible.Should().BeTrue();

        tree.SelectedItem = tree.Items.OfType<FileTreeNode>().Single(node => node.Path == "a.txt");
        Dispatcher.UIThread.RunJobs();
        tree.RaiseEvent(new ContextRequestedEventArgs());
        Dispatcher.UIThread.RunJobs();
        tree.ContextMenu!.Close();
        Find<MenuItem>(tree.ContextMenu!, "openSubmoduleMenuItem").IsVisible.Should().BeFalse();
        window.Close();
    }

    [AvaloniaTest]
    public void With_the_setting_a_double_click_opens_the_submodule_and_its_item_is_bold()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "lib", "a.txt"), "moved");
        string library = Path.Combine(_repo.Path, "lib");
        GitProcess.Run(library, "config", "user.name", "Test");
        GitProcess.Run(library, "config", "user.email", "test@example.com");
        GitProcess.Run(library, "commit", "-q", "-am", "moved on");
        _repo.Run("add", "lib");
        _repo.Run("commit", "-q", "-m", "move the submodule");
        TestAppBuilder.Preferences.OpenSubmoduleDiffInSeparateWindow = true;
        try
        {
            MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
            window.Show();
            window.OpenRepository(_repo.Path);
            WaitUntil(() => !window.IsBusy);
            Find<TabControl>(window, "DetailTabs").SelectedItem = Find<TabItem>(window, "DiffTab");
            ListBox files = Find<ListBox>(window, "CommitFilesList");
            WaitUntil(() => files.Items.OfType<CommitFile>().Any(file => file.Path == "lib"));
            files.SelectedItem = files.Items.OfType<CommitFile>().Single(file => file.Path == "lib");
            Dispatcher.UIThread.RunJobs();
            files.RaiseEvent(new ContextRequestedEventArgs());
            Dispatcher.UIThread.RunJobs();
            files.ContextMenu!.Close();
            Find<MenuItem>(files.ContextMenu!, "openSubmoduleMenuItem").FontWeight.Should().Be(Avalonia.Media.FontWeight.Bold);
            Find<MenuItem>(files.ContextMenu!, "tsmiFileHistory").FontWeight.Should().Be(Avalonia.Media.FontWeight.Normal);

            // With the folder gone, opening it gives upstream's message, rather than starting another app.
            GitProcess.DeleteFolder(library);
            files.RaiseEvent(new TappedEventArgs(InputElement.DoubleTappedEvent, null!));

            WaitUntil(() => window.OwnedWindows.OfType<MessageBoxWindow>().Any());
            window.OwnedWindows.OfType<MessageBoxWindow>().Single().Close();
            window.OwnedWindows.OfType<FileHistoryWindow>().Should().BeEmpty();
            window.Close();
        }
        finally
        {
            TestAppBuilder.Preferences.OpenSubmoduleDiffInSeparateWindow = false;
        }
    }

    [AvaloniaTest]
    public void A_commit_in_a_submodule_stages_it_in_the_superproject_as_stage_in_superproject_says()
    {
        string library = Path.Combine(_repo.Path, "lib");
        GitProcess.Run(library, "config", "user.name", "Test");
        GitProcess.Run(library, "config", "user.email", "test@example.com");
        bool stage = TestAppBuilder.Preferences.StageInSuperprojectAfterCommit;
        bool close = TestAppBuilder.Preferences.CloseCommitDialogAfterCommit;
        TestAppBuilder.Preferences.CloseCommitDialogAfterCommit = false;
        try
        {
            MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
            window.Show();
            window.OpenRepository(library);
            WaitUntil(() => !window.IsBusy);
            WaitUntil(() => Find<Button>(window, "CommitDialogButton").IsEnabled);

            // Off: the superproject's index stays as it is.
            TestAppBuilder.Preferences.StageInSuperprojectAfterCommit = false;
            CommitInSubmodule(window, library, "not staged");
            _repo.Run("diff", "--cached", "--name-only").Trim().Should().BeEmpty();

            TestAppBuilder.Preferences.StageInSuperprojectAfterCommit = true;
            CommitInSubmodule(window, library, "staged");
            WaitUntil(() => _repo.Run("diff", "--cached", "--name-only").Trim() == "lib");
            window.Close();
        }
        finally
        {
            TestAppBuilder.Preferences.StageInSuperprojectAfterCommit = stage;
            TestAppBuilder.Preferences.CloseCommitDialogAfterCommit = close;
        }

        static void CommitInSubmodule(MainWindow window, string library, string message)
        {
            File.WriteAllText(Path.Combine(library, "a.txt"), message);
            GitProcess.Run(library, "add", "a.txt");
            Find<Button>(window, "RefreshButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            WaitUntil(() => !window.IsBusy && Find<Button>(window, "CommitDialogButton").IsEnabled);
            Find<Button>(window, "CommitDialogButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            WaitUntil(() => window.OwnedWindows.OfType<CommitWindow>().Any());
            CommitWindow commit = window.OwnedWindows.OfType<CommitWindow>().Single();
            CheckBox check = Find<CheckBox>(commit, "StageInSuperprojectCheck");
            WaitUntil(() => check.IsVisible);
            WaitUntil(() => Find<ListBox>(commit, "StagedList").Items.OfType<Core.Repository.FileChange>().Any());
            Find<TextBox>(commit, "CommitMessageBox").Text = message;
            Find<Button>(commit, "CommitButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            WaitUntil(() => GitProcess.Run(library, "log", "-1", "--format=%s").Trim() == message);

            // The window closes after the commit that leaves no unstaged changes, once it has staged in the superproject.
            WaitUntil(() => !window.OwnedWindows.OfType<CommitWindow>().Any() && !window.IsBusy);
        }
    }

    [AvaloniaTest]
    public void The_commit_windows_working_directory_list_has_upstreams_submodule_items()
    {
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        window.Show();
        window.OpenRepository(_repo.Path);
        WaitUntil(() => !window.IsBusy);
        WaitUntil(() => Find<Button>(window, "CommitDialogButton").IsEnabled);
        Find<Button>(window, "CommitDialogButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        WaitUntil(() => window.OwnedWindows.OfType<CommitWindow>().Any());
        CommitWindow commit = window.OwnedWindows.OfType<CommitWindow>().Single();
        ListBox unstaged = Find<ListBox>(commit, "UnstagedList");
        WaitUntil(() => unstaged.Items.OfType<Core.Repository.FileChange>().Any(change => change.Path == "lib"));
        Core.Repository.FileChange submodule = unstaged.Items.OfType<Core.Repository.FileChange>().Single(change => change.Path == "lib");
        submodule.IsSubmodule.Should().BeTrue();
        unstaged.SelectedItem = submodule;

        // The viewer's diff of the submodule runs git status in it, which may hold its index lock, as upstream's viewer
        // does; the stash would fail on it. It runs again when the browse window's status refresh refills the lists.
        DiffView diff = Find<DiffView>(commit, "Diff");
        WaitUntil(() => !window.IsBusy && !diff.IsLoading && diff.Lines.Count > 0);
        unstaged.RaiseEvent(new ContextRequestedEventArgs());
        Dispatcher.UIThread.RunJobs();
        unstaged.ContextMenu!.Close();
        foreach (string name in new[]
                 {
                     "openSubmoduleMenuItem", "tsmiUpdateSubmodule", "tsmiResetSubmoduleChanges",
                     "tsmiStashSubmoduleChanges", "tsmiCommitSubmoduleChanges"
                 })
        {
            Find<MenuItem>(unstaged.ContextMenu!, name).IsVisible.Should().BeTrue(name);
        }

        Find<MenuItem>(unstaged.ContextMenu!, "tsmiStashSubmoduleChanges").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

        WaitUntil(() => ErrorShown(window, commit) is null ? GitProcess.Run(Path.Combine(_repo.Path, "lib"), "stash", "list").Length > 0
            : throw new AssertionException("The stash failed: " + ErrorShown(window, commit)));
        commit.Close();
        window.Close();
    }

    [AvaloniaTest]
    public void The_compare_and_stash_windows_lists_have_upstreams_submodule_items()
    {
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        window.Show();
        window.OpenRepository(_repo.Path);
        WaitUntil(() => !window.IsBusy);
        ListBox commits = Find<ListBox>(window, "CommitList");
        WaitUntil(() => commits.Items.Cast<CommitListItem>().Any(item => item.Row.Subject == "add submodule"));
        commits.SelectedItem = commits.Items.Cast<CommitListItem>().First(item => item.Row.Subject == "add submodule");
        Find<MenuItem>(window, "CompareToWorkingDirectoryMenuItem").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        WaitUntil(() => window.OwnedWindows.OfType<RevisionDiffWindow>().SingleOrDefault()?.DiffLoaded.IsCompleted == true);
        RevisionDiffWindow diff = window.OwnedWindows.OfType<RevisionDiffWindow>().Single();
        ListBox diffFiles = Find<ListBox>(diff, "DiffFiles");
        WaitUntil(() => diffFiles.Items.OfType<Core.Diff.RevisionDiffFile>().Any(file => file.Path == "lib"));
        Core.Diff.RevisionDiffFile diffSubmodule = diffFiles.Items.OfType<Core.Diff.RevisionDiffFile>().Single(file => file.Path == "lib");
        diffSubmodule.IsSubmodule.Should().BeTrue("upstream's diff marks the submodule's entry");
        diffFiles.SelectedItem = diffSubmodule;

        ShouldHaveSubmoduleItems(diffFiles);
        diff.Close();

        Find<MenuItem>(window, "ManageStashesCommandMenuItem").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        WaitUntil(() => window.OwnedWindows.OfType<StashWindow>().SingleOrDefault() is { StashesLoaded.IsCompleted: true, IsLoadingFiles: false });
        StashWindow stash = window.OwnedWindows.OfType<StashWindow>().Single();
        ListBox stashFiles = Find<ListBox>(stash, "FilesList");
        WaitUntil(() => stashFiles.Items.OfType<StashFileRow>().Any(row => row.File?.Change.Path == "lib"));
        stashFiles.SelectedItem = stashFiles.Items.OfType<StashFileRow>().Single(row => row.File?.Change.Path == "lib");

        ShouldHaveSubmoduleItems(stashFiles);
        stash.Close();
        window.Close();
        return;

        static void ShouldHaveSubmoduleItems(ListBox files)
        {
            Dispatcher.UIThread.RunJobs();
            files.RaiseEvent(new ContextRequestedEventArgs());
            Dispatcher.UIThread.RunJobs();
            files.ContextMenu!.Close();
            foreach (string name in new[] { "openSubmoduleMenuItem", "tsmiUpdateSubmodule", "tsmiResetSubmoduleChanges", "tsmiStashSubmoduleChanges", "tsmiCommitSubmoduleChanges" })
            {
                Find<MenuItem>(files.ContextMenu, name).IsVisible.Should().BeTrue(name);
            }
        }
    }

    // Git may still be rewriting the file.
    private static string? ReadWritten(string path)
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

    // The text of an error window an action opened over one of the windows, for a failure's message.
    private static string? ErrorShown(params Window[] owners)
        => owners.SelectMany(owner => owner.OwnedWindows).OfType<ErrorWindow>()
            .Select(error => error.FindControl<TextBlock>("MessageText")?.Text).FirstOrDefault();

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

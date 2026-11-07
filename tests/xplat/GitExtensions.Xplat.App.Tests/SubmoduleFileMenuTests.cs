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

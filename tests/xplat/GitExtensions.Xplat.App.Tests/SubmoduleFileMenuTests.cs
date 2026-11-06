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
        Dispatcher.UIThread.RunJobs();
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

        WaitUntil(() => GitProcess.Run(Path.Combine(_repo.Path, "lib"), "stash", "list").Length > 0
                        && File.ReadAllText(Path.Combine(_repo.Path, "lib", "a.txt")) != "dirty");
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
        unstaged.RaiseEvent(new ContextRequestedEventArgs());
        Dispatcher.UIThread.RunJobs();
        unstaged.ContextMenu!.Close();
        foreach (string name in new[]
                 {
                     "UnstagedOpenSubmoduleMenuItem", "UnstagedUpdateSubmoduleMenuItem", "UnstagedResetSubmoduleMenuItem",
                     "UnstagedStashSubmoduleMenuItem", "UnstagedCommitSubmoduleMenuItem"
                 })
        {
            Find<MenuItem>(commit, name).IsVisible.Should().BeTrue(name);
        }

        Find<MenuItem>(commit, "UnstagedStashSubmoduleMenuItem").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

        WaitUntil(() => GitProcess.Run(Path.Combine(_repo.Path, "lib"), "stash", "list").Length > 0);
        commit.Close();
        window.Close();
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
            Thread.Sleep(10);
        }
    }
}

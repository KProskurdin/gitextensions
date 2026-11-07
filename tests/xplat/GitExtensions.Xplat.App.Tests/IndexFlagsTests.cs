using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using GitCommands;
using GitExtensions.Xplat.Core.Repository;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

/// <summary>
///  Upstream FileStatusList's "Skip worktree" and "Assume unchanged" in the commit window's working directory list.
/// </summary>
internal sealed class IndexFlagsTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private TestRepository _repo = null!;

    [SetUp]
    public void SetUp()
    {
        _repo = new TestRepository();
        File.WriteAllText(Path.Combine(_repo.Path, "config.txt"), "a\n");
        _repo.Run("add", "config.txt");
        _repo.Run("commit", "-q", "-m", "config");
        File.WriteAllText(Path.Combine(_repo.Path, "config.txt"), "local\n");
    }

    [TearDown]
    public void TearDown() => _repo.Dispose();

    [AvaloniaTest]
    public void Skip_worktree_and_assume_unchanged_set_and_clear_gits_flags_of_the_selected_file()
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
        WaitUntil(() => unstaged.Items.OfType<FileChange>().Any(change => change.Path == "config.txt"));
        unstaged.SelectedItem = unstaged.Items.OfType<FileChange>().Single(change => change.Path == "config.txt");
        OpenMenu(unstaged);
        MenuItem skip = unstaged.ContextMenu!.FindControl<MenuItem>("tsmiSkipWorktree")!;
        MenuItem assume = unstaged.ContextMenu!.FindControl<MenuItem>("tsmiAssumeUnchanged")!;
        (skip.IsVisible, skip.IsChecked, assume.IsVisible, assume.IsChecked).Should().Be((true, false, true, false));

        skip.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

        WaitUntil(() => _repo.Run("ls-files", "-v", "config.txt").StartsWith("S ", StringComparison.Ordinal));
        WaitUntil(() => !unstaged.Items.OfType<FileChange>().Any(), "upstream's list hides a skip-worktree file");

        // Upstream's "Show skip-worktree files" lists it again, with the item checked.
        MenuItem showSkipWorktree = Find<MenuItem>(commit, "UnstagedShowSkipWorktreeMenuItem");
        showSkipWorktree.IsChecked = true;
        showSkipWorktree.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        WaitUntil(() => unstaged.Items.OfType<FileChange>().Any(change => change.IsSkipWorktree));
        unstaged.SelectedItem = unstaged.Items.OfType<FileChange>().Single(change => change.Path == "config.txt");
        OpenMenu(unstaged);
        skip.IsChecked.Should().BeTrue();

        skip.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        WaitUntil(() => _repo.Run("ls-files", "-v", "config.txt").StartsWith("H ", StringComparison.Ordinal));

        WaitUntil(() => unstaged.Items.OfType<FileChange>().Any(change => change.Path == "config.txt" && !change.IsSkipWorktree));
        unstaged.SelectedItem = unstaged.Items.OfType<FileChange>().Single(change => change.Path == "config.txt");
        OpenMenu(unstaged);
        assume.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        WaitUntil(() => _repo.Run("ls-files", "-v", "config.txt").StartsWith("h ", StringComparison.Ordinal));
        commit.Close();
        window.Close();
    }

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

    private static void WaitUntil(Func<bool> condition, string because = "")
    {
        DateTime deadline = DateTime.UtcNow + _timeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail($"Timed out waiting for the UI {because}");
            }

            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(10);
        }
    }
}

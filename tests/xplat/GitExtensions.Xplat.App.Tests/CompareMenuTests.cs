using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using GitCommands;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Diff;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

/// <summary>
///  The grid menu's Compare submenu, upstream's FormDiff (<see cref="RevisionDiffWindow"/>) and FormCompareToBranch
///  (<see cref="CompareToBranchWindow"/>).
/// </summary>
internal sealed class CompareMenuTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private TestRepository _repo = null!;

    [SetUp]
    public void SetUp() => _repo = new TestRepository();

    [TearDown]
    public void TearDown() => _repo.Dispose();

    [AvaloniaTest]
    public void Compare_to_BASE_diffs_the_commit_selected_as_base_with_the_selected_one()
    {
        MainWindow window = OpenWindow();
        SelectCommit(window, "first");
        Click(Find<MenuItem>(window, "SelectAsBaseMenuItem"));
        Find<MenuItem>(window, "CompareToBaseMenuItem").IsEnabled.Should().BeTrue();
        SelectCommit(window, "second");

        Click(Find<MenuItem>(window, "CompareToBaseMenuItem"));

        RevisionDiffWindow diff = WaitForDiffWindow(window);
        Find<TextBlock>(diff, "lblFirstCommit").Text.Should().Be("first");
        Find<TextBlock>(diff, "lblSecondCommit").Text.Should().Be("second");
        ListBox files = Find<ListBox>(diff, "DiffFiles");
        files.Items.Cast<RevisionDiffFile>().Select(file => file.Display).Should().Equal("M a.txt");
        DiffView text = Find<DiffView>(diff, "DiffText");
        WaitUntil(() => text.Lines.Any(line => line.Text.EndsWith("two", StringComparison.Ordinal)));
        Find<CheckBox>(diff, "ckCompareToMergeBase").IsEnabled.Should().BeTrue("first is the merge base of the two");
        diff.Close();
        window.Close();
    }

    [AvaloniaTest]
    public void Compare_to_working_directory_lists_the_changed_and_untracked_files_and_swap_turns_the_diff_around()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "changed");
        File.WriteAllText(Path.Combine(_repo.Path, "new.txt"), "new");
        MainWindow window = OpenWindow();
        SelectCommit(window, "second");

        Click(Find<MenuItem>(window, "CompareToWorkingDirectoryMenuItem"));

        RevisionDiffWindow diff = WaitForDiffWindow(window);
        Find<TextBlock>(diff, "lblSecondCommit").Text.Should().Be("Working directory");
        ListBox files = Find<ListBox>(diff, "DiffFiles");
        files.Items.Cast<RevisionDiffFile>().Select(file => file.Display).Should().BeEquivalentTo(["M a.txt", "A new.txt"]);
        Find<Button>(diff, "btnCompareDirectoriesWithDiffTool").IsEnabled.Should().BeTrue();

        Find<Button>(diff, "btnSwap").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        WaitUntil(() => diff.FilesShown.IsCompleted && files.ItemCount > 0);

        Find<TextBlock>(diff, "lblFirstCommit").Text.Should().Be("Working directory");
        Find<Button>(diff, "btnCompareDirectoriesWithDiffTool").IsEnabled.Should()
            .BeFalse("git for Windows cannot start a directory diff from the work tree");
        files.Items.Cast<RevisionDiffFile>().Select(file => file.Display).Should().BeEquivalentTo(["M a.txt", "D new.txt"]);
        diff.Close();
        window.Close();
    }

    [AvaloniaTest]
    public async Task The_compare_to_branch_window_lists_local_or_remote_branches_and_closes_with_the_chosen_one()
    {
        _repo.Run("branch", "topic", "HEAD~1");
        _repo.Run("update-ref", "refs/remotes/origin/main", "HEAD");
        Window owner = new();
        owner.Show();
        CompareToBranchWindow choose = new(_repo.Path, ObjectId.Parse(_repo.Run("rev-parse", "HEAD").Trim()));
        Task<string?> chosen = choose.ShowDialog<string?>(owner);
        WaitUntil(() => choose.BranchesLoaded.IsCompleted);
        ComboBox branches = Find<ComboBox>(choose, "Branches");
        branches.Items.Cast<string>().Should().Equal(["origin/main"], "remote branches come first, as upstream's");

        Find<RadioButton>(choose, "LocalBranch").IsChecked = true;
        WaitUntil(() => branches.Items.Cast<string>().Contains("topic"));
        branches.SelectedItem = "topic";
        WaitUntil(() => Find<TextBlock>(choose, "lbChanges").Text?.Length > 0);
        Find<Button>(choose, "btnCompare").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        WaitUntil(() => chosen.IsCompleted);
        (await chosen).Should().Be("topic");
        owner.Close();
    }

    [AvaloniaTest]
    public void Compare_to_working_directory_on_the_working_directory_is_refused()
    {
        TestAppBuilder.Preferences.RevisionGraphShowArtificialCommits = true;
        try
        {
            File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "changed");
            MainWindow window = OpenWindow();
            ListBox commits = Find<ListBox>(window, "CommitList");
            WaitUntil(() => commits.Items.Cast<CommitListItem>().Any(item => item.Row.Hash == ArtificialCommits.WorkTreeHash));
            commits.SelectedItem = commits.Items.Cast<CommitListItem>().First(item => item.Row.Hash == ArtificialCommits.WorkTreeHash);

            Click(Find<MenuItem>(window, "CompareToWorkingDirectoryMenuItem"));

            WaitUntil(() => window.OwnedWindows.OfType<MessageBoxWindow>().Any());
            window.OwnedWindows.OfType<RevisionDiffWindow>().Should().BeEmpty();
            window.OwnedWindows.OfType<MessageBoxWindow>().Single().Close();
            window.Close();
        }
        finally
        {
            TestAppBuilder.Preferences.RevisionGraphShowArtificialCommits = false;
        }
    }

    private MainWindow OpenWindow()
    {
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        window.Show();
        window.OpenRepository(_repo.Path);
        WaitUntil(() => !window.IsBusy);
        return window;
    }

    private static RevisionDiffWindow WaitForDiffWindow(Window owner)
    {
        WaitUntil(() => owner.OwnedWindows.OfType<RevisionDiffWindow>().SingleOrDefault()?.DiffLoaded.IsCompleted == true);
        return owner.OwnedWindows.OfType<RevisionDiffWindow>().Single();
    }

    private static void SelectCommit(MainWindow window, string subject)
    {
        ListBox commits = Find<ListBox>(window, "CommitList");
        commits.SelectedItem = commits.Items.Cast<CommitListItem>().Single(item => item.Row.Subject == subject);
    }

    private static void Click(MenuItem item) => item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

    private static T Find<T>(Window window, string name)
        where T : Control
        => window.FindControl<T>(name)
           ?? window.GetLogicalDescendants().OfType<T>().FirstOrDefault(control => control.Name == name)
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

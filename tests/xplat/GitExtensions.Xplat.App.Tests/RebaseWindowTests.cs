using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using GitCommands;
using GitExtensions.Xplat.Core.Operations;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

internal sealed class RebaseWindowTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private TestRepository _repo = null!;
    private string _base = "";

    [SetUp]
    public void Setup()
    {
        _repo = new TestRepository();
        _base = _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim();
        Reset();
    }

    [TearDown]
    public void TearDown()
    {
        Reset();
        _repo.Dispose();
    }

    [AvaloniaTest]
    public void The_window_shows_the_current_branch_and_the_options()
    {
        _repo.Run("branch", "other");

        RebaseWindow rebase = Open(onto: "other");

        Find<TextBlock>(rebase, "CurrentBranchText").Text.Should().Be(_base);
        Find<ComboBox>(rebase, "BranchesBox").Text.Should().Be("other");
        (Find<ComboBox>(rebase, "BranchesBox").ItemsSource as IEnumerable<string>).Should().Contain(["other", _base]);
        Find<ComboBox>(rebase, "ToBox").Text.Should().Be(_base);
        Find<Control>(rebase, "OptionsTopPanel").IsVisible.Should().BeTrue();
        Find<Control>(rebase, "ShowOptionsLink").IsVisible.Should().BeFalse();
        Find<Button>(rebase, "RebaseButton").IsVisible.Should().BeTrue();
        Find<Button>(rebase, "ContinueButton").IsVisible.Should().BeFalse();
        Find<CheckBox>(rebase, "AutosquashCheck").IsEnabled.Should().BeFalse();
        Find<CheckBox>(rebase, "AutoStashCheck").IsEnabled.Should().BeFalse("the working directory is clean");

        Find<CheckBox>(rebase, "IgnoreDateCheck").IsChecked = true;

        Find<CheckBox>(rebase, "InteractiveCheck").IsEnabled.Should().BeFalse();
        Find<CheckBox>(rebase, "CommitterDateCheck").IsEnabled.Should().BeFalse();
        rebase.Close();
    }

    [AvaloniaTest]
    public void A_rebase_replays_the_branch_and_closes_the_window()
    {
        _repo.Run("checkout", "-q", "-b", "other", "HEAD~1");
        File.WriteAllText(Path.Combine(_repo.Path, "b.txt"), "other");
        _repo.Run("add", "b.txt");
        _repo.Run("commit", "-q", "-m", "other work");
        _repo.Run("checkout", "-q", _base);
        RebaseWindow rebase = Open(onto: "other");
        Find<CheckBox>(rebase, "AutoStashCheck").IsChecked = true;

        Click(rebase, "RebaseButton");

        WaitUntil(() => !rebase.IsVisible);
        _repo.Run("rev-parse", "HEAD~1").Trim().Should().Be(_repo.Run("rev-parse", "other").Trim());
        TestAppBuilder.Preferences.RebaseAutoStash.Should().BeTrue();
    }

    [AvaloniaTest]
    public void A_rebase_that_stops_on_conflicts_shows_its_commits_and_can_be_aborted()
    {
        MakeConflictingBranches();
        RebaseWindow rebase = Open(onto: "other");

        Click(rebase, "RebaseButton");

        WaitUntil(() => Find<Button>(rebase, "SolveConflictsButton").IsVisible);
        Find<Button>(rebase, "RebaseButton").IsVisible.Should().BeFalse();
        Find<Button>(rebase, "UnresolvedConflictsButton").IsVisible.Should().BeTrue();
        Find<Button>(rebase, "SkipButton").IsVisible.Should().BeTrue();
        Find<AccessText>(rebase, "SolveConflictsText").Text.Should().Be(">_Solve conflicts<");
        rebase.Patches.Should().ContainSingle(patch => patch.IsNext);
        RebasePatch applying = rebase.Patches.Single(patch => patch.IsNext);
        (applying.Action, applying.Status, applying.Subject).Should().Be(("pick", RebasePatch.ApplyingText, "ours"));

        Click(rebase, "AbortButton");

        WaitUntil(() => !rebase.IsVisible);
        Directory.Exists(Path.Combine(_repo.Path, ".git", "rebase-merge")).Should().BeFalse();
        _repo.Run("log", "-1", "--format=%s").Trim().Should().Be("ours");
    }

    [AvaloniaTest]
    public void Skipping_the_conflicting_commit_finishes_the_rebase()
    {
        MakeConflictingBranches();
        GitProcess.RunAllowingFailure(_repo.Path, "rebase", "other");

        RebaseWindow rebase = Open(onto: null);

        Find<Control>(rebase, "RebasePanel").IsVisible.Should().BeFalse("a rebase is in progress");
        Click(rebase, "SkipButton");

        WaitUntil(() => !rebase.IsVisible);
        _repo.Run("log", "-1", "--format=%s").Trim().Should().Be("theirs");
        RebasePatches.Skipped.Should().ContainSingle();
    }

    [AvaloniaTest]
    public void The_action_bars_more_opens_the_rebase_window()
    {
        MakeConflictingBranches();
        GitProcess.RunAllowingFailure(_repo.Path, "rebase", "other");
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        window.Show();
        window.OpenRepository(_repo.Path);
        WaitUntil(() => !window.IsBusy && Find<Border>(window, "StateBanner").IsVisible);

        Click(window, "MoreActionButton");

        WaitUntil(() => window.OwnedWindows.OfType<RebaseWindow>().Any());
        RebaseWindow rebase = window.OwnedWindows.OfType<RebaseWindow>().Single();
        WaitUntil(() => rebase.RebaseLoaded.IsCompleted);
        Click(rebase, "AbortButton");
        WaitUntil(() => !rebase.IsVisible);
        WaitUntil(() => !Find<Border>(window, "StateBanner").IsVisible);
        window.Close();
    }

    [AvaloniaTest]
    public void The_grids_selected_commit_rebases_at_once()
    {
        _repo.Run("checkout", "-q", "-b", "other", "HEAD~1");
        File.WriteAllText(Path.Combine(_repo.Path, "b.txt"), "other");
        _repo.Run("add", "b.txt");
        _repo.Run("commit", "-q", "-m", "other work");
        string onto = _repo.Run("rev-parse", "HEAD").Trim();
        _repo.Run("checkout", "-q", _base);

        RebaseWindow rebase = Open(onto, immediately: true);

        WaitUntil(() => !rebase.IsVisible);
        _repo.Run("rev-parse", "HEAD~1").Trim().Should().Be(onto);
        (Find<ComboBox>(rebase, "BranchesBox").ItemsSource as IEnumerable<string>).Should().BeEmpty(
            "upstream reads no refs for a rebase at once");
    }

    private static void Reset()
    {
        TestAppBuilder.Preferences.RebaseAutoStash = false;
        TestAppBuilder.Preferences.AlwaysShowAdvOpt = false;
        RebasePatches.Skipped.Clear();
    }

    // "other" changes a.txt to "theirs" and the base branch to "ours", which stays checked out.
    private void MakeConflictingBranches()
    {
        _repo.Run("checkout", "-q", "-b", "other");
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "theirs");
        _repo.Run("commit", "-q", "-am", "theirs");
        _repo.Run("checkout", "-q", _base);
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "ours");
        _repo.Run("commit", "-q", "-am", "ours");
    }

    private RebaseWindow Open(string? onto, bool immediately = false)
    {
        Window owner = new();
        owner.Show();
        RebaseWindow rebase = new(_repo.Path, new RepositoryOperationsViewModel(new GitOperations()),
            TestAppBuilder.Preferences, from: "", to: null, onto, interactive: false, startRebaseImmediately: immediately,
            _ => { }, _ => { });
        _ = rebase.ShowDialog<bool>(owner);
        WaitUntil(() => rebase.RebaseLoaded.IsCompleted);
        return rebase;
    }

    private static void Click(Window window, string name)
        => Find<Button>(window, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static T Find<T>(Window window, string name) where T : Control
        => window.FindControl<T>(name) ??
           window.GetLogicalDescendants().OfType<T>().FirstOrDefault(control => control.Name == name) ??
           throw new InvalidOperationException($"Control {name} not found");

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

using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using GitCommands;
using GitCommands.Git;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

// Upstream's notification bars (InteractiveGitActionControl) and bisect (FormBisect, the grid's bisect items) with real git.
internal sealed class GitActionBarTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(20);

    private TestRepository _repo = null!;
    private readonly List<string> _folders = [];

    [SetUp]
    public void Setup()
    {
        _repo = new TestRepository();
        TestAppBuilder.Preferences.ResetConfirmations();
        TestAppBuilder.Preferences.ShowCurrentBranchOnly = false;
        TestAppBuilder.Preferences.CloseProcessDialog = true;
    }

    [TearDown]
    public void TearDown()
    {
        TestAppBuilder.Preferences.CloseProcessDialog = false;
        _repo.Dispose();
        foreach (string folder in _folders)
        {
            GitProcess.DeleteFolder(folder);
        }
    }

    [AvaloniaTest]
    public void Bisect_operations_find_the_first_bad_commit_and_reset_returns_to_the_branch()
    {
        string branch = _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim();
        string root = _repo.Run("rev-list", "--max-parents=0", "HEAD").Trim();
        AddCommits("third", "fourth");
        GitOperations operations = new();
        List<GitOutputLine> output = [];

        Wait(operations.StartBisectAsync(_repo.Path));
        Wait(operations.MarkBisectAsync(_repo.Path, GitBisectOption.Bad, commit: null));
        Wait(operations.MarkBisectAsync(_repo.Path, GitBisectOption.Good, root, new CollectingProgress(output)));

        output.Select(line => line.Text).Should().Contain(text => text.StartsWith("Bisecting:", StringComparison.Ordinal));
        Snapshot().IsBisecting.Should().BeTrue();
        Snapshot().IsRebasing.Should().BeFalse();
        _repo.Run("for-each-ref", "--format=%(refname)", "refs/bisect/").Should().Contain("refs/bisect/bad");

        Wait(operations.StopBisectAsync(_repo.Path));

        Snapshot().IsBisecting.Should().BeFalse();
        Snapshot().CurrentBranch.Should().Be(branch);
    }

    [AvaloniaTest]
    public void A_stopped_git_am_is_a_patch_not_a_rebase_and_can_be_aborted()
    {
        string patch = ConflictingPatch();

        GitProcess.RunAllowingFailure(_repo.Path, "am", patch).Should().NotBe(0);

        RepositorySnapshot stopped = Snapshot();
        stopped.IsApplyingPatch.Should().BeTrue();
        stopped.IsRebasing.Should().BeFalse();
        Wait(new GitOperations().AbortPatchAsync(_repo.Path));
        Snapshot().IsApplyingPatch.Should().BeFalse();
    }

    [AvaloniaTest]
    public void A_stopped_rebase_is_a_rebase_not_a_patch()
    {
        string baseBranch = MakeConflictingBranches();

        GitProcess.RunAllowingFailure(_repo.Path, "rebase", "other").Should().NotBe(0);

        RepositorySnapshot stopped = Snapshot();
        stopped.IsRebasing.Should().BeTrue();
        stopped.IsApplyingPatch.Should().BeFalse();
        _repo.Run("rebase", "--abort");
        Snapshot().CurrentBranch.Should().Be(baseBranch);
    }

    [AvaloniaTest]
    public void The_action_bar_shows_a_conflicted_merge_and_continues_it_once_resolved()
    {
        MakeConflictingBranches();
        GitProcess.RunAllowingFailure(_repo.Path, "merge", "other");
        MainWindow window = NewWindow();
        window.Show();
        Open(window, _repo.Path);

        WaitUntil(() => Find<Border>(window, "StateBanner").IsVisible);
        Find<TextBlock>(window, "StateBannerText").Text.Should().Be("Merge is currently in progress with merge conflicts.");
        Find<Button>(window, "ResolveConflictsButton").IsVisible.Should().BeTrue();
        Find<Button>(window, "ContinueActionButton").IsVisible.Should().BeFalse();
        Find<Button>(window, "AbortActionButton").IsVisible.Should().BeTrue();
        Find<Button>(window, "MoreActionButton").IsVisible.Should().BeFalse();
        object? conflictBackground = Find<Border>(window, "StateBanner").Background;
        conflictBackground.Should().NotBeNull();

        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "both");
        _repo.Run("add", "a.txt");
        Click(window, "RefreshButton");
        WaitUntil(() => Find<TextBlock>(window, "StateBannerText").Text == "Merge is currently in progress.");
        Find<Button>(window, "ContinueActionButton").IsVisible.Should().BeTrue();
        Find<Border>(window, "StateBanner").Background.Should().NotBe(conflictBackground);

        Click(window, "ContinueActionButton");

        WaitUntil(() => !Find<Border>(window, "StateBanner").IsVisible);
        _repo.Run("rev-list", "--parents", "-n", "1", "HEAD").Trim().Split(' ').Should().HaveCount(3);
    }

    [AvaloniaTest]
    public void The_action_bar_offers_skip_and_edit_todo_for_a_rebase_under_more()
    {
        MakeConflictingBranches();
        GitProcess.RunAllowingFailure(_repo.Path, "rebase", "other");
        MainWindow window = NewWindow();
        window.Show();
        Open(window, _repo.Path);

        WaitUntil(() => Find<Border>(window, "StateBanner").IsVisible);
        Find<TextBlock>(window, "StateBannerText").Text.Should().Be("Rebase is currently in progress with merge conflicts.");
        Find<Button>(window, "MoreActionButton").IsVisible.Should().BeTrue();
        Find<MenuItem>(window, "SkipRebaseMenuItem").IsVisible.Should().BeTrue();
        Find<MenuItem>(window, "SkipPatchMenuItem").IsVisible.Should().BeFalse();

        Click(window, "SkipRebaseMenuItem");

        WaitUntil(() => !Find<Border>(window, "StateBanner").IsVisible);
        _repo.Run("log", "-1", "--format=%s").Trim().Should().Be("theirs");
    }

    [AvaloniaTest]
    public void The_bisect_window_starts_a_bisect_and_marks_the_checked_out_commit()
    {
        MainWindow window = NewWindow();
        window.Show();
        Open(window, _repo.Path);
        Find<ListBox>(window, "CommitList").SelectedIndex = 0;
        WaitUntil(() => Find<MenuItem>(window, "BisectMenuItem").IsEnabled);

        Click(window, "BisectMenuItem");
        WaitUntil(() => window.OwnedWindows.OfType<BisectWindow>().Any());
        BisectWindow bisect = window.OwnedWindows.OfType<BisectWindow>().Single();
        Find<Button>(bisect, "BadButton").IsEnabled.Should().BeFalse();
        Click(bisect, "StartButton");
        WaitUntil(() => Find<Button>(bisect, "BadButton").IsEnabled);
        Find<Button>(bisect, "StartButton").IsEnabled.Should().BeFalse();
        Click(bisect, "BadButton");

        WaitUntil(() => _repo.Run("for-each-ref", "--format=%(refname)", "refs/bisect/").Contains("refs/bisect/bad"));
        WaitUntil(() => Find<Border>(window, "BisectBanner").IsVisible);
        Find<TextBlock>(window, "BisectBannerText").Text.Should().Be("Bisect is currently in progress.");

        // A mark's output is its result (the next commit to test), so it stays open although the setting closes it.
        ProcessWindow process = window.OwnedWindows.OfType<ProcessWindow>().Single();
        WaitUntil(() => Find<TextBlock>(process, "StateText").Text == "Done");
        process.IsVisible.Should().BeTrue();
        process.Close();
    }

    [AvaloniaTest]
    public void The_grid_marks_the_selected_commit_shows_the_marks_and_stops_the_bisect()
    {
        _repo.Run("bisect", "start");
        _repo.Run("bisect", "bad");
        MainWindow window = NewWindow();
        window.Show();
        Open(window, _repo.Path);
        WaitUntil(() => Find<Border>(window, "BisectBanner").IsVisible);
        Find<MenuItem>(window, "MarkGoodMenuItem").IsVisible.Should().BeTrue();
        Find<MenuItem>(window, "StopBisectMenuItem").IsVisible.Should().BeTrue();

        ListBox commits = Find<ListBox>(window, "CommitList");
        IReadOnlyList<CommitListItem> rows = (IReadOnlyList<CommitListItem>)commits.ItemsSource!;
        rows.Single(row => row.Row.Subject == "second").Labels.Should()
            .Contain(new RefLabel(RefLabel.BisectBadName, RefKind.BisectBad));
        commits.SelectedItem = rows.Single(row => row.Row.Subject == "first");
        Click(window, "MarkGoodMenuItem");

        WaitUntil(() => _repo.Run("for-each-ref", "--format=%(refname)", "refs/bisect/").Contains("refs/bisect/good-"));
        window.OwnedWindows.OfType<ProcessWindow>().Single().Close();
        WaitUntil(() => ((IReadOnlyList<CommitListItem>)commits.ItemsSource!)
            .Any(row => row.Labels.Contains(new RefLabel(RefLabel.BisectGoodName, RefKind.BisectGood))));

        Click(window, "StopBisectMenuItem");

        WaitUntil(() => !Find<Border>(window, "BisectBanner").IsVisible);
        File.Exists(Path.Combine(_repo.Path, ".git", "BISECT_START")).Should().BeFalse();
        Find<MenuItem>(window, "MarkGoodMenuItem").IsVisible.Should().BeFalse();
    }

    private void AddCommits(params string[] subjects)
    {
        foreach (string subject in subjects)
        {
            File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), subject);
            _repo.Run("commit", "-q", "-am", subject);
        }
    }

    // "other" changes a.txt to "theirs" and the base branch to "ours"; returns the base branch, which stays checked out.
    private string MakeConflictingBranches()
    {
        string baseBranch = _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim();
        _repo.Run("checkout", "-q", "-b", "other");
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "theirs");
        _repo.Run("commit", "-q", "-am", "theirs");
        _repo.Run("checkout", "-q", baseBranch);
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "ours");
        _repo.Run("commit", "-q", "-am", "ours");
        return baseBranch;
    }

    // A patch of "other"'s commit, which does not apply on the base branch.
    private string ConflictingPatch()
    {
        MakeConflictingBranches();
        string folder = Path.Combine(Path.GetTempPath(), "xplat-patch-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        _folders.Add(folder);
        _repo.Run("format-patch", "-q", "-1", "other", "-o", folder);
        return Directory.GetFiles(folder).Single();
    }

    private RepositorySnapshot Snapshot() => Wait(new GitRepositoryService().GetSnapshotAsync(_repo.Path));

    private static MainWindow NewWindow() =>
        new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));

    private static void Open(MainWindow window, string path)
    {
        Find<TextBox>(window, "PathBox").Text = path;
        Find<Button>(window, "OpenButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        WaitUntil(() => Find<Button>(window, "OpenButton").IsEnabled);
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

    private static void Wait(Task task) => task.GetAwaiter().GetResult();

    private static T Wait<T>(Task<T> task) => task.GetAwaiter().GetResult();

    private sealed class CollectingProgress(List<GitOutputLine> lines) : IProgress<GitOutputLine>
    {
        public void Report(GitOutputLine value)
        {
            lock (lines)
            {
                lines.Add(value);
            }
        }
    }
}

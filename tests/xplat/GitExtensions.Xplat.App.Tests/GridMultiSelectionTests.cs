using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using GitCommands;
using GitExtensions.Xplat.Core.CommitHistory;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

/// <summary>
///  The grid's multi-selection (upstream's <c>MultiSelect</c>): the latest selected commit, the Diff tab between the first
///  and the latest selected, and Compare selected commits.
/// </summary>
internal sealed class GridMultiSelectionTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private TestRepository _repo = null!;

    [SetUp]
    public void SetUp()
    {
        _repo = new TestRepository();
        File.WriteAllText(Path.Combine(_repo.Path, "b.txt"), "b");
        _repo.Run("add", "b.txt");
        _repo.Run("commit", "-q", "-m", "third");
    }

    [TearDown]
    public void TearDown() => _repo.Dispose();

    [AvaloniaTest]
    public void Two_selected_commits_show_the_files_between_them_and_the_latest_in_the_commit_info()
    {
        MainWindow window = OpenWindow();
        ListBox commits = Find<ListBox>(window, "CommitList");
        Select(commits, "first");

        commits.SelectedItems!.Add(Item(commits, "third"));

        window.SelectedRevisions.Select(row => row.Subject).Should().Equal("third", "first");
        ListBox files = Find<ListBox>(window, "CommitFilesList");
        WaitUntil(() => files.ItemCount == 2);
        files.Items.Cast<CommitFile>().Select(file => file.Display).Should().BeEquivalentTo(["M a.txt", "A b.txt"]);
        files.SelectedItem = files.Items.Cast<CommitFile>().Single(file => file.Path == "a.txt");
        DiffView diff = Find<DiffView>(window, "CommitDiff");
        WaitUntil(() => diff.Lines.Any(line => line.Text.EndsWith("two", StringComparison.Ordinal)));

        commits.SelectedItems.Remove(Item(commits, "first"));

        WaitUntil(() => files.ItemCount == 1);
        files.Items.Cast<CommitFile>().Single().Path.Should().Be("b.txt", "one commit shows its own changes again");
        window.Close();
    }

    [AvaloniaTest]
    public void Compare_selected_commits_diffs_the_first_selected_with_the_latest()
    {
        MainWindow window = OpenWindow();
        ListBox commits = Find<ListBox>(window, "CommitList");
        Select(commits, "third");
        commits.SelectedItems!.Add(Item(commits, "first"));

        Find<MenuItem>(window, "CompareSelectedCommitsMenuItem").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

        WaitUntil(() => window.OwnedWindows.OfType<RevisionDiffWindow>().SingleOrDefault()?.DiffLoaded.IsCompleted == true);
        RevisionDiffWindow diff = window.OwnedWindows.OfType<RevisionDiffWindow>().Single();
        Find<TextBlock>(diff, "lblFirstCommit").Text.Should().Be("third");
        Find<TextBlock>(diff, "lblSecondCommit").Text.Should().Be("first");
        diff.Close();
        window.Close();
    }

    [AvaloniaTest]
    public void One_selected_commit_compares_with_its_first_parent()
    {
        MainWindow window = OpenWindow();
        ListBox commits = Find<ListBox>(window, "CommitList");
        Select(commits, "third");
        commits.RaiseEvent(new Avalonia.Input.ContextRequestedEventArgs());
        Dispatcher.UIThread.RunJobs();
        commits.ContextMenu!.Close();

        Find<MenuItem>(window, "CompareSelectedCommitsMenuItem").IsEnabled.Should().BeTrue();
        Find<MenuItem>(window, "OpenCommitsWithDiffToolMenuItem").IsEnabled.Should().BeTrue();
        window.Close();
    }

    [AvaloniaTest]
    public void Cherry_pick_opens_a_window_per_selected_commit_from_the_bottom_until_one_is_aborted()
    {
        MainWindow window = OpenWindow();
        ListBox commits = Find<ListBox>(window, "CommitList");
        Select(commits, "third");
        commits.SelectedItems!.Add(Item(commits, "second"));

        Find<MenuItem>(window, "CherryPickMenuItem").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

        WaitUntil(() => window.OwnedWindows.OfType<CherryPickWindow>().SingleOrDefault()?.RevisionLoaded.IsCompleted == true);
        CherryPickWindow pick = window.OwnedWindows.OfType<CherryPickWindow>().Single();
        pick.GetLogicalDescendants().OfType<TextBlock>().Select(text => text.Text).Should()
            .Contain("second", "the lowest selected commit comes first");
        Find<Button>(pick, "btnAbort").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        WaitUntil(() => !pick.IsVisible);
        for (int i = 0; i < 20; i++)
        {
            Dispatcher.UIThread.RunJobs();
        }

        window.OwnedWindows.OfType<CherryPickWindow>().Should().BeEmpty("an aborted pick ends the run");
        window.Close();
    }

    [AvaloniaTest]
    public void Archive_of_two_selected_commits_archives_the_files_changed_between_them()
    {
        MainWindow window = OpenWindow();
        ListBox commits = Find<ListBox>(window, "CommitList");
        Select(commits, "first");
        commits.SelectedItems!.Add(Item(commits, "third"));

        Find<MenuItem>(window, "ArchiveCommitMenuItem").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

        WaitUntil(() => window.OwnedWindows.OfType<ArchiveWindow>().SingleOrDefault()?.RevisionsLoaded.IsCompleted == true);
        ArchiveWindow archive = window.OwnedWindows.OfType<ArchiveWindow>().Single();
        Find<CheckBox>(archive, "checkboxRevisionFilter").IsChecked.Should().BeTrue();
        Find<TextBlock>(archive, "labelMessage").Text.Should().Be("first");
        archive.Close();

        commits.SelectedItems.Add(Item(commits, "second"));
        Find<MenuItem>(window, "ArchiveCommitMenuItem").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

        WaitUntil(() => window.OwnedWindows.OfType<MessageBoxWindow>().Any());
        window.OwnedWindows.OfType<ArchiveWindow>().Should().BeEmpty("upstream archives one or two commits");
        window.OwnedWindows.OfType<MessageBoxWindow>().Single().Close();
        window.Close();
    }

    [AvaloniaTest]
    public void Starting_a_bisect_with_two_selected_commits_offers_them_as_the_range()
    {
        MainWindow window = OpenWindow();
        ListBox commits = Find<ListBox>(window, "CommitList");
        Select(commits, "third");
        commits.SelectedItems!.Add(Item(commits, "first"));
        string third = _repo.Run("rev-parse", "HEAD").Trim();
        string first = _repo.Run("rev-parse", "HEAD~2").Trim();

        Find<MenuItem>(window, "BisectMenuItem").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        WaitUntil(() => window.OwnedWindows.OfType<BisectWindow>().Any());
        BisectWindow bisect = window.OwnedWindows.OfType<BisectWindow>().Single();
        Find<Button>(bisect, "StartButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        WaitUntil(() => bisect.OwnedWindows.OfType<MessageBoxWindow>().Any());
        MessageBoxWindow question = bisect.OwnedWindows.OfType<MessageBoxWindow>().Single();
        Find<Button>(question, "YesButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        WaitUntil(() => !bisect.IsVisible);
        string log = File.ReadAllText(Path.Combine(_repo.Path, ".git", "BISECT_LOG"));
        log.Should().Contain($"git bisect good {first}", "upstream marks the latest selected good").And
            .Contain($"git bisect bad {third}");
        _repo.Run("bisect", "reset");
        window.Close();
    }

    [AvaloniaTest]
    public void The_grid_opens_on_HEAD_and_a_refresh_keeps_the_selected_commits()
    {
        MainWindow window = OpenWindow();
        ListBox commits = Find<ListBox>(window, "CommitList");
        WaitUntil(() => window.SelectedRevisions.Select(row => row.Subject).SequenceEqual(["third"]));
        Select(commits, "first");
        commits.SelectedItems!.Add(Item(commits, "second"));

        Find<Button>(window, "RefreshButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        WaitUntil(() => window.IsBusy);
        WaitUntil(() => !window.IsBusy && window.SelectedRevisions.Count == 2);

        window.SelectedRevisions.Select(row => row.Subject).Should()
            .Equal(["second", "first"], "the latest selected stays the latest");
        window.Close();
    }

    [AvaloniaTest]
    public void A_selected_commit_that_is_gone_after_a_refresh_hands_over_to_its_nearest_shown_ancestor()
    {
        MainWindow window = OpenWindow();
        ListBox commits = Find<ListBox>(window, "CommitList");
        Select(commits, "third");
        _repo.Run("reset", "-q", "--hard", "HEAD~1");
        _repo.Run("commit", "-q", "--allow-empty", "-m", "fourth");

        Find<Button>(window, "RefreshButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        WaitUntil(() => window.IsBusy);
        WaitUntil(() => !window.IsBusy && window.SelectedRevisions.Count == 1
                                       && window.SelectedRevisions[0].Subject != "third");

        window.SelectedRevisions.Single().Subject.Should().Be("second", "third's nearest commit the grid shows");
        window.Close();
    }

    private MainWindow OpenWindow()
    {
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        window.Show();
        window.OpenRepository(_repo.Path);
        WaitUntil(() => !window.IsBusy);
        return window;
    }

    private static CommitListItem Item(ListBox commits, string subject)
        => commits.Items.Cast<CommitListItem>().Single(item => item.Row.Subject == subject);

    private static void Select(ListBox commits, string subject) => commits.SelectedItem = Item(commits, subject);

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

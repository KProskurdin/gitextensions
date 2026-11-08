using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
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
    public void A_merge_lists_its_files_against_each_parent_and_its_combined_diff_as_upstreams_groups()
    {
        string main = _repo.Run("branch", "--show-current").Trim();
        _repo.Run("checkout", "-q", "-b", "side", "HEAD~1");
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "side");
        File.WriteAllText(Path.Combine(_repo.Path, "c.txt"), "c");
        _repo.Run("add", ".");
        _repo.Run("commit", "-q", "-m", "side");
        _repo.Run("checkout", "-q", main);
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "main");
        _repo.Run("commit", "-q", "-am", "main change");
        GitProcess.RunAllowingFailure(_repo.Path, "merge", "-q", "side", "-m", "merge side").Should().NotBe(0, "a.txt conflicts");
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "merged");
        _repo.Run("commit", "-q", "-am", "merge side");
        bool allParents = AppSettings.ShowDiffForAllParents;
        AppSettings.ShowDiffForAllParents = true;
        try
        {
            MainWindow window = OpenWindow();
            ListBox commits = Find<ListBox>(window, "CommitList");
            Find<TabControl>(window, "DetailTabs").SelectedItem = Find<TabItem>(window, "DiffTab");
            ListBox files = Find<ListBox>(window, "CommitFilesList");
            Select(commits, "merge side");
            WaitUntil(() => files.Items.OfType<DiffGroupHeader>().Count() == 3);

            List<DiffGroupHeader> headers = [.. files.Items.OfType<DiffGroupHeader>()];
            headers[0].Text.Should().MatchRegex(@"^\(\d\) Diff with A [0-9a-f]{7,}: main change$");
            headers[1].Text.Should().MatchRegex(@"^\(\d\) Diff with A [0-9a-f]{7,}: side$");
            (headers[2].Text, headers[2].Group.Icon, headers[2].IsExpanded).Should().Be(("(1) Combined diff", "DiffC", false),
                "upstream collapses the third group unless it is a plain diff of few files");
            files.Items.OfType<CommitFile>().Should().NotContain(file => file.Group == headers[2].Group);

            // Upstream expands a node on a click on its glyph.
            ClickItem(window, files, headers[2], x: 4);
            WaitUntil(() => files.Items.OfType<CommitFile>().Any(file => file.Group is { IsCombinedDiff: true }));
            files.SelectedItem = files.Items.OfType<CommitFile>().Single(file => file.Group is { IsCombinedDiff: true });
            DiffView diff = Find<DiffView>(window, "CommitDiff");
            WaitUntil(() => diff.Lines.Any(line => line.Text.StartsWith("@@@", StringComparison.Ordinal)));
            diff.Lines.Should().Contain(line => line.Text == "++merged" && line.Background != null && line.NewNumber.Length > 0,
                "a line both parents lacked is added, as upstream colors it");
            Find<Button>(window, "CommitDiffToolButton").IsEnabled.Should().BeFalse("upstream has no difftool for it");
            window.Close();
        }
        finally
        {
            AppSettings.ShowDiffForAllParents = allParents;
        }
    }

    [AvaloniaTest]
    public void Two_commits_on_separate_branches_add_upstreams_groups_from_their_merge_base()
    {
        string main = _repo.Run("branch", "--show-current").Trim();
        _repo.Run("checkout", "-q", "-b", "side", "HEAD~1");
        File.WriteAllText(Path.Combine(_repo.Path, "c.txt"), "c");
        _repo.Run("add", "c.txt");
        _repo.Run("commit", "-q", "-m", "side");
        bool allParents = AppSettings.ShowDiffForAllParents;
        AppSettings.ShowDiffForAllParents = true;
        try
        {
            MainWindow window = OpenWindow();
            ListBox commits = Find<ListBox>(window, "CommitList");
            ListBox files = Find<ListBox>(window, "CommitFilesList");
            Select(commits, "side");
            commits.SelectedItems!.Add(Item(commits, "third"));

            WaitUntil(() => files.Items.OfType<DiffGroupHeader>().Count() == 3);
            files.Items.OfType<DiffGroupHeader>().Select(header => (header.Text[..header.Text.IndexOf(' ')], header.Group.Icon))
                .Should().Equal(("(2)", "Diff"), ("(1)", "DiffB"), ("(1)", "DiffA"));
            files.Items.OfType<DiffGroupHeader>().Select(header => header.Text).Should()
                .SatisfyRespectively(
                    first => first.Should().MatchRegex(@" Diff with A [0-9a-f]{7,}: side$"),
                    second => second.Should().MatchRegex($@" Diff BASE with B [0-9a-f]{{7,}}: {main}$",
                        "upstream names a commit by its first branch"),
                    third => third.Should().MatchRegex(@" Diff BASE with A [0-9a-f]{7,}: side$"));

            // Upstream marks each file with the side it changed on, and its diff status filter shows.
            files.Items.OfType<CommitFile>().Where(file => file.Group?.Icon == "Diff")
                .Select(file => (file.Path, file.Image)).Should().BeEquivalentTo(
                    [("b.txt", "FileStatusAddedOnlyB"), ("c.txt", "FileStatusRemovedOnlyA")]);
            ToolToggleButton onlyA = Find<ToolToggleButton>(window, "DiffFilesOnlyAButton");
            onlyA.IsVisible.Should().BeTrue();
            onlyA.IsChecked = false;
            onlyA.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();

            files.Items.OfType<CommitFile>().Should().NotContain(file => file.Path == "c.txt",
                "the files changed in A only are filtered out");
            files.Items.OfType<CommitFile>().Should().Contain(file => file.Path == "b.txt");
            window.Close();
        }
        finally
        {
            AppSettings.ShowDiffForAllParents = allParents;
        }
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
        MenuItem difftool = Find<MenuItem>(window, "OpenCommitsWithDiffToolMenuItem");
        difftool.IsEnabled.Should().BeTrue();

        // Upstream's CustomDiffMergeToolProvider: the tools, then "Disable this dropdown".
        WaitUntil(() => difftool.Items.Count > 0);
        difftool.Items.OfType<MenuItem>().Select(item => item.Tag as string).Should().Equal("meld", "vscode", null);
        difftool.Items.OfType<MenuItem>().Last().Header.Should().Be("Disable this dropdown");
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

    [AvaloniaTest]
    public void Rebase_with_advanced_options_on_two_selected_commits_starts_the_range_at_the_other_one()
    {
        MainWindow window = OpenWindow();
        ListBox commits = Find<ListBox>(window, "CommitList");
        Select(commits, "first");
        commits.SelectedItems!.Add(Item(commits, "third"));

        Find<MenuItem>(window, "RebaseAdvancedMenuItem").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

        WaitUntil(() => window.OwnedWindows.OfType<RebaseWindow>().Any());
        RebaseWindow rebase = window.OwnedWindows.OfType<RebaseWindow>().Single();
        WaitUntil(() => rebase.RebaseLoaded.IsCompleted);
        string first = _repo.Run("rev-parse", "HEAD~2").Trim();
        Find<TextBox>(rebase, "FromBox").Text.Should().Be(GitExtensions.Extensibility.Git.ObjectId.Parse(first).ToShortString());
        Find<CheckBox>(rebase, "SpecificRangeCheck").IsChecked.Should().BeTrue();
        Find<ComboBox>(rebase, "BranchesBox").Text.Should().Be(_repo.Run("rev-parse", "HEAD").Trim());
        rebase.Close();
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

    [AvaloniaTest]
    public void Two_commits_on_separate_branches_list_the_range_diff_item_without_a_group_and_show_git_range_diff()
    {
        _repo.Run("checkout", "-q", "-b", "side", "HEAD~1");
        File.WriteAllText(Path.Combine(_repo.Path, "c.txt"), "c");
        _repo.Run("add", "c.txt");
        _repo.Run("commit", "-q", "-m", "side");
        bool allParents = AppSettings.ShowDiffForAllParents;
        AppSettings.ShowDiffForAllParents = true;
        try
        {
            MainWindow window = OpenWindow();
            ListBox commits = Find<ListBox>(window, "CommitList");
            ListBox files = Find<ListBox>(window, "CommitFilesList");
            Select(commits, "side");
            commits.SelectedItems!.Add(Item(commits, "third"));
            WaitUntil(() => files.Items.OfType<DiffGroupHeader>().Count() == 3);
            CommitFile range = files.Items.OfType<CommitFile>().Single(file => file.DiffFile?.IsRangeDiff == true);

            range.Image.Should().Be("DiffR");
            range.Path.Should().StartWith("Range diff 1").And.Contain(" BASE ");
            files.Items.OfType<object>().ElementAt(files.Items.OfType<object>().ToList().IndexOf(range) + 1)
                .Should().BeOfType<DiffGroupHeader>().Which.Group.Icon.Should().Be("DiffB", "it stands between the groups");

            files.SelectedItem = range;
            DiffView diff = Find<DiffView>(window, "CommitDiff");
            WaitUntil(() => diff.Lines.Any(line => line.Text.Contains("side", StringComparison.Ordinal)
                                                   && !line.Text.StartsWith("git range-diff", StringComparison.Ordinal)));
            diff.Lines.Should().Contain(line => line.Text.Contains("third", StringComparison.Ordinal));
            Find<Button>(window, "CommitDiffToolButton").IsEnabled.Should().BeFalse();
            window.Close();
        }
        finally
        {
            AppSettings.ShowDiffForAllParents = allParents;
        }
    }

    [AvaloniaTest]
    public void Show_skip_worktree_files_lists_the_working_directorys_skip_worktree_files()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "changed but skipped");
        _repo.Run("update-index", "--skip-worktree", "a.txt");
        File.WriteAllText(Path.Combine(_repo.Path, "b.txt"), "changed");
        bool artificial = TestAppBuilder.Preferences.RevisionGraphShowArtificialCommits;
        TestAppBuilder.Preferences.RevisionGraphShowArtificialCommits = true;
        try
        {
            MainWindow window = OpenWindow();
            ListBox commits = Find<ListBox>(window, "CommitList");
            ListBox files = Find<ListBox>(window, "CommitFilesList");
            WaitUntil(() => commits.Items.Cast<CommitListItem>().Any(item => item.Row.Hash == ArtificialCommits.WorkTreeHash));
            commits.SelectedItem = commits.Items.Cast<CommitListItem>().Single(item => item.Row.Hash == ArtificialCommits.WorkTreeHash);
            WaitUntil(() => files.Items.OfType<CommitFile>().Any(file => file.Path == "b.txt"));
            files.Items.OfType<CommitFile>().Should().NotContain(file => file.Path == "a.txt");
            MenuItem show = Find<MenuItem>(window, "DiffFilesShowSkipWorktreeMenuItem");

            show.IsChecked = true;
            show.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            WaitUntil(() => files.Items.OfType<CommitFile>().Any(file => file.Path == "a.txt"));
            files.Items.OfType<CommitFile>().Should().Contain(file => file.Path == "b.txt");
            files.Items.OfType<DiffGroupHeader>().Should().BeEmpty("one group has no node");

            show.IsChecked = false;
            show.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            WaitUntil(() => !files.Items.OfType<CommitFile>().Any(file => file.Path == "a.txt"));
            window.Close();
        }
        finally
        {
            TestAppBuilder.Preferences.RevisionGraphShowArtificialCommits = artificial;
        }
    }

    [AvaloniaTest]
    public void Several_selected_commits_leave_the_authors_highlight_as_upstream()
    {
        _repo.Run("-c", "user.name=Ann", "-c", "user.email=ann@example.com", "commit", "-q", "--allow-empty", "-m", "by ann");
        bool highlight = TestAppBuilder.Preferences.HighlightAuthoredRevisions;
        TestAppBuilder.Preferences.HighlightAuthoredRevisions = true;
        try
        {
            MainWindow window = OpenWindow();
            ListBox commits = Find<ListBox>(window, "CommitList");
            Select(commits, "third");
            Dispatcher.UIThread.RunJobs();
            IReadOnlyList<string> Authored() => [.. commits.GetRealizedContainers()
                .Where(container => container.Classes.Contains("authored"))
                .Select(container => ((CommitListItem)container.DataContext!).Row.Subject)];
            Authored().Should().Contain("third").And.NotContain("by ann");

            commits.SelectedItems!.Add(Item(commits, "by ann"));
            Dispatcher.UIThread.RunJobs();

            Authored().Should().Contain("third").And.NotContain("by ann", "the highlight stays the single selection's");
            window.Close();
        }
        finally
        {
            TestAppBuilder.Preferences.HighlightAuthoredRevisions = highlight;
        }
    }

    [AvaloniaTest]
    public void Ctrl_A_in_the_diff_tab_selects_the_files_of_the_first_diff_group_as_upstreams_hotkey()
    {
        _repo.Run("checkout", "-q", "-b", "side", "HEAD~1");
        File.WriteAllText(Path.Combine(_repo.Path, "c.txt"), "c");
        _repo.Run("add", "c.txt");
        _repo.Run("commit", "-q", "-m", "side");
        bool allParents = AppSettings.ShowDiffForAllParents;
        AppSettings.ShowDiffForAllParents = true;
        try
        {
            MainWindow window = OpenWindow();
            ListBox commits = Find<ListBox>(window, "CommitList");
            Find<TabControl>(window, "DetailTabs").SelectedItem = Find<TabItem>(window, "DiffTab");
            ListBox files = Find<ListBox>(window, "CommitFilesList");
            Select(commits, "side");
            commits.SelectedItems!.Add(Item(commits, "third"));
            WaitUntil(() => files.Items.OfType<DiffGroupHeader>().Count() == 3);
            DiffGroupHeader first = files.Items.OfType<DiffGroupHeader>().First();
            CommitFile file = files.Items.OfType<CommitFile>().Last();
            files.SelectedItem = file;
            WaitUntil(() => files.ContainerFromItem(file) is not null);
            files.ContainerFromItem(file)!.Focus();

            window.KeyPress(Key.A, TestKeys.Command, PhysicalKey.A, keySymbol: null);

            files.SelectedItems!.Cast<object>().Should().AllBeOfType<CommitFile>()
                .And.Subject.Cast<CommitFile>().Should().HaveCount(2)
                .And.OnlyContain(selected => selected.Group == first.Group, "upstream's FirstGroupItems");
            window.Close();
        }
        finally
        {
            AppSettings.ShowDiffForAllParents = allParents;
        }
    }

    private static void ClickItem(Window window, ListBox list, object item, double x = 20)
    {
        list.ScrollIntoView(item);
        Dispatcher.UIThread.RunJobs();
        List<ListBoxItem> containers = [.. list.GetVisualDescendants().OfType<ListBoxItem>()];
        ListBoxItem container = containers.SingleOrDefault(candidate => Equals(candidate.DataContext, item))
                                ?? throw new InvalidOperationException(
                                    $"No container for {item} among {string.Join(", ", containers.Select(candidate => candidate.DataContext))}");
        Point point = container.TranslatePoint(new Point(x, container.Bounds.Height / 2), window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
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

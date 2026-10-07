using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using GitCommands;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;
using GitUIPluginInterfaces;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

internal sealed class SortingSettingsTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(20);

    private TestRepository _repo = null!;

    [SetUp]
    public void Setup()
    {
        _repo = new TestRepository();
        Reset();
    }

    [TearDown]
    public void TearDown()
    {
        Reset();
        _repo.Dispose();
    }

    [AvaloniaTest]
    public void The_sorting_tab_lists_upstreams_choices_and_saves_them()
    {
        MainWindow window = NewWindow();
        window.Show();
        Click(window, "SettingsMenuItem");
        WaitUntil(() => window.OwnedWindows.OfType<SettingsWindow>().Any());
        SettingsWindow settings = window.OwnedWindows.OfType<SettingsWindow>().Single();
        WaitUntil(() => settings.IsGitConfigLoaded);

        ComboBox revisions = Find<ComboBox>(settings, "RevisionSortBox");
        ComboBox sortBy = Find<ComboBox>(settings, "BranchesSortByBox");
        ComboBox order = Find<ComboBox>(settings, "BranchesOrderBox");
        Labels(revisions).Should().Equal("GitDefault", "AuthorDate", "Topology");
        Labels(sortBy).Should().Equal("Git default", "Author date", "Committer date", "Creator date", "Tagger date",
            "Alpha-numeric", "Version", "Object size", "Originating remote");
        Labels(order).Should().Equal("A ↓ Z", "Z ↑ A");
        order.SelectedItem!.ToString().Should().Be("Z ↑ A");
        Find<TextBox>(settings, "PrioritizedBranchesBox").Text.Should()
            .Be(InMemoryAppPreferences.DefaultPrioritizedBranchNames);

        revisions.SelectedIndex = 2;
        sortBy.SelectedIndex = 5;
        order.SelectedIndex = 0;
        Find<TextBox>(settings, "PrioritizedBranchesBox").Text = "dev";
        Find<TextBox>(settings, "PrioritizedRemotesBox").Text = "fork";
        Click(settings, "SaveButton");
        WaitUntil(() => !window.OwnedWindows.OfType<SettingsWindow>().Any());

        IAppPreferences preferences = TestAppBuilder.Preferences;
        (preferences.RevisionSortOrder, preferences.RefsSortBy, preferences.RefsSortOrder,
                preferences.PrioritizedBranchNames, preferences.PrioritizedRemoteNames)
            .Should().Be((RevisionSortOrder.Topology, GitRefsSortBy.refname, GitRefsSortOrder.Ascending, "dev", "fork"));
    }

    [AvaloniaTest]
    public void The_branch_tree_follows_the_sort_its_menu_chooses_and_the_priorities()
    {
        string head = _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim();
        _repo.Run("branch", "alpha");
        _repo.Run("branch", "beta");
        TestAppBuilder.Preferences.RefsSortBy = GitRefsSortBy.refname;
        TestAppBuilder.Preferences.RefsSortOrder = GitRefsSortOrder.Ascending;
        TestAppBuilder.Preferences.PrioritizedBranchNames = "";
        MainWindow window = OpenWindow();
        TreeView branches = Find<TreeView>(window, "BranchTreeView");
        string[] ascending = [.. new[] { "alpha", "beta", head }.Order(StringComparer.Ordinal)];
        WaitUntil(() => LocalBranches(branches).SequenceEqual(ascending));

        MenuItem descending = Find<MenuItem>(window, "SortOrderMenuItem").Items.OfType<MenuItem>()
            .Single(item => item.Header as string == "Z ↑ A");
        descending.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

        WaitUntil(() => LocalBranches(branches).SequenceEqual(ascending.Reverse()));
        TestAppBuilder.Preferences.RefsSortOrder.Should().Be(GitRefsSortOrder.Descending);

        TestAppBuilder.Preferences.PrioritizedBranchNames = "alpha";
        Click(window, "RefreshButton");
        WaitUntil(() => LocalBranches(branches).First() == "alpha");
    }

    [AvaloniaTest]
    public void The_grid_reads_commits_in_the_chosen_order()
    {
        // "older" is authored last but committed first; "newer" the other way round.
        string head = _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim();
        Commit("newer", authorDate: "2030-01-01T00:00:00", commitDate: "2020-01-01T00:00:00");
        _repo.Run("checkout", "-q", "-b", "other", "HEAD~1");
        Commit("older", authorDate: "2001-01-01T00:00:00", commitDate: "2021-01-01T00:00:00");
        _repo.Run("checkout", "-q", head);
        RevisionFilter all = RevisionFilter.AllBranches;

        CommitPage byAuthor = new GitCommitHistory(sortOrder: () => RevisionSortOrder.AuthorDate)
            .LoadPageAsync(_repo.Path, limit: 10, all).GetAwaiter().GetResult();
        Subjects(byAuthor).Should().Equal("newer", "older");

        // Upstream's own setting still adds its flag first; git's default order shows only while that is the default too.
        Assume.That(AppSettings.RevisionSortOrder.Value, Is.EqualTo(RevisionSortOrder.GitDefault));
        CommitPage byDefault = new GitCommitHistory(sortOrder: () => RevisionSortOrder.GitDefault)
            .LoadPageAsync(_repo.Path, limit: 10, all).GetAwaiter().GetResult();
        Subjects(byDefault).Should().Equal("older", "newer");
    }

    // The two commits only: git's default order may show their parent, committed now, between them.
    private static IEnumerable<string> Subjects(CommitPage page)
        => page.Rows.Select(row => row.Subject).Where(subject => subject is "newer" or "older");

    private void Commit(string subject, string authorDate, string commitDate)
    {
        File.WriteAllText(Path.Combine(_repo.Path, subject + ".txt"), subject);
        _repo.Run("add", subject + ".txt");
        Environment.SetEnvironmentVariable("GIT_COMMITTER_DATE", commitDate);
        try
        {
            _repo.Run("commit", "-q", "-m", subject, "--date", authorDate);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GIT_COMMITTER_DATE", null);
        }
    }

    private static void Reset()
    {
        IAppPreferences preferences = TestAppBuilder.Preferences;
        preferences.RevisionSortOrder = RevisionSortOrder.GitDefault;
        preferences.RefsSortBy = GitRefsSortBy.Default;
        preferences.RefsSortOrder = GitRefsSortOrder.Descending;
        preferences.PrioritizedBranchNames = InMemoryAppPreferences.DefaultPrioritizedBranchNames;
        preferences.PrioritizedRemoteNames = InMemoryAppPreferences.DefaultPrioritizedRemoteNames;
    }

    private static IEnumerable<string> Labels(ComboBox box) => box.Items.Select(item => item!.ToString()!);

    private static IReadOnlyList<string> LocalBranches(TreeView tree)
        => (tree.ItemsSource as IReadOnlyList<BranchTreeNode>)?
            .First(root => root.Name == BranchTree.LocalGroupName)
            .Descendants().Where(node => node.Branch is not null).Select(node => node.Branch!.Name).ToList() ?? [];

    private static MainWindow NewWindow() =>
        new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));

    private MainWindow OpenWindow()
    {
        MainWindow window = NewWindow();
        window.Show();
        Find<TextBox>(window, "PathBox").Text = _repo.Path;
        Click(window, "OpenButton");
        WaitUntil(() => Find<Button>(window, "OpenButton").IsEnabled);
        return window;
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
}

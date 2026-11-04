using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using GitCommands;
using GitCommands.Settings;
using GitExtensions.Extensibility.BuildServerIntegration;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Settings;
using GitExtensions.Xplat.Core.BuildServer;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Settings;
using GitUIPluginInterfaces.BuildServerIntegration;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

internal sealed class FileHistoryWindowTests
{
    private const string FakeBuildServer = "Fake CI";
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private TestRepository _repo = null!;

    // The shared settings the tests change, restored after each.
    private GridColumns _columns = null!;
    private bool _showAvailableDiffTools;
    private IReadOnlyList<string> _revisionFilterDropdowns = [];
    private bool _showOnlyFirstParent;

    [SetUp]
    public void Setup()
    {
        IAppPreferences preferences = TestAppBuilder.Preferences;
        _columns = preferences.GridColumns;
        _showAvailableDiffTools = preferences.ShowAvailableDiffTools;
        _revisionFilterDropdowns = preferences.RevisionFilterDropdowns;
        _showOnlyFirstParent = preferences.ShowOnlyFirstParent;
        _repo = new TestRepository();

        // a.txt ("first", "second") is renamed to b.txt, which "third" changes.
        _repo.Run("mv", "a.txt", "b.txt");
        _repo.Run("commit", "-q", "-m", "renamed");
        File.WriteAllText(Path.Combine(_repo.Path, "b.txt"), "three");
        _repo.Run("commit", "-q", "-am", "third");
        Reset();
    }

    [TearDown]
    public void TearDown()
    {
        Reset();
        IAppPreferences preferences = TestAppBuilder.Preferences;
        preferences.GridColumns = _columns;
        preferences.ShowAvailableDiffTools = _showAvailableDiffTools;
        preferences.RevisionFilterDropdowns = _revisionFilterDropdowns;
        preferences.ShowOnlyFirstParent = _showOnlyFirstParent;
        TestAppBuilder.UseBuildServers(new FixedBuildServerCatalog());
        _repo.Dispose();
    }

    [AvaloniaTest]
    public void The_history_follows_the_files_renames_and_shows_the_diff_of_the_selected_commit()
    {
        FileHistoryWindow history = Open("b.txt");

        Subjects(history).Should().Equal("third", "renamed", "second", "first");
        history.Title.Should().StartWith("File History - b.txt - ");
        Find<TabControl>(history, "Tabs").SelectedItem.Should().Be(Find<TabItem>(history, "DiffTab"));
        DiffView diff = Find<DiffView>(history, "Diff");
        WaitUntil(() => diff.Lines.Any(line => line.Text == "+three"));

        Select(history, "second");

        history.Title.Should().StartWith("File History - b.txt (a.txt) - ");
        WaitUntil(() => diff.Lines.Any(line => line.Text == "+two"));
    }

    [AvaloniaTest]
    public void The_menu_turns_off_following_renames_and_keeps_the_choice()
    {
        FileHistoryWindow history = Open("b.txt");

        Click(history, "FollowRenamesMenuItem");

        WaitUntil(() => Subjects(history).SequenceEqual(["third", "renamed"]));
        TestAppBuilder.Preferences.FileHistory.FollowRenames.Should().BeFalse();
        Find<MenuItem>(history, "FollowExactRenamesMenuItem").IsEnabled.Should().BeFalse();
    }

    [AvaloniaTest]
    public void Blame_opens_on_its_tab_and_a_line_selects_its_commit()
    {
        FileHistoryWindow history = Open("b.txt", showBlame: true);

        Find<TabControl>(history, "Tabs").SelectedItem.Should().Be(Find<TabItem>(history, "BlameTab"));
        BlameView blame = Find<BlameView>(history, "Blame");
        WaitUntil(() => blame.Lines.Count == 1);
        Select(history, "second");
        WaitUntil(() => history.Shown.IsCompleted && blame.Lines.Count == 1 && blame.Lines[0].Content == "two");

        ListBox lines = blame.FindControl<ListBox>("BlameList")!;
        lines.SelectedIndex = 0;
        lines.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });

        WaitUntil(() => history.Commits.Selected?.Subject == "second");
    }

    [AvaloniaTest]
    public void Without_load_on_show_the_history_waits_for_load()
    {
        TestAppBuilder.Preferences.FileHistory =
            new FileHistoryOptions(LoadHistoryOnShow: false, LoadBlameOnShow: false);

        FileHistoryWindow history = Open("b.txt");

        Find<ListBox>(history, "HistoryList").IsVisible.Should().BeFalse();
        Click(history, "LoadButton");
        WaitUntil(() => Find<ListBox>(history, "HistoryList").IsVisible && Subjects(history).Count == 4);
    }

    [AvaloniaTest]
    public void A_commit_without_the_file_has_only_the_commit_tab()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "c.txt"), "c");
        _repo.Run("add", "c.txt");
        _repo.Run("commit", "-q", "-m", "add c");
        _repo.Run("rm", "-q", "c.txt");
        _repo.Run("commit", "-q", "-m", "remove c");

        FileHistoryWindow history = Open("c.txt");

        WaitUntil(() => history.Shown.IsCompleted && !Find<TabItem>(history, "DiffTab").IsVisible);
        Find<TabControl>(history, "Tabs").SelectedItem.Should().Be(Find<TabItem>(history, "CommitTab"));
        Find<TextBlock>(history, "CommitTabText").Text.Should()
            .Be("Commit - Git could not identify the file \"c.txt\"");
        WaitUntil(() => Find<CommitDiffView>(history, "CommitDiffPanel").Info.Details?.Message.Contains("remove c")
                        == true);
    }

    [AvaloniaTest]
    public void Double_click_on_a_commit_opens_it_in_upstreams_commit_diff_window()
    {
        FileHistoryWindow history = Open("a.txt");

        Find<ListBox>(history, "HistoryList").RaiseEvent(new TappedEventArgs(InputElement.DoubleTappedEvent, null!));

        WaitUntil(() => history.OwnedWindows.OfType<CommitDiffWindow>().Any());
        CommitDiffWindow diff = history.OwnedWindows.OfType<CommitDiffWindow>().Single();
        WaitUntil(() => diff.RevisionLoaded.IsCompleted);
        diff.Title.Should().StartWith("Diff - ");
        diff.Close();
    }

    [AvaloniaTest]
    public void The_menu_has_upstreams_copy_items_and_difftool_against_the_working_directory()
    {
        FileHistoryWindow history = Open("b.txt");
        Select(history, "second");
        ListBox list = Find<ListBox>(history, "HistoryList");

        list.RaiseEvent(new ContextRequestedEventArgs());
        Dispatcher.UIThread.RunJobs();

        MenuItem copy = Find<MenuItem>(history, "CopyToClipboardMenuItem");
        string shortHash = history.Commits.Selected!.Hash[..8];
        copy.Items.OfType<MenuItem>().Select(item => item.Header as string)
            .Should().Contain(header => header != null && header.Contains(shortHash, StringComparison.Ordinal));
        Find<MenuItem>(history, "DiffToolRemoteLocalMenuItem").IsEnabled.Should().BeTrue("b.txt exists in the work tree");
        list.ContextMenu!.Close();
    }

    [AvaloniaTest]
    public void Two_selected_commits_compare_the_file_between_them_and_the_menu_follows_upstreams_rules()
    {
        FileHistoryWindow history = Open("b.txt");
        Select(history, "third");
        ListBox list = Find<ListBox>(history, "HistoryList");

        list.SelectedItems!.Add(history.Commits.VisibleRows.Single(item => item.Row.Subject == "renamed"));

        WaitUntil(() => history.SelectedRevisions.Count == 2 && history.Shown.IsCompleted);
        history.SelectedRevisions.Select(row => row.Subject).Should().Equal("renamed", "third");
        DiffView diff = Find<DiffView>(history, "Diff");
        WaitUntil(() => diff.Lines.Any(line => line.Text == "-three") && diff.Lines.Any(line => line.Text == "+two"));

        list.RaiseEvent(new ContextRequestedEventArgs());
        Dispatcher.UIThread.RunJobs();
        Find<MenuItem>(history, "OpenWithDifftoolMenuItem").IsEnabled.Should().BeTrue("two commits are compared");
        Find<MenuItem>(history, "DiffToolRemoteLocalMenuItem").IsEnabled.Should().BeFalse();
        Find<MenuItem>(history, "ManipulateCommitMenuItem").IsEnabled.Should().BeFalse();
        Find<MenuItem>(history, "SaveAsMenuItem").IsEnabled.Should().BeFalse();
        list.ContextMenu!.Close();
    }

    [AvaloniaTest]
    public void The_difftool_items_offer_the_available_tools_until_the_dropdown_is_disabled()
    {
        TestAppBuilder.Preferences.ShowAvailableDiffTools = true;
        FileHistoryWindow history = Open("b.txt");
        MenuItem open = Find<MenuItem>(history, "OpenWithDifftoolMenuItem");
        MenuItem local = Find<MenuItem>(history, "DiffToolRemoteLocalMenuItem");

        WaitUntil(() => open.Items.Count > 0 && local.Items.Count > 0);
        open.Items.OfType<MenuItem>().Select(item => item.Tag as string).Should()
            .Equal("meld", "vscode", null);
        open.Items.OfType<MenuItem>().Last().Header.Should().Be("Disable this dropdown");

        open.Items.OfType<MenuItem>().Last().RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

        TestAppBuilder.Preferences.ShowAvailableDiffTools.Should().BeFalse();
        open.Items.Should().BeEmpty();
        local.Items.Should().BeEmpty();
    }

    [AvaloniaTest]
    public void The_filter_toolbar_filters_the_files_history()
    {
        FileHistoryWindow history = Open("b.txt");
        ComboBox box = history.FilterToolBar.FindControl<ComboBox>("RevisionFilterBox")!;

        box.Text = "second";
        box.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });

        WaitUntil(() => Subjects(history).SequenceEqual(["second"]));
        history.Commits.Filter.PathFilter.Should().Be("b.txt", "the file's filter stays");
        history.FilterToolBar.FindControl<ToolSplitButton>("AdvancedFilterButton")!.Image.Should()
            .Be("FunnelExclamation");

        Click(history, "LoadButton");
        WaitUntil(() => history.Commits.Filter.Message == "second" && Subjects(history).SequenceEqual(["second"]));
    }

    [AvaloniaTest]
    public void The_navigate_menu_moves_over_the_grid_and_back()
    {
        FileHistoryWindow history = Open("b.txt");
        Select(history, "third");

        Click(history, "GoToParentMenuItem");
        WaitUntil(() => history.Commits.Selected?.Subject == "renamed");

        Click(history, "NavigateBackwardMenuItem");
        WaitUntil(() => history.Commits.Selected?.Subject == "third");
        Find<MenuItem>(history, "GoToParentMenuItem").InputGesture.Should().NotBeNull("upstream's grid hotkey");
    }

    [AvaloniaTest]
    public void The_view_menu_changes_the_grids_columns()
    {
        FileHistoryWindow history = Open("b.txt");
        bool shown = TestAppBuilder.Preferences.GridColumns.ObjectId;

        Click(history, "ShowObjectIdColumnMenuItem");

        TestAppBuilder.Preferences.GridColumns.ObjectId.Should().Be(!shown);
        history.Resources["ShowObjectIdColumn"].Should().Be(!shown);
    }

    [AvaloniaTest]
    public void The_build_report_tab_follows_the_selected_commits_build()
    {
        TestAppBuilder.UseRevisionLinks();
        string head = _repo.Run("rev-parse", "HEAD").Trim();
        TestAppBuilder.UseBuildServers(new FixedBuildServerCatalog(new Dictionary<string, Func<IBuildServerAdapter>>
        {
            [FakeBuildServer] = () => new FakeAdapter(
            [
                new BuildInfo
                {
                    CommitHashList = [ObjectId.Parse(head)],
                    Status = BuildStatus.Success,
                    Url = "https://ci.example.com/build/7",
                },
            ]),
        }));
        DistributedSettings settings = AppServices.RevisionLinks.Open(_repo.Path);
        BuildServerSettings.ServerName[settings] = FakeBuildServer;
        BuildServerSettings.ShowBuildResultPage[settings] = true;
        AppServices.RevisionLinks.Save(settings);

        FileHistoryWindow history = Open("b.txt");
        TabItem report = Find<TabItem>(history, "BuildReportTab");

        WaitUntil(() => report.IsVisible);
        history.BuildServerType.Should().Be(FakeBuildServer);
        ToolTip.GetTip(Find<HyperlinkButton>(history, "OpenBuildReportLink")).Should()
            .Be("https://ci.example.com/build/7");
        report.Header.Should().Be(FileHistoryWindow.BuildReportTabCaption);

        // Upstream's hand cursor over a build status with a report; none over a commit without a build.
        StackPanel Status(string subject) => Find<ListBox>(history, "HistoryList").GetVisualDescendants()
            .OfType<StackPanel>()
            .Single(panel => panel.Classes.Contains("buildStatus")
                             && panel.DataContext is CommitListItem item && item.Row.Subject == subject);
        WaitUntil(() => Status("third").Cursor is not null);
        Status("renamed").Cursor.Should().BeNull();

        Select(history, "renamed");
        WaitUntil(() => !report.IsVisible);
    }

    private static void Reset() => TestAppBuilder.Preferences.FileHistory = new FileHistoryOptions();

    private FileHistoryWindow Open(string file, bool showBlame = false)
    {
        Window owner = new();
        owner.Show();
        FileHistoryWindow history = new(_repo.Path, TestAppBuilder.Preferences, file, revision: null, showBlame,
            (_, _) => Task.CompletedTask, (_, _) => Task.CompletedTask);
        history.Show(owner);
        WaitUntil(() => history.HistoryLoaded.IsCompleted);
        WaitUntil(() => history.Commits.Selected is not null || !Find<ListBox>(history, "HistoryList").IsVisible);
        WaitUntil(() => history.Shown.IsCompleted);
        return history;
    }

    private static IReadOnlyList<string> Subjects(FileHistoryWindow history)
        => [.. history.Commits.VisibleRows.Select(item => item.Row.Subject)];

    private static void Select(FileHistoryWindow history, string subject)
    {
        ListBox list = Find<ListBox>(history, "HistoryList");
        list.SelectedItem = history.Commits.VisibleRows.Single(item => item.Row.Subject == subject);
        WaitUntil(() => history.Commits.Selected?.Subject == subject && history.Shown.IsCompleted);
    }

    private static void Click(Window window, string name)
    {
        Control control = Find<Control>(window, name);
        control.RaiseEvent(new RoutedEventArgs(control switch
        {
            MenuItem => MenuItem.ClickEvent,
            SplitButton => SplitButton.ClickEvent,
            _ => Button.ClickEvent,
        }));
    }

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

    // A build server that reports the given builds once.
    private sealed class FakeAdapter(IReadOnlyList<BuildInfo> builds) : IBuildServerAdapter
    {
        public string UniqueKey => "https://ci.example.com";

        public void Initialize(IBuildServerWatcher buildServerWatcher, SettingsSource config, Action openSettings,
            Func<ObjectId, bool>? isCommitInRevisionGrid = null)
        {
        }

        public IObservable<BuildInfo> GetFinishedBuildsSince(IScheduler scheduler, DateTime? sinceDate = null)
            => builds.ToObservable(scheduler);

        public IObservable<BuildInfo> GetRunningBuilds(IScheduler scheduler) => Observable.Empty<BuildInfo>();

        public void Dispose()
        {
        }
    }
}

using Avalonia.Controls;
using Avalonia.Headless;
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
///  Upstream's "Find in commit files using git-grep" in the Diff tab (<see cref="GitGrepBox"/>): the box, the toolbar
///  button, the window and the "grep" group with the matches in the viewer.
/// </summary>
internal sealed class GitGrepTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private TestRepository _repo = null!;

    [SetUp]
    public void SetUp()
    {
        _repo = new TestRepository();
        File.WriteAllText(Path.Combine(_repo.Path, "found.txt"), "first\nthe needle here\nlast\n");
        File.WriteAllText(Path.Combine(_repo.Path, "other.txt"), "nothing\n");
        _repo.Run("add", "-A");
        _repo.Run("commit", "-q", "-m", "add files");
    }

    [TearDown]
    public void TearDown()
    {
        TestAppBuilder.Preferences.ShowFindInCommitFilesGitGrep = false;
        TestAppBuilder.Preferences.FindInFilesGitGrepTypeIndex = 1;
        _repo.Dispose();
    }

    [AvaloniaTest]
    public void The_box_lists_the_matching_files_in_upstreams_grep_group_and_the_viewer_shows_the_matches()
    {
        TestAppBuilder.Preferences.ShowFindInCommitFilesGitGrep = true;
        MainWindow window = OpenWindow();
        ListBox files = ShowCommitFiles(window);
        ComboBox box = Find<ComboBox>(window, "DiffFilesGitGrepBox");
        box.IsVisible.Should().BeTrue("upstream's setting shows the box");

        box.Text = "needle";

        WaitUntil(() => window.DiffGitGrep!.Searched.IsCompleted
                        && files.Items.OfType<CommitFile>().Any(file => file.Group?.IsGrep == true));
        files.Items.OfType<DiffGroupHeader>().Last().Group.IsGrep.Should().BeTrue("upstream lists the grep group last");
        files.Items.OfType<CommitFile>().Where(file => file.Group?.IsGrep == true).Select(file => file.Path)
            .Should().Equal("found.txt");
        Find<Button>(window, "DiffFilesGitGrepDeleteButton").IsVisible.Should().BeTrue();

        files.SelectedItem = files.Items.OfType<CommitFile>().Single(file => file.Group?.IsGrep == true);

        DiffView viewer = Find<DiffView>(window, "CommitDiff");
        WaitUntil(() => viewer.Lines.Any(line => line.NewNumber == "2"));
        DiffLineItem match = viewer.Lines.Single(line => line.NewNumber == "2");
        match.Text.Should().Be("the needle here");
        match.Runs!.Single(run => run.Bold).Text.Should().Be("needle");

        Find<Button>(window, "DiffFilesGitGrepDeleteButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        WaitUntil(() => window.DiffGitGrep!.Searched.IsCompleted
                        && files.Items.OfType<CommitFile>().Any() && !files.Items.OfType<DiffGroupHeader>().Any());
        window.Close();
    }

    [AvaloniaTest]
    public void The_toolbar_button_shows_and_hides_the_box_as_upstreams_input_box_choice()
    {
        MainWindow window = OpenWindow();
        ComboBox box = Find<ComboBox>(window, "DiffFilesGitGrepBox");
        SplitButton button = Find<SplitButton>(window, "DiffFilesGitGrepButton");
        box.IsVisible.Should().BeFalse();

        button.RaiseEvent(new RoutedEventArgs(SplitButton.ClickEvent));
        box.IsVisible.Should().BeTrue();
        TestAppBuilder.Preferences.ShowFindInCommitFilesGitGrep.Should().BeTrue();

        button.RaiseEvent(new RoutedEventArgs(SplitButton.ClickEvent));
        box.IsVisible.Should().BeFalse();
        window.DiffGitGrep!.Window.Should().BeNull("upstream's default shows the box only");
        window.Close();
    }

    [AvaloniaTest]
    public void The_hotkey_opens_upstreams_window_whose_Find_searches_and_whose_closing_clears_the_search()
    {
        MainWindow window = OpenWindow();
        ListBox files = ShowCommitFiles(window);
        files.SelectedItem = files.Items.OfType<CommitFile>().First();
        WaitUntil(() => files.ContainerFromItem(files.SelectedItem!) is not null);
        files.ContainerFromItem(files.SelectedItem!)!.Focus();

        window.KeyPress(Key.F, RawInputModifiers.Control | RawInputModifiers.Shift, PhysicalKey.F, keySymbol: null);

        WaitUntil(() => window.DiffGitGrep!.Window is not null);
        FindInCommitFilesGitGrepWindow grep = window.DiffGitGrep!.Window!;
        grep.GitGrepExpressionText = "needle";
        Find<Button>(grep, "btnSearch").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        WaitUntil(() => window.DiffGitGrep.Searched.IsCompleted
                        && files.Items.OfType<CommitFile>().Any(file => file.Group?.IsGrep == true));

        grep.Close();

        WaitUntil(() => window.DiffGitGrep.Searched.IsCompleted && !files.Items.OfType<DiffGroupHeader>().Any()
                        && files.Items.OfType<CommitFile>().Any());
        window.DiffGitGrep.Text.Should().BeEmpty("upstream clears a search the hidden box cannot keep");
        window.Close();
    }

    [AvaloniaTest]
    public void The_file_tree_follows_the_shared_setting_and_lists_only_the_files_git_grep_matched()
    {
        MainWindow window = OpenWindow();
        TabControl tabs = Find<TabControl>(window, "DetailTabs");
        tabs.SelectedItem = Find<TabItem>(window, "DiffTab");
        Find<SplitButton>(window, "DiffFilesGitGrepButton").RaiseEvent(new RoutedEventArgs(SplitButton.ClickEvent));
        tabs.SelectedItem = Find<TabItem>(window, "FileTreeTab");
        ComboBox box = Find<ComboBox>(window, "FileTreeGitGrepBox");
        box.IsVisible.Should().BeTrue("both lists follow upstream's ShowFindInCommitFilesGitGrep");
        TreeView tree = Find<TreeView>(window, "FileTreeView");
        WaitUntil(() => Files(tree).Contains("other.txt"));

        box.Text = "needle";

        WaitUntil(() => window.FileTreeGitGrep!.Searched.IsCompleted && Files(tree).SequenceEqual(["found.txt"]));
        tree.SelectedItem = ((IReadOnlyList<FileTreeNode>)tree.ItemsSource!).Single();
        DiffView viewer = Find<DiffView>(window, "FileContentView");
        WaitUntil(() => viewer.Lines.Any(line => line.NewNumber == "2"));
        viewer.Lines.Single(line => line.NewNumber == "2").Runs!.Single(run => run.Bold).Text.Should().Be("needle");

        Find<Button>(window, "FileTreeGitGrepDeleteButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        WaitUntil(() => Files(tree).Contains("other.txt"));
        window.Close();

        static IEnumerable<string> Files(TreeView tree)
            => (tree.ItemsSource as IReadOnlyList<FileTreeNode> ?? []).SelectMany(root => root.Files())
                .Select(file => file.Path);
    }

    private MainWindow OpenWindow()
    {
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        window.Show();
        window.OpenRepository(_repo.Path);
        WaitUntil(() => !window.IsBusy);
        return window;
    }

    private static ListBox ShowCommitFiles(MainWindow window)
    {
        Find<TabControl>(window, "DetailTabs").SelectedItem = Find<TabItem>(window, "DiffTab");
        ListBox commits = Find<ListBox>(window, "CommitList");
        WaitUntil(() => commits.Items.Cast<CommitListItem>().Any(item => item.Row.Subject == "add files"));
        commits.SelectedItem = commits.Items.Cast<CommitListItem>().First(item => item.Row.Subject == "add files");
        ListBox files = Find<ListBox>(window, "CommitFilesList");
        WaitUntil(() => files.Items.OfType<CommitFile>().Count() == 2);
        return files;
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

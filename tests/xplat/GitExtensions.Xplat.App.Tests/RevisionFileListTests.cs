using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using GitCommands;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Operations;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

/// <summary>
///  Upstream's FileStatusList in the browse window's Diff tab (group by, filter, NoFiles) and the commit picker's button.
/// </summary>
internal sealed class RevisionFileListTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private TestRepository _repo = null!;
    private DiffListSortType _sorting;

    [SetUp]
    public void SetUp()
    {
        _sorting = TestAppBuilder.Preferences.DiffListSorting;
        _repo = new TestRepository();
        File.WriteAllText(Path.Combine(_repo.Path, "z.md"), "z");
        _repo.Run("add", "z.md");
        _repo.Run("commit", "-q", "-m", "add z");
        File.WriteAllText(Path.Combine(_repo.Path, "z.md"), "changed");
        File.WriteAllText(Path.Combine(_repo.Path, "b.txt"), "b");
        _repo.Run("add", "-A");
        _repo.Run("commit", "-q", "-m", "change z add b");
    }

    [TearDown]
    public void TearDown()
    {
        TestAppBuilder.Preferences.DiffListSorting = _sorting;
        _repo.Dispose();
    }

    [AvaloniaTest]
    public void The_diff_tabs_files_are_grouped_and_filtered_as_upstreams_file_list()
    {
        TestAppBuilder.Preferences.DiffListSorting = DiffListSortType.FilePath;
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        window.Show();
        window.OpenRepository(_repo.Path);
        WaitUntil(() => !window.IsBusy);
        ListBox commits = Find<ListBox>(window, "CommitList");
        commits.SelectedItem = commits.Items.Cast<CommitListItem>().Single(item => item.Row.Subject == "change z add b");
        ListBox files = Find<ListBox>(window, "CommitFilesList");
        WaitUntil(() => files.ItemCount == 2);
        Paths(files).Should().Equal("b.txt", "z.md");
        files.Items.Cast<CommitFile>().Select(file => file.Image).Should().Equal("FileStatusAdded", "FileStatusModified");

        Find<ToolToggleButton>(window, "DiffFilesByStatusButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Paths(files).Should().Equal(["z.md", "b.txt"], "a modified file comes before an added one");

        TextBox filter = Find<TextBox>(window, "DiffFilesFilterBox");
        filter.Text = "\\.md$";
        Dispatcher.UIThread.RunJobs();
        Paths(files).Should().Equal("z.md");
        filter.Text = "nothing";
        Dispatcher.UIThread.RunJobs();
        TextBlock noFiles = Find<TextBlock>(window, "DiffFilesNoFilesText");
        noFiles.IsVisible.Should().BeTrue();
        noFiles.Text.Should().Be("No changes");
        filter.Text = "(";
        Dispatcher.UIThread.RunJobs();
        files.ItemCount.Should().Be(2, "an invalid expression filters nothing");
        ToolTip.GetTip(filter).Should().NotBeNull();
        window.Close();
    }

    [AvaloniaTest]
    public void The_commit_pickers_button_picks_a_commit_in_the_choose_commit_window()
    {
        Window owner = new();
        owner.Show();
        CreateBranchWindow create = new(_repo.Path, new RepositoryOperationsViewModel(new GitOperations()),
            TestAppBuilder.Preferences);
        _ = create.ShowDialog<bool>(owner);
        WaitUntil(() => create.BranchLoaded.IsCompleted);
        CommitPickerView picker = Find<CommitPickerView>(create, "CommitPicker");

        picker.GetLogicalDescendants().OfType<Button>().Single(button => button.Name == "buttonPickCommit")
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        WaitUntil(() => create.OwnedWindows.OfType<ChooseCommitWindow>().SingleOrDefault()?.CommitsLoaded.IsCompleted == true);
        ChooseCommitWindow choose = create.OwnedWindows.OfType<ChooseCommitWindow>().Single();
        ListBox grid = Find<ListBox>(choose, "revisionGrid");
        grid.SelectedItem = grid.Items.Cast<CommitListItem>().Single(item => item.Row.Subject == "first");
        Find<Button>(choose, "btnOK").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        string first = _repo.Run("rev-parse", "HEAD~3").Trim();
        WaitUntil(() => picker.Selected?.ToString() == first);
        owner.Close();
    }

    private static IEnumerable<string> Paths(ListBox files) => files.Items.Cast<CommitFile>().Select(file => file.Path);

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
        }
    }
}

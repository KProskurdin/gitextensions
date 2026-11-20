using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Threading;
using GitCommands;
using GitExtensions.Xplat.Core.CommitHistory;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

/// <summary>
///  Upstream's <c>FormLog</c>: the grid, the selected commits' files in upstream's diff groups, and a file's diff; the
///  browse window opens it for a double-click on its grid with nothing selected.
/// </summary>
internal sealed class LogWindowTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(20);

    private TestRepository _repo = null!;

    [SetUp]
    public void Setup() => _repo = new TestRepository();

    [TearDown]
    public void TearDown() => _repo.Dispose();

    [AvaloniaTest]
    public void The_selected_commits_files_are_listed_and_a_files_diff_shows()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "b.txt"), "bee");
        _repo.Run("add", "b.txt");
        _repo.Run("commit", "-q", "-m", "third");
        Window owner = new();
        owner.Show();
        LogWindow log = new(_repo.Path, TestAppBuilder.Preferences);
        _ = log.ShowDialog(owner);
        ListBox grid = Find<ListBox>(log, "RevisionGrid");
        WaitUntil(() => grid.ItemCount >= 3);

        // The latest commit alone: its own files. Then with the first commit: the files between the two, in upstream's
        // groups.
        grid.SelectedItem = grid.Items.OfType<CommitListItem>().First(item => item.Row.Subject == "third");
        WaitUntil(() => log.Files.Count > 0);
        log.Files.Select(file => file.Path).Should().Equal("b.txt");
        grid.SelectedItems!.Add(grid.Items.OfType<CommitListItem>().First(item => item.Row.Subject == "first"));
        WaitUntil(() => log.Files.Any(file => file.Path == "a.txt"));
        log.Files.Select(file => file.Path).Should().Contain(["a.txt", "b.txt"]);

        ListBox files = Find<ListBox>(log, "DiffFiles");
        files.SelectedItem = files.Items.OfType<CommitFile>().First(file => file.Path == "b.txt");
        DiffView diff = Find<DiffView>(log, "diffViewer");
        // The commit selected last is upstream's A, so the later file shows as removed.
        WaitUntil(() => diff.ViewModel.Lines.Any(line => line.Text == "-bee"));
        log.Title.Should().Be("Diff");
        log.Close();
        owner.Close();
    }

    [AvaloniaTest]
    public void A_double_click_on_the_browse_grid_with_nothing_selected_opens_the_window()
    {
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        window.Show();
        window.OpenRepository(_repo.Path);
        WaitUntil(() => !window.IsBusy);
        ListBox grid = Find<ListBox>(window, "CommitList");
        WaitUntil(() => grid.ItemCount > 0);

        grid.SelectedItems!.Clear();
        Dispatcher.UIThread.RunJobs();
        grid.RaiseEvent(new TappedEventArgs(InputElement.DoubleTappedEvent, null!));

        WaitUntil(() => window.OwnedWindows.OfType<LogWindow>().Any());
        window.OwnedWindows.OfType<LogWindow>().Single().Close();
        window.Close();
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
            Thread.Sleep(10);
        }
    }
}

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
///  Upstream's stash rows in the grid (<c>ShowStashes</c>) and the grid menu's stash items.
/// </summary>
internal sealed class GridStashTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private TestRepository _repo = null!;

    [SetUp]
    public void SetUp()
    {
        _repo = new TestRepository();
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "older stash");
        _repo.Run("stash", "push", "-q", "-m", "older");
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "newer stash");
        File.WriteAllText(Path.Combine(_repo.Path, "new.txt"), "untracked");
        _repo.Run("stash", "push", "-q", "-u", "-m", "newer");
    }

    [TearDown]
    public void TearDown()
    {
        TestAppBuilder.Preferences.ShowStashes = false;
        _repo.Dispose();
    }

    [Test]
    public async Task The_history_lists_the_stashes_before_the_commit_they_were_made_on()
    {
        GitCommitHistory history = new(showStashes: () => true);

        CommitPage page = await history.LoadPageAsync(_repo.Path, 100, RevisionFilter.AllBranches);

        List<CommitRow> stashes = [.. page.Rows.Where(row => row.IsStash)];
        stashes.Select(row => row.StashLabel).Should().Equal("stash@{0}", "stash@{1}");
        stashes[0].Subject.Should().Contain("newer");
        int second = page.Rows.ToList().FindIndex(row => row.Subject == "second");
        page.Rows.ToList().FindIndex(row => row.Hash == stashes[1].Hash).Should().BeLessThan(second);
        page.Rows.Should().Contain(row => row.Subject.StartsWith("untracked files on", StringComparison.Ordinal),
            "the newest stash's untracked files have a commit of their own");

        CommitPage hidden = await new GitCommitHistory().LoadPageAsync(_repo.Path, 100, RevisionFilter.AllBranches);
        hidden.Rows.Should().NotContain(row => row.IsStash);
    }

    [AvaloniaTest]
    public void A_stash_row_has_its_label_and_the_menus_pop_pops_it()
    {
        TestAppBuilder.Preferences.ShowStashes = true;
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        window.Show();
        window.OpenRepository(_repo.Path);
        WaitUntil(() => !window.IsBusy);
        ListBox commits = Find<ListBox>(window, "CommitList");
        CommitListItem older = commits.Items.Cast<CommitListItem>().Single(item => item.Row.StashLabel == "stash@{1}");
        older.GridLabels.Select(label => label.Text).Should().Contain("stash@{1}");
        Find<MenuItem>(window, "ShowStashesMenuItem").IsChecked.Should().BeTrue();

        commits.SelectedItem = older;
        commits.RaiseEvent(new Avalonia.Input.ContextRequestedEventArgs());
        Dispatcher.UIThread.RunJobs();
        commits.ContextMenu!.Close();
        Find<MenuItem>(window, "PopStashMenuItem").IsVisible.Should().BeTrue();
        Find<MenuItem>(window, "PopStashMenuItem").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

        WaitUntil(() => window.OwnedWindows.OfType<ErrorWindow>().FirstOrDefault() is { } error
            ? throw new AssertionException("The pop failed: " + error.FindControl<TextBlock>("MessageText")?.Text)
            : _repo.ReadFile("a.txt") == "older stash");
        _repo.Run("stash", "list").Trim().Split('\n').Should().ContainSingle().Which.Should().Contain("newer");
        window.Close();
    }

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

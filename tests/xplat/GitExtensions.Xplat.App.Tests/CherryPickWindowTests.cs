using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using GitCommands;
using GitExtensions.Extensibility;
using GitExtensions.Xplat.Core.CommitHistory;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

internal sealed class CherryPickWindowTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private TestRepository _repo = null!;
    private string _side = "";

    [SetUp]
    public void SetUp()
    {
        _repo = new TestRepository();
        _repo.Run("checkout", "-q", "-b", "side", "HEAD~1");
        File.WriteAllText(Path.Combine(_repo.Path, "b.txt"), "side");
        _repo.Run("add", "b.txt");
        _repo.Run("commit", "-q", "-m", "side change");
        _side = _repo.Run("rev-parse", "HEAD").Trim();
        _repo.Run("checkout", "-q", "-");
    }

    [TearDown]
    public void TearDown()
    {
        TestAppBuilder.Preferences.CommitAutomaticallyAfterCherryPick = false;
        TestAppBuilder.Preferences.AddCommitReferenceToCherryPick = false;
        _repo.Dispose();
    }

    [AvaloniaTest]
    public void The_grids_item_picks_the_commit_with_the_options_and_keeps_them()
    {
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        window.Show();
        window.OpenRepository(_repo.Path);
        WaitUntil(() => !window.IsBusy);
        ListBox commits = window.FindControl<ListBox>("CommitList")!;
        commits.SelectedItem = commits.Items.Cast<CommitListItem>().Single(item => item.Row.Subject == "side change");

        window.FindControl<MenuItem>("CherryPickMenuItem")!.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        WaitUntil(() => window.OwnedWindows.OfType<CherryPickWindow>().Any());
        CherryPickWindow pick = window.OwnedWindows.OfType<CherryPickWindow>().Single();
        WaitUntil(() => pick.RevisionLoaded.IsCompleted);
        Find<ListBox>(pick, "lvParentsList").IsVisible.Should().BeFalse("the commit is no merge");
        Find<CheckBox>(pick, "cbxAutoCommit").IsChecked = true;
        Find<CheckBox>(pick, "cbxAddReference").IsChecked = true;
        Click(pick, "btnPick");

        WaitUntil(() => !pick.IsVisible);
        WaitUntil(() => _repo.Run("log", "-1", "--format=%s").Trim() == "side change");
        _repo.Run("log", "-1", "--format=%b").Should().Contain($"(cherry picked from commit {_side})");
        TestAppBuilder.Preferences.CommitAutomaticallyAfterCherryPick.Should().BeTrue();
        TestAppBuilder.Preferences.AddCommitReferenceToCherryPick.Should().BeTrue();
        window.Close();
    }

    [AvaloniaTest]
    public void A_merge_needs_a_parent_and_offers_its_parents()
    {
        _repo.Run("merge", "-q", "--no-ff", "-m", "merge side", "side");
        Window owner = new();
        owner.Show();
        List<ArgumentString> picked = [];
        CherryPickWindow pick = new(_repo.Path, "HEAD", TestAppBuilder.Preferences, (_, _, arguments) =>
        {
            picked.Add(arguments);
            return Task.CompletedTask;
        });
        _ = pick.ShowDialog(owner);
        WaitUntil(() => pick.RevisionLoaded.IsCompleted);
        ListBox parents = Find<ListBox>(pick, "lvParentsList");
        parents.IsVisible.Should().BeTrue();
        parents.Items.Cast<CherryPickParent>().Select(parent => parent.Subject).Should().Equal("second", "side change");
        parents.SelectedIndex.Should().Be(0);

        parents.SelectedIndex = -1;
        Click(pick, "btnPick");
        WaitUntil(() => pick.OwnedWindows.OfType<MessageBoxWindow>().Any());
        pick.OwnedWindows.OfType<MessageBoxWindow>().Single().Close();
        picked.Should().BeEmpty();

        parents.SelectedIndex = 1;
        Click(pick, "btnPick");
        WaitUntil(() => picked.Count == 1);
        picked[0].ToString().Should().StartWith("cherry-pick --no-commit -m 2 ");
        owner.Close();
    }

    private static void Click(Window window, string name)
        => Find<Button>(window, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static T Find<T>(Window window, string name)
        where T : Control
        => window.FindControl<T>(name) ?? throw new InvalidOperationException($"No control named {name}");

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

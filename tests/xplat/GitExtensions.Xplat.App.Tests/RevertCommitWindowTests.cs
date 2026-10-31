using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using GitCommands;
using GitExtensions.Extensibility;
using GitExtensions.Xplat.Core.CommitHistory;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

internal sealed class RevertCommitWindowTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private TestRepository _repo = null!;

    [SetUp]
    public void SetUp() => _repo = new TestRepository();

    [TearDown]
    public void TearDown() => _repo.Dispose();

    [AvaloniaTest]
    public void The_grids_item_reverts_the_commit_with_the_output_shown()
    {
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        window.Show();
        window.OpenRepository(_repo.Path);
        WaitUntil(() => !window.IsBusy);
        ListBox commits = window.FindControl<ListBox>("CommitList")!;
        commits.SelectedItem = commits.Items.Cast<CommitListItem>().Single(item => item.Row.Subject == "second");

        window.FindControl<MenuItem>("RevertMenuItem")!.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        WaitUntil(() => window.OwnedWindows.OfType<RevertCommitWindow>().SingleOrDefault()?.RevisionLoaded.IsCompleted == true);
        RevertCommitWindow revert = window.OwnedWindows.OfType<RevertCommitWindow>().Single();
        Find<ListBox>(revert, "lvParentsList").IsVisible.Should().BeFalse("the commit is no merge");
        Find<CheckBox>(revert, "AutoCommit").IsChecked = true;
        Click(revert, "Revert");

        WaitUntil(() => !revert.IsVisible);
        WaitUntil(() => _repo.Run("log", "-1", "--format=%s").Trim() == "Revert \"second\"");
        _repo.ReadFile("a.txt").Should().Be("one");
        window.Close();
    }

    [AvaloniaTest]
    public void A_merge_needs_a_parent_and_the_command_names_it()
    {
        _repo.Run("checkout", "-q", "-b", "side", "HEAD~1");
        File.WriteAllText(Path.Combine(_repo.Path, "b.txt"), "side");
        _repo.Run("add", "b.txt");
        _repo.Run("commit", "-q", "-m", "side change");
        _repo.Run("checkout", "-q", "-");
        _repo.Run("merge", "-q", "--no-ff", "-m", "merge side", "side");
        Window owner = new();
        owner.Show();
        List<(ArgumentString Arguments, bool AutoCommit)> reverted = [];
        RevertCommitWindow revert = new(_repo.Path, "HEAD", (_, arguments, autoCommit) =>
        {
            reverted.Add((arguments, autoCommit));
            return Task.CompletedTask;
        });
        _ = revert.ShowDialog(owner);
        WaitUntil(() => revert.RevisionLoaded.IsCompleted);
        ListBox parents = Find<ListBox>(revert, "lvParentsList");
        parents.IsVisible.Should().BeTrue();
        parents.Items.Cast<CherryPickParent>().Select(parent => parent.Subject).Should().Equal("second", "side change");

        parents.SelectedIndex = -1;
        Click(revert, "Revert");
        WaitUntil(() => revert.OwnedWindows.OfType<MessageBoxWindow>().Any());
        revert.OwnedWindows.OfType<MessageBoxWindow>().Single().Close();
        reverted.Should().BeEmpty();

        parents.SelectedIndex = 1;
        Click(revert, "Revert");
        WaitUntil(() => reverted.Count == 1);
        reverted[0].Arguments.ToString().Should().StartWith("revert --no-commit -m 2 ");
        reverted[0].AutoCommit.Should().BeFalse();
        owner.Close();
    }

    private static void Click(Window window, string name)
        => Find<Button>(window, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

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

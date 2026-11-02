using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using GitCommands;
using GitExtensions.Extensibility;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Settings;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

internal sealed class ResetAnotherBranchWindowTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private TestRepository _repo = null!;

    [SetUp]
    public void SetUp() => _repo = new TestRepository();

    [TearDown]
    public void TearDown()
    {
        TestAppBuilder.Preferences.CheckoutOtherBranchAfterReset = true;
        _repo.Dispose();
    }

    [AvaloniaTest]
    public void The_grids_item_moves_a_branch_behind_the_commit_to_it()
    {
        _repo.Run("branch", "old", "HEAD~1");
        TestAppBuilder.Preferences.CheckoutOtherBranchAfterReset = false;
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        window.Show();
        window.OpenRepository(_repo.Path);
        WaitUntil(() => !window.IsBusy);
        ListBox commits = window.FindControl<ListBox>("CommitList")!;
        commits.SelectedItem = commits.Items.Cast<CommitListItem>().Single(item => item.Row.Subject == "second");

        window.FindControl<MenuItem>("ResetAnotherBranchMenuItem")!.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        WaitUntil(() =>
            window.OwnedWindows.OfType<ResetAnotherBranchWindow>().SingleOrDefault()?.BranchesLoaded.IsCompleted == true);
        ResetAnotherBranchWindow reset = window.OwnedWindows.OfType<ResetAnotherBranchWindow>().Single();
        ComboBox branches = Find<ComboBox>(reset, "Branches");
        branches.Items.Cast<string>().Should().Equal(["old"], "the current branch is left out");
        branches.IsDropDownOpen = false;
        branches.Text = "old";
        WaitUntil(() => reset.Validated.IsCompleted && Find<Button>(reset, "Ok").IsEnabled);
        Click(reset, "Ok");

        WaitUntil(() => !reset.IsVisible);
        WaitUntil(() => _repo.Run("rev-parse", "old").Trim() == _repo.Run("rev-parse", "HEAD").Trim());
        _repo.Run("symbolic-ref", "--short", "HEAD").Trim().Should().NotBe("old", "checking out was not asked");
        window.Close();
    }

    [AvaloniaTest]
    public void A_branch_that_is_no_ancestor_needs_the_force_reset()
    {
        _repo.Run("checkout", "-q", "-b", "side", "HEAD~1");
        File.WriteAllText(Path.Combine(_repo.Path, "b.txt"), "side");
        _repo.Run("add", "b.txt");
        _repo.Run("commit", "-q", "-m", "side change");
        _repo.Run("checkout", "-q", "-");
        (ResetAnotherBranchWindow reset, Window owner, List<string> ran, List<string> checkedOut) = Open("HEAD");
        ComboBox branches = Find<ComboBox>(reset, "Branches");
        Button ok = Find<Button>(reset, "Ok");
        branches.IsDropDownOpen = false;

        branches.Text = "nothing";
        ok.IsEnabled.Should().BeFalse("no listed branch has that name");
        branches.Text = "side";
        WaitUntil(() => reset.Validated.IsCompleted);
        Dispatcher.UIThread.RunJobs();
        ok.IsEnabled.Should().BeFalse("side has a commit the target lacks");

        Find<CheckBox>(reset, "ForceReset").IsChecked = true;
        ok.IsEnabled.Should().BeTrue();
        Find<CheckBox>(reset, "cbxCheckoutBranch").IsChecked = true;
        Click(reset, "Ok");
        WaitUntil(() => checkedOut.Count == 1);
        ran.Should().ContainSingle().Which.Should().Be($"update-ref \"refs/heads/side\" {_repo.Run("rev-parse", "HEAD").Trim()}");
        checkedOut.Should().Equal("side");
        TestAppBuilder.Preferences.CheckoutOtherBranchAfterReset.Should().BeTrue();
        owner.Close();
    }

    [AvaloniaTest]
    public void The_branch_tracking_the_commits_remote_branch_is_proposed_first()
    {
        _repo.Run("update-ref", "refs/remotes/origin/feature", "HEAD");
        _repo.Run("branch", "aaa", "HEAD~1");
        _repo.Run("branch", "feature", "HEAD~1");
        _repo.Run("config", "branch.feature.remote", "origin");
        _repo.Run("config", "branch.feature.merge", "refs/heads/feature");
        _repo.Run("branch", "here", "HEAD");

        (ResetAnotherBranchWindow reset, Window owner, _, _) = Open("HEAD");

        ComboBox branches = Find<ComboBox>(reset, "Branches");
        branches.Items.Cast<string>().Should().Equal(["feature", "aaa"], "a branch already at the commit is left out");
        branches.Text.Should().Be("feature");
        owner.Close();
    }

    private (ResetAnotherBranchWindow Reset, Window Owner, List<string> Ran, List<string> CheckedOut) Open(
        string revision)
    {
        Window owner = new();
        owner.Show();
        List<string> ran = [];
        List<string> checkedOut = [];
        InMemoryAppPreferences preferences = TestAppBuilder.Preferences;
        ResetAnotherBranchWindow reset = new(_repo.Path, revision, preferences,
            (_, arguments) =>
            {
                ran.Add(arguments.ToString());
                return Task.FromResult(true);
            },
            (_, branch) =>
            {
                checkedOut.Add(branch);
                return Task.CompletedTask;
            });
        _ = reset.ShowDialog(owner);
        WaitUntil(() => reset.BranchesLoaded.IsCompleted);
        return (reset, owner, ran, checkedOut);
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

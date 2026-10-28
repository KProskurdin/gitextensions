using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

internal sealed class DeleteRemoteBranchWindowTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private TestRepository _repo = null!;
    private string _bare = "";
    private readonly List<string> _deletedOnRemote = [];
    private readonly List<string> _deletedLocally = [];

    [SetUp]
    public void SetUp()
    {
        _repo = new TestRepository();
        _bare = _repo.Path.TrimEnd('/', '\\') + "-remote.git";
        _repo.Run("init", "--bare", "-q", _bare);
        _repo.Run("remote", "add", "origin", _bare);
        _repo.Run("branch", "merged", "HEAD~1");
        _repo.Run("push", "-q", "origin", "merged");
        _repo.Run("checkout", "-q", "-b", "feature");
        File.WriteAllText(Path.Combine(_repo.Path, "f.txt"), "feature");
        _repo.Run("add", "f.txt");
        _repo.Run("commit", "-q", "-m", "feature work");
        _repo.Run("push", "-q", "-u", "origin", "feature");
        _repo.Run("checkout", "-q", "-");
        _deletedOnRemote.Clear();
        _deletedLocally.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        _repo.Dispose();
        GitProcess.DeleteFolder(_bare);
    }

    [AvaloniaTest]
    public void A_merged_branch_is_deleted_once_Delete_branches_from_remote_is_checked()
    {
        (Window owner, DeleteRemoteBranchWindow window, Task<bool> result) = Open("origin/merged");
        Button delete = Find<Button>(window, "Delete");
        delete.IsEnabled.Should().BeFalse();
        Find<CheckBox>(window, "DeleteLocalTrackingBranch").IsEnabled.Should().BeFalse("no local branch tracks it");
        Find<TextBlock>(window, "_NO_TRANSLATE_labelLocalTrackingBranches").Text.Should().BeEmpty();

        Find<CheckBox>(window, "DeleteRemote").IsChecked = true;
        delete.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        WaitUntil(() => result.IsCompleted);
        result.Result.Should().BeTrue();
        _deletedOnRemote.Should().Equal("origin:merged");
        _deletedLocally.Should().BeEmpty();
        owner.Close();
    }

    [AvaloniaTest]
    public void An_unmerged_branch_asks_first_and_offers_its_local_tracking_branch()
    {
        (Window owner, DeleteRemoteBranchWindow window, Task<bool> result) = Open("origin/feature");
        CheckBox tracking = Find<CheckBox>(window, "DeleteLocalTrackingBranch");
        tracking.IsEnabled.Should().BeTrue();
        Find<TextBlock>(window, "_NO_TRANSLATE_labelLocalTrackingBranches").Text.Should().Contain(" - feature");
        Find<CheckBox>(window, "DeleteRemote").IsChecked = true;
        tracking.IsChecked = true;

        Find<Button>(window, "Delete").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Answer(window, "NoButton");
        _deletedOnRemote.Should().BeEmpty("No keeps the branch");

        Find<Button>(window, "Delete").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Answer(window, "YesButton");

        WaitUntil(() => result.IsCompleted);
        _deletedOnRemote.Should().Equal("origin:feature");
        _deletedLocally.Should().Equal("feature");
        owner.Close();
    }

    private (Window Owner, DeleteRemoteBranchWindow Window, Task<bool> Result) Open(string branch)
    {
        Window owner = new();
        owner.Show();
        DeleteRemoteBranchWindow window = new(_repo.Path, branch,
            (remote, branches) =>
            {
                _deletedOnRemote.AddRange(branches.Select(name => $"{remote}:{name}"));
                return Task.FromResult(true);
            },
            (_, branches) =>
            {
                _deletedLocally.AddRange(branches);
                return Task.CompletedTask;
            });
        Task<bool> result = window.ShowDialog<bool>(owner);
        WaitUntil(() => window.BranchesLoaded.IsCompleted);
        Find<ComboBox>(window, "Branches").Items.Cast<string>().Should().Contain(["origin/merged", "origin/feature"]);
        return (owner, window, result);
    }

    private static void Answer(Window window, string button)
    {
        WaitUntil(() => window.OwnedWindows.OfType<MessageBoxWindow>().Any());
        MessageBoxWindow question = window.OwnedWindows.OfType<MessageBoxWindow>().Single();
        question.Title.Should().Be("Delete remote branches");
        question.GetLogicalDescendants().OfType<Button>().Single(choice => choice.Name == button)
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        WaitUntil(() => !question.IsVisible);
    }

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

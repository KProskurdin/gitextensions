using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using GitCommands;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

internal sealed class CommitStatusBarUiTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(20);

    private TestRepository _repo = null!;

    [SetUp]
    public void Setup() => _repo = new TestRepository();

    [TearDown]
    public void TearDown() => _repo.Dispose();

    [AvaloniaTest]
    public void The_status_bar_shows_committer_branch_staged_count_and_caret()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "changed");
        _repo.Run("add", "a.txt");
        File.WriteAllText(Path.Combine(_repo.Path, "new.txt"), "new");
        string branch = _repo.Run("branch", "--show-current").Trim();
        CommitWindow commit = OpenCommitWindow();

        WaitUntil(() => Text(commit, "CommitterText") == "Committer Test <test@example.com>");
        WaitUntil(() => Text(commit, "BranchNameText") == $"{branch} →");
        Text(commit, "RemoteNameText").Should().Be("(remote not configured)", "the repository has no remote");
        commit.Title.Should().StartWith($"Commit to {branch} (");
        WaitUntil(() => Text(commit, "StagedCountText") == "1/2");

        TextBox message = Find<TextBox>(commit, "CommitMessageBox");
        message.Text = "subject\n\nbody";
        message.CaretIndex = 11;

        WaitUntil(() => (Text(commit, "CaretLineText"), Text(commit, "CaretColumnText")) == ("3", "3"));
        commit.Close();
    }

    [AvaloniaTest]
    public void The_branch_pushes_to_origin_untracked_and_the_author_is_added()
    {
        _repo.Run("remote", "add", "origin", "https://example.com/repo.git");
        string branch = _repo.Run("branch", "--show-current").Trim();
        CommitWindow commit = OpenCommitWindow();

        WaitUntil(() => Text(commit, "RemoteNameText") == $"origin/{branch} (untracked)");

        TextBox author = Find<TextBox>(commit, "CommitAuthorBox");
        author.Text = "Other <other@example.com>";
        author.RaiseEvent(new FocusChangedEventArgs(InputElement.LostFocusEvent));

        WaitUntil(() => Text(commit, "CommitterText")
                        == "Committer Test <test@example.com> Author Other <other@example.com>");
        commit.Close();
    }

    private static string? Text(Window window, string name) => Find<TextBlock>(window, name).Text;

    private CommitWindow OpenCommitWindow()
    {
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        window.Show();
        window.OpenRepository(_repo.Path);
        WaitUntil(() => !window.IsBusy);
        WaitUntil(() => Find<Button>(window, "CommitDialogButton").IsEnabled);
        Find<Button>(window, "CommitDialogButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        WaitUntil(() => window.OwnedWindows.OfType<CommitWindow>().Any());
        return window.OwnedWindows.OfType<CommitWindow>().Single();
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

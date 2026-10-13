using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using GitCommands;
using GitExtensions.Xplat.Core.Settings;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

internal sealed class CommitBottomPanelTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(20);

    private TestRepository _repo = null!;

    [SetUp]
    public void Setup()
    {
        _repo = new TestRepository();
        Reset();
    }

    [TearDown]
    public void TearDown()
    {
        Reset();
        _repo.Dispose();
    }

    [AvaloniaTest]
    public void The_options_sign_off_set_the_author_and_no_verify_is_cleared_after_the_commit()
    {
        StageChange();
        CommitWindow commit = OpenCommitWindow();
        Find<MenuItem>(commit, "SignOffMenuItem").IsChecked = true;
        Find<MenuItem>(commit, "NoVerifyMenuItem").IsChecked = true;
        Find<TextBox>(commit, "CommitAuthorBox").Text = "Other <other@example.com>";
        WaitUntil(() => Find<MenuItem>(commit, "AuthorLabelMenuItem").IsChecked);
        Find<TextBox>(commit, "CommitMessageBox").Text = "with options";

        Click(commit, "CommitButton");

        WaitUntil(() => _repo.Run("log", "-1", "--format=%s").Trim() == "with options");
        _repo.Run("log", "-1", "--format=%an <%ae>").Trim().Should().Be("Other <other@example.com>");
        _repo.Run("log", "-1", "--format=%B").Should().Contain("Signed-off-by: Test <test@example.com>");
        WaitUntil(() => !Find<MenuItem>(commit, "NoVerifyMenuItem").IsChecked);
        commit.Close();
    }

    [AvaloniaTest]
    public void The_window_closes_after_the_commit_that_leaves_no_unstaged_changes()
    {
        StageChange();
        CommitWindow commit = OpenCommitWindow();
        Find<TextBox>(commit, "CommitMessageBox").Text = "last";

        Click(commit, "CommitButton");

        WaitUntil(() => !commit.IsVisible);
    }

    [AvaloniaTest]
    public void The_window_stays_open_while_unstaged_changes_are_left_or_the_option_is_off()
    {
        StageChange();
        File.WriteAllText(Path.Combine(_repo.Path, "other.txt"), "left");
        CommitWindow commit = OpenCommitWindow();
        Find<TextBox>(commit, "CommitMessageBox").Text = "not last";

        Click(commit, "CommitButton");

        WaitUntil(() => _repo.Run("log", "-1", "--format=%s").Trim() == "not last");
        Dispatcher.UIThread.RunJobs();
        commit.IsVisible.Should().BeTrue();
        commit.Close();
    }

    [AvaloniaTest]
    public void Commit_and_push_reads_push_when_there_is_nothing_to_commit()
    {
        CommitWindow commit = OpenCommitWindow();

        WaitUntil(() => Text(Find<Button>(commit, "CommitAndPushButton")) == "_Push");
        Find<Button>(commit, "ResetAllButton").IsEnabled.Should().BeFalse();
        Find<CheckBox>(commit, "AmendCheck").IsChecked = true;

        WaitUntil(() => Text(Find<Button>(commit, "CommitAndPushButton")) == "Commit & _push");
        commit.Close();
    }

    [AvaloniaTest]
    public void Reset_soft_while_amending_moves_HEAD_back_and_keeps_the_changes_staged()
    {
        CommitWindow commit = OpenCommitWindow();
        Find<StackPanel>(commit, "AmendPanel").IsVisible.Should().BeFalse();
        Find<CheckBox>(commit, "AmendCheck").IsChecked = true;
        Find<StackPanel>(commit, "AmendPanel").IsVisible.Should().BeTrue();
        WaitUntil(() => Find<Button>(commit, "ResetSoftButton").IsEnabled);

        Click(commit, "ResetSoftButton");
        WaitUntil(() => commit.OwnedWindows.OfType<ConfirmWindow>().Any());
        Click(commit.OwnedWindows.OfType<ConfirmWindow>().Single(), "ConfirmButton");

        WaitUntil(() => _repo.Run("log", "-1", "--format=%s").Trim() == "first");
        _repo.Run("status", "--porcelain").Should().Contain("M  a.txt");
        WaitUntil(() => Find<CheckBox>(commit, "AmendCheck").IsChecked == false);
        commit.Close();
    }

    [AvaloniaTest]
    public void Create_branch_creates_and_checks_out_the_branch()
    {
        CommitWindow commit = OpenCommitWindow();

        Click(commit, "CreateBranchButton");
        WaitUntil(() => commit.OwnedWindows.OfType<PromptWindow>().Any());
        PromptWindow prompt = commit.OwnedWindows.OfType<PromptWindow>().Single();
        Find<TextBox>(prompt, "ValueBox").Text = "topic";
        Click(prompt, "OkButton");

        WaitUntil(() => _repo.Run("branch", "--show-current").Trim() == "topic");
        commit.Close();
    }

    [AvaloniaTest]
    public void The_options_menu_keeps_upstreams_choices()
    {
        CommitWindow commit = OpenCommitWindow();
        MenuItem closeAfterLast = Find<MenuItem>(commit, "CloseAfterAllCommittedMenuItem");

        closeAfterLast.IsChecked = false;
        closeAfterLast.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        MenuItem refresh = Find<MenuItem>(commit, "RefreshOnFocusMenuItem");
        refresh.IsChecked = true;
        refresh.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

        TestAppBuilder.Preferences.CloseCommitDialogAfterLastCommit.Should().BeFalse();
        TestAppBuilder.Preferences.RefreshCommitDialogOnFocus.Should().BeTrue();
        commit.Close();
    }

    private void StageChange()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "changed");
        _repo.Run("add", "a.txt");
    }

    private static string? Text(Button button)
        => button.GetLogicalDescendants().OfType<AccessText>().FirstOrDefault()?.Text;

    private static void Reset()
    {
        TestAppBuilder.Preferences.CloseCommitDialogAfterCommit = false;
        TestAppBuilder.Preferences.CloseCommitDialogAfterLastCommit = true;
        TestAppBuilder.Preferences.RefreshCommitDialogOnFocus = false;

        // Reset soft asks the amend question; other tests turn it off.
        TestAppBuilder.Preferences.SetAsks(Confirmation.Amend, true);
    }

    private CommitWindow OpenCommitWindow()
    {
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        window.Show();
        window.OpenRepository(_repo.Path);
        WaitUntil(() => !window.IsBusy);
        WaitUntil(() => Find<Button>(window, "CommitDialogButton").IsEnabled);
        Click(window, "CommitDialogButton");
        WaitUntil(() => window.OwnedWindows.OfType<CommitWindow>().Any());
        return window.OwnedWindows.OfType<CommitWindow>().Single();
    }

    private static void Click(Window window, string name)
    {
        Control control = Find<Control>(window, name);
        control.RaiseEvent(new RoutedEventArgs(control is MenuItem ? MenuItem.ClickEvent : Button.ClickEvent));
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

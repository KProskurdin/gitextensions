using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using GitCommands;
using GitExtensions.Xplat.Core.Settings;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

internal sealed class CommitDialogSettingsTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(20);

    private TestRepository _repo = null!;

    [SetUp]
    public void Setup()
    {
        _repo = new TestRepository();
        ResetPreferences();
    }

    [TearDown]
    public void TearDown()
    {
        ResetPreferences();
        _repo.Dispose();
    }

    [AvaloniaTest]
    public void A_second_line_of_text_gets_an_empty_line_before_it_unless_turned_off()
    {
        MainWindow window = OpenWithChange("one");
        CommitWindow commit = OpenCommitWindow(window);
        Commit(commit, "subject" + Environment.NewLine + "body");
        WaitUntil(() => _repo.Run("log", "-1", "--format=%s").Trim() == "subject");
        _repo.Run("log", "-1", "--format=%B").Trim().ReplaceLineEndings("\n").Should().Be("subject\n\nbody");

        TestAppBuilder.Preferences.EnsureCommitMessageSecondLineEmpty = false;
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "two");
        _repo.Run("add", "a.txt");
        CommitWindow again = OpenCommitWindow(window);
        Commit(again, "next" + Environment.NewLine + "body");
        WaitUntil(() => _repo.Run("log", "-1", "--format=%s").Trim().StartsWith("next", StringComparison.Ordinal));

        // Without the empty line git takes both lines as the subject.
        _repo.Run("log", "-1", "--format=%s").Trim().Should().Be("next body");
    }

    [AvaloniaTest]
    public void Amend_is_remembered_across_closing_the_commit_window_unless_turned_off()
    {
        MainWindow window = OpenWithChange("one");
        CommitWindow commit = OpenCommitWindow(window);
        Find<CheckBox>(commit, "AmendCheck").IsChecked = true;
        WaitUntil(() =>
            (Find<TextBox>(commit, "CommitMessageBox").Text ?? "").StartsWith("second", StringComparison.Ordinal));
        Find<TextBox>(commit, "CommitMessageBox").Text = "second, reworded";
        commit.Close();

        CommitWindow reopened = OpenCommitWindow(window);
        WaitUntil(() => Find<CheckBox>(reopened, "AmendCheck").IsChecked == true);

        // As upstream: the draft is kept, not replaced by the last commit's message.
        Find<TextBox>(reopened, "CommitMessageBox").Text.Should().Be("second, reworded");

        // Closed while remembered, so the amend state is stored; turned off, it is not read back.
        reopened.Close();
        File.Exists(Path.Combine(_repo.Path, ".git", "GitExtensions.amend")).Should().BeTrue();
        TestAppBuilder.Preferences.RememberAmendCommitState = false;
        CommitWindow third = OpenCommitWindow(window);
        WaitUntil(() => Find<TextBox>(third, "CommitMessageBox").Text == "second, reworded");
        Dispatcher.UIThread.RunJobs();
        Find<CheckBox>(third, "AmendCheck").IsChecked.Should().BeFalse();
        third.Close();
    }

    [AvaloniaTest]
    public void The_commit_and_push_button_follows_its_setting_and_the_settings_tab_saves_all_three()
    {
        TestAppBuilder.Preferences.ShowCommitAndPush = false;
        MainWindow window = OpenWithChange("one");
        CommitWindow commit = OpenCommitWindow(window);
        Find<Button>(commit, "CommitAndPushButton").IsVisible.Should().BeFalse();
        commit.Close();

        Click(window, "SettingsMenuItem");
        WaitUntil(() => window.OwnedWindows.OfType<SettingsWindow>().Any());
        SettingsWindow settings = window.OwnedWindows.OfType<SettingsWindow>().Single();
        WaitUntil(() => settings.IsGitConfigLoaded);
        Find<CheckBox>(settings, "ShowCommitAndPushCheck").IsChecked.Should().BeFalse();
        Find<CheckBox>(settings, "ShowCommitAndPushCheck").IsChecked = true;
        Find<CheckBox>(settings, "SecondLineEmptyCheck").IsChecked = false;
        Find<CheckBox>(settings, "RememberAmendCheck").IsChecked = false;
        Click(settings, "SaveButton");
        WaitUntil(() => !window.OwnedWindows.OfType<SettingsWindow>().Any());

        TestAppBuilder.Preferences.ShowCommitAndPush.Should().BeTrue();
        TestAppBuilder.Preferences.EnsureCommitMessageSecondLineEmpty.Should().BeFalse();
        TestAppBuilder.Preferences.RememberAmendCommitState.Should().BeFalse();
    }

    private static void ResetPreferences()
    {
        TestAppBuilder.Preferences.EnsureCommitMessageSecondLineEmpty = true;
        TestAppBuilder.Preferences.RememberAmendCommitState = true;
        TestAppBuilder.Preferences.ShowCommitAndPush = true;
        TestAppBuilder.Preferences.CloseCommitDialogAfterCommit = true;
        TestAppBuilder.Preferences.ResetConfirmations();

        // Amend asks first; these tests are about what happens after the question.
        TestAppBuilder.Preferences.SetAsks(Confirmation.Amend, false);
    }

    private MainWindow OpenWithChange(string content)
    {
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), content);
        _repo.Run("add", "a.txt");
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        window.Show();
        Find<TextBox>(window, "PathBox").Text = _repo.Path;
        Click(window, "OpenButton");
        WaitUntil(() => Find<Button>(window, "OpenButton").IsEnabled);
        return window;
    }

    private static CommitWindow OpenCommitWindow(MainWindow window)
    {
        WaitUntil(() => Find<Button>(window, "CommitDialogButton").IsEnabled &&
                        !window.OwnedWindows.OfType<CommitWindow>().Any());
        Click(window, "CommitDialogButton");
        WaitUntil(() => window.OwnedWindows.OfType<CommitWindow>().Any());
        return window.OwnedWindows.OfType<CommitWindow>().Single();
    }

    private static void Commit(CommitWindow commit, string message)
    {
        TextBox box = Find<TextBox>(commit, "CommitMessageBox");
        WaitUntil(() => commit.IsVisible);
        box.Text = message;
        Click(commit, "CommitButton");
    }

    private static void Click(Window window, string name)
    {
        Control control = Find<Control>(window, name);
        RoutedEvent click = control is MenuItem ? MenuItem.ClickEvent : Button.ClickEvent;
        control.RaiseEvent(new RoutedEventArgs(click));
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

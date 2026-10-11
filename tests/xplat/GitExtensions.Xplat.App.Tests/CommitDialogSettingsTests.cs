using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
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
        Find<CheckBox>(settings, "ShowResetUnstagedCheck").IsChecked = false;
        Find<CheckBox>(settings, "ShowStagingErrorsCheck").IsChecked = false;
        Find<CheckBox>(settings, "AutocompleteCheck").IsChecked = false;
        Find<CheckBox>(settings, "ComposeInWindowCheck").IsChecked.Should().BeTrue();
        Find<NumericUpDown>(settings, "PreviousMessagesBox").Value.Should().Be(6);
        Find<NumericUpDown>(settings, "PreviousMessagesBox").Value = 9;
        Click(settings, "SaveButton");
        WaitUntil(() => !window.OwnedWindows.OfType<SettingsWindow>().Any());

        TestAppBuilder.Preferences.ShowCommitAndPush.Should().BeTrue();
        TestAppBuilder.Preferences.EnsureCommitMessageSecondLineEmpty.Should().BeFalse();
        TestAppBuilder.Preferences.RememberAmendCommitState.Should().BeFalse();
        TestAppBuilder.Preferences.CommitDialogNumberOfPreviousMessages.Should().Be(9);
        TestAppBuilder.Preferences.ShowResetWorkTreeChanges.Should().BeFalse();
        TestAppBuilder.Preferences.ShowResetAllChanges.Should().BeTrue();
        TestAppBuilder.Preferences.ShowErrorsWhenStagingFiles.Should().BeFalse();
        TestAppBuilder.Preferences.ProvideAutocompletion.Should().BeFalse();
    }

    [AvaloniaTest]
    public void The_commit_message_menu_lists_earlier_messages_and_the_last_one_typed()
    {
        _repo.Run("-c", "user.name=Other", "-c", "user.email=other@example.com", "commit", "-q", "--allow-empty", "-m",
            "by other");
        TestAppBuilder.Preferences.LastCommitMessage = "typed in another repository";
        TestAppBuilder.Preferences.CommitDialogNumberOfPreviousMessages = 2;
        MainWindow window = OpenWithChange("one");
        CommitWindow commit = OpenCommitWindow(window);
        Button button = Find<Button>(commit, "CommitMessageMenuButton");
        MenuFlyout flyout = (MenuFlyout)button.Flyout!;
        try
        {
            // As upstream: the last typed message first, then git's, cut to the setting's count, then the fixed items.
            flyout.ShowAt(button);
            WaitUntil(() => flyout.Items.OfType<MenuItem>().Count() == 4);
            List<MenuItem> items = [.. flyout.Items.OfType<MenuItem>()];
            items.Select(HeaderText).Should().Equal("typed in another repository", "by other",
                "Generate a list of changes in submodules", "Show only my messages");
            items[1].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Find<TextBox>(commit, "CommitMessageBox").Text.Should().Be("by other");

            items[3].IsChecked = true;
            items[3].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            TestAppBuilder.Preferences.CommitDialogShowOnlyMyMessages.Should().BeTrue();
            flyout.Hide();
            flyout.ShowAt(button);
            WaitUntil(() => flyout.Items.OfType<MenuItem>().Count() == 4);
            flyout.Items.OfType<MenuItem>().Select(HeaderText).Take(2).Should()
                .Equal("typed in another repository", "second");
        }
        finally
        {
            flyout.Hide();
        }

        // A commit keeps its message for the menu of any repository.
        Commit(commit, "committed here");
        WaitUntil(() => TestAppBuilder.Preferences.LastCommitMessage == "committed here");
    }

    [AvaloniaTest]
    public void The_reset_buttons_ask_first_and_follow_their_settings()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "staged");
        _repo.Run("add", "a.txt");
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "unstaged");
        File.WriteAllText(Path.Combine(_repo.Path, "new.txt"), "new");
        MainWindow window = Open();
        CommitWindow commit = OpenCommitWindow(window);
        WaitUntil(() => Find<ListBox>(commit, "UnstagedList").ItemCount == 2);

        // As upstream's FormResetChanges: with changed and new files, deleting the new ones is the user's choice.
        Click(commit, "ResetUnstagedButton");
        WaitUntil(() => commit.OwnedWindows.OfType<ResetChangesWindow>().Any());
        ResetChangesWindow ask = commit.OwnedWindows.OfType<ResetChangesWindow>().Single();
        Find<CheckBox>(ask, "DeleteNewFilesCheck").IsEnabled.Should().BeTrue();
        Find<CheckBox>(ask, "DeleteNewFilesCheck").IsChecked = true;
        Click(ask, "ResetButton");
        WaitUntil(() => !File.Exists(Path.Combine(_repo.Path, "new.txt")));
        File.ReadAllText(Path.Combine(_repo.Path, "a.txt")).Should().Be("staged");
        commit.Close();

        TestAppBuilder.Preferences.ShowResetAllChanges = false;
        CommitWindow again = OpenCommitWindow(window);
        Find<Button>(again, "ResetAllButton").IsVisible.Should().BeFalse();
        Find<Button>(again, "ResetUnstagedButton").IsVisible.Should().BeTrue();
        again.Close();
    }

    [AvaloniaTest]
    public void Typing_offers_the_words_of_the_changed_files_and_ctrl_space_takes_the_only_match()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "RenderWidget.cs"),
            "public class RenderWidget\n{\n    public void DrawFrame() { }\n}\n");
        _repo.Run("add", "RenderWidget.cs");
        MainWindow window = Open();
        CommitWindow commit = OpenCommitWindow(window);
        TextBox box = Find<TextBox>(commit, "CommitMessageBox");
        WaitUntil(() => commit.IsVisible);
        box.Focus();

        // As upstream: after a short pause the list offers the file's names and the identifiers its regex finds.
        commit.KeyTextInput("Ren");
        WaitUntil(() => commit.AutoCompleteOffers.Count > 0);
        commit.AutoCompleteOffers.Should().Contain(["RenderWidget.cs", "RenderWidget"]);
        string first = commit.AutoCompleteOffers[0];
        commit.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, keySymbol: null);
        box.Text.Should().Be(first);
        commit.AutoCompleteOffers.Should().BeEmpty();

        // Ctrl+Space with the capitals of a word: the only match is taken at once.
        commit.KeyTextInput(" DF");
        commit.KeyPress(Key.Space, RawInputModifiers.Control, PhysicalKey.Space, keySymbol: null);
        WaitUntil(() => box.Text == first + " DrawFrame");

        // Turned off, typing offers nothing.
        TestAppBuilder.Preferences.ProvideAutocompletion = false;
        commit.KeyTextInput(" Ren");
        Thread.Sleep(300);
        Dispatcher.UIThread.RunJobs();
        commit.AutoCompleteOffers.Should().BeEmpty();
        commit.Close();
    }

    [AvaloniaTest]
    public void Without_composing_in_the_window_git_asks_its_editor_for_the_message()
    {
        string folder = Path.Combine(Path.GetTempPath(), "xplat-editor-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string script = Path.Combine(folder, "editor.sh");
        File.WriteAllText(script, "#!/bin/sh\nprintf 'typed in the editor\\n' > \"$1\"\n");
        string? editorBefore = AppServices.EditorCommand;
        AppServices.EditorCommand = $"sh '{script.Replace('\\', '/')}'";
        TestAppBuilder.Preferences.UseFormCommitMessage = false;
        try
        {
            MainWindow window = OpenWithChange("one");
            CommitWindow commit = OpenCommitWindow(window);
            TextBox box = Find<TextBox>(commit, "CommitMessageBox");
            box.IsEnabled.Should().BeFalse();
            box.PlaceholderText.Should().Be("Commit Message is requested during commit");
            Find<Button>(commit, "CommitTemplatesButton").IsEnabled.Should().BeFalse();

            Click(commit, "CommitButton");
            WaitUntil(() => _repo.Run("log", "-1", "--format=%s").Trim() == "typed in the editor");
        }
        finally
        {
            AppServices.EditorCommand = editorBefore;
            GitProcess.DeleteFolder(folder);
        }
    }

    private static void ResetPreferences()
    {
        TestAppBuilder.Preferences.EnsureCommitMessageSecondLineEmpty = true;
        TestAppBuilder.Preferences.RememberAmendCommitState = true;
        TestAppBuilder.Preferences.ShowCommitAndPush = true;
        TestAppBuilder.Preferences.CloseCommitDialogAfterCommit = true;
        TestAppBuilder.Preferences.LastCommitMessage = "";
        TestAppBuilder.Preferences.CommitDialogNumberOfPreviousMessages = 6;
        TestAppBuilder.Preferences.CommitDialogShowOnlyMyMessages = false;
        TestAppBuilder.Preferences.ShowResetAllChanges = true;
        TestAppBuilder.Preferences.ShowResetWorkTreeChanges = true;
        TestAppBuilder.Preferences.ShowErrorsWhenStagingFiles = true;
        TestAppBuilder.Preferences.ProvideAutocompletion = true;
        TestAppBuilder.Preferences.UseFormCommitMessage = true;
        TestAppBuilder.Preferences.ResetConfirmations();

        // Amend asks first; these tests are about what happens after the question.
        TestAppBuilder.Preferences.SetAsks(Confirmation.Amend, false);
    }

    private MainWindow OpenWithChange(string content)
    {
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), content);
        _repo.Run("add", "a.txt");
        return Open();
    }

    private MainWindow Open()
    {
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        window.Show();
        window.OpenRepository(_repo.Path);
        WaitUntil(() => !window.IsBusy);
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

    private static string HeaderText(MenuItem item)
        => item.Header as string ?? (item.Header as TextBlock)?.Text ?? "";

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

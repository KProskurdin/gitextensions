using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using GitCommands;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

internal sealed class ConfirmationsTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(20);

    private TestRepository _repo = null!;

    [SetUp]
    public void Setup()
    {
        _repo = new TestRepository();
        TestAppBuilder.Preferences.ResetConfirmations();
    }

    [TearDown]
    public void TearDown()
    {
        TestAppBuilder.Preferences.ResetConfirmations();
        _repo.Dispose();
    }

    [AvaloniaTest]
    public void The_confirmations_tab_shows_upstreams_questions_and_saves_them()
    {
        MainWindow window = NewWindow();
        window.Show();
        Click(window, "SettingsMenuItem");
        WaitUntil(() => window.OwnedWindows.OfType<SettingsWindow>().Any());
        SettingsWindow settings = window.OwnedWindows.OfType<SettingsWindow>().Single();
        WaitUntil(() => settings.IsGitConfigLoaded);

        // Upstream's defaults: every question is asked except the left panel checkout.
        settings.ConfirmationChecks.Where(pair => pair.Value.IsChecked == true).Select(pair => pair.Key).Should()
            .BeEquivalentTo(Enum.GetValues<Confirmation>().Where(c => c != Confirmation.BranchCheckout));
        settings.ConfirmationChecks[Confirmation.StashDrop].Content.Should().Be("Drop stash");
        settings.ConfirmationChecks[Confirmation.StashDrop].IsChecked = false;
        settings.ConfirmationChecks[Confirmation.BranchCheckout].IsChecked = true;
        Click(settings, "SaveButton");
        WaitUntil(() => !window.OwnedWindows.OfType<SettingsWindow>().Any());

        TestAppBuilder.Preferences.Asks(Confirmation.StashDrop).Should().BeFalse();
        TestAppBuilder.Preferences.Asks(Confirmation.BranchCheckout).Should().BeTrue();
        TestAppBuilder.Preferences.Asks(Confirmation.Amend).Should().BeTrue();
    }

    [AvaloniaTest]
    public void Dont_show_again_turns_the_stash_drop_question_off()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "first change");
        _repo.Run("stash", "push", "-q", "-m", "one");
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "second change");
        _repo.Run("stash", "push", "-q", "-m", "two");
        MainWindow window = NewWindow();
        window.Show();
        Open(window, _repo.Path);
        ListBox stashes = Find<ListBox>(window, "StashList");
        WaitUntil(() => stashes.ItemCount == 2);

        stashes.SelectedIndex = 0;
        Click(window, "DropStashButton");
        WaitUntil(() => window.OwnedWindows.OfType<ConfirmWindow>().Any());
        ConfirmWindow question = window.OwnedWindows.OfType<ConfirmWindow>().Single();
        question.Title.Should().Be("Drop Stash Confirmation");
        CheckBox dontShowAgain = Find<CheckBox>(question, "DontShowAgainCheck");
        dontShowAgain.IsVisible.Should().BeTrue();
        dontShowAgain.IsChecked = true;
        Click(question, "ConfirmButton");
        WaitUntil(() => stashes.ItemCount == 1);

        // Turned off: the next drop happens without a question.
        TestAppBuilder.Preferences.Asks(Confirmation.StashDrop).Should().BeFalse();
        stashes.SelectedIndex = 0;
        Click(window, "DropStashButton");
        WaitUntil(() => stashes.ItemCount == 0);
        window.OwnedWindows.OfType<ConfirmWindow>().Should().BeEmpty();
    }

    [AvaloniaTest]
    public void Left_panel_checkout_asks_only_when_turned_on_and_cancel_keeps_the_branch()
    {
        string current = _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim();
        _repo.Run("branch", "other");
        TestAppBuilder.Preferences.SetAsks(Confirmation.BranchCheckout, true);
        MainWindow window = NewWindow();
        window.Show();
        Open(window, _repo.Path);
        TreeView branches = Find<TreeView>(window, "BranchTreeView");
        WaitUntil(() => BranchNode(branches, "other") is not null);

        branches.SelectedItem = BranchNode(branches, "other");
        Click(window, "CheckoutButton");
        WaitUntil(() => window.OwnedWindows.OfType<ConfirmWindow>().Any());
        ConfirmWindow question = window.OwnedWindows.OfType<ConfirmWindow>().Single();

        // As upstream's ConfirmBranchCheckout: a plain question, without "don't show again".
        Find<CheckBox>(question, "DontShowAgainCheck").IsVisible.Should().BeFalse();
        Click(question, "CancelButton");
        Dispatcher.UIThread.RunJobs();

        _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim().Should().Be(current);
    }

    [AvaloniaTest]
    public void Amend_asks_first_and_cancel_leaves_the_last_commit_alone()
    {
        string head = _repo.Run("rev-parse", "HEAD").Trim();
        MainWindow window = NewWindow();
        window.Show();
        Open(window, _repo.Path);
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "changed");
        _repo.Run("add", "a.txt");
        Button commitButton = Find<Button>(window, "CommitDialogButton");
        WaitUntil(() => commitButton.IsEnabled);
        Click(window, "CommitDialogButton");
        WaitUntil(() => window.OwnedWindows.OfType<CommitWindow>().Any());
        CommitWindow commit = window.OwnedWindows.OfType<CommitWindow>().Single();

        Find<CheckBox>(commit, "AmendCheck").IsChecked = true;
        WaitUntil(() => (Find<TextBox>(commit, "CommitMessageBox").Text ?? "").Length > 0);
        Click(commit, "CommitButton");
        WaitUntil(() => commit.OwnedWindows.OfType<ConfirmWindow>().Any());
        ConfirmWindow question = commit.OwnedWindows.OfType<ConfirmWindow>().Single();
        question.Title.Should().Be("Amend commit");
        Click(question, "CancelButton");
        Dispatcher.UIThread.RunJobs();

        _repo.Run("rev-parse", "HEAD").Trim().Should().Be(head);
        commit.Close();
    }

    private static BranchTreeNode? BranchNode(TreeView tree, string name)
        => (tree.ItemsSource as IReadOnlyList<BranchTreeNode>)?
            .SelectMany(root => root.Descendants())
            .FirstOrDefault(node => node.Branch?.Name == name);

    private static MainWindow NewWindow() =>
        new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));

    private static void Open(MainWindow window, string path)
    {
        Find<TextBox>(window, "PathBox").Text = path;
        Find<Button>(window, "OpenButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        WaitUntil(() => Find<Button>(window, "OpenButton").IsEnabled);
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

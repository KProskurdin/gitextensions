using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using GitCommands;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

internal sealed class AdvancedSettingsTests
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
    public void A_checkout_with_local_changes_uses_the_last_choice_without_asking_unless_always_asked()
    {
        _repo.Run("branch", "other");
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "local edit");
        TestAppBuilder.Preferences.CheckoutBranchAction = LocalChangesAction.Stash;
        TestAppBuilder.Preferences.UseDefaultCheckoutBranchAction = true;
        TestAppBuilder.Preferences.AlwaysShowCheckoutBranchDlg = true;
        MainWindow window = OpenWindow();
        TreeView branches = Find<TreeView>(window, "BranchTreeView");
        WaitUntil(() => BranchNode(branches, "other") is not null &&
                        Find<Button>(window, "CommitDialogButton").Content as string == "Commit (1)");
        branches.SelectedItem = BranchNode(branches, "other");

        Click(window, "CheckoutButton");
        WaitUntil(() => window.OwnedWindows.OfType<CheckoutWindow>().Any());
        window.OwnedWindows.OfType<CheckoutWindow>().Single().Close();

        TestAppBuilder.Preferences.AlwaysShowCheckoutBranchDlg = false;
        Click(window, "CheckoutButton");

        WaitUntil(() => Find<TextBlock>(window, "OperationStatusText").Text == "Checked out other");
        window.OwnedWindows.OfType<CheckoutWindow>().Should().BeEmpty();
        File.ReadAllText(Path.Combine(_repo.Path, "a.txt")).Should().Be("local edit");
    }

    [AvaloniaTest]
    public void A_new_branch_name_is_normalised_with_the_chosen_symbol()
    {
        TestAppBuilder.Preferences.AutoNormaliseSymbol = "-";
        MainWindow window = OpenWindow();

        Find<TextBox>(window, "NewBranchBox").Text = "my new feature";
        Click(window, "CreateBranchButton");

        WaitUntil(() => _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim() == "my-new-feature");
    }

    [AvaloniaTest]
    public void Commit_and_push_after_an_amend_pushes_with_lease_when_the_setting_asks_for_it()
    {
        string remote = Path.Combine(Path.GetTempPath(), "xplat-remote-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(remote);
        try
        {
            GitProcess.Run(remote, "init", "--bare", "-q");
            _repo.Run("remote", "add", "origin", remote);
            string branch = _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim();
            _repo.Run("push", "-q", "-u", "origin", branch);
            TestAppBuilder.Preferences.CommitAndPushForcedWhenAmend = true;
            File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "amended");
            _repo.Run("add", "a.txt");
            MainWindow window = OpenWindow();
            WaitUntil(() => Find<Button>(window, "CommitDialogButton").IsEnabled);
            Click(window, "CommitDialogButton");
            WaitUntil(() => window.OwnedWindows.OfType<CommitWindow>().Any());
            CommitWindow commit = window.OwnedWindows.OfType<CommitWindow>().Single();

            Find<CheckBox>(commit, "AmendCheck").IsChecked = true;
            WaitUntil(() => (Find<TextBox>(commit, "CommitMessageBox").Text ?? "").Length > 0);
            string before = _repo.Run("rev-parse", "HEAD").Trim();
            Click(commit, "CommitAndPushButton");

            // A plain push of the rewritten commit would be refused; with lease it replaces the remote's. The remote starts at
            // the old HEAD, so the wait is for a new HEAD that the remote has too.
            WaitUntil(() => _repo.Run("rev-parse", "HEAD").Trim() is var head && head != before &&
                            GitProcess.Run(remote, "rev-parse", branch).Trim() == head);
            _repo.Run("show", "HEAD:a.txt").Trim().Should().Be("amended");
        }
        finally
        {
            GitProcess.DeleteFolder(remote);
        }
    }

    [AvaloniaTest]
    public void The_advanced_tab_saves_upstreams_choices()
    {
        MainWindow window = NewWindow();
        window.Show();
        Click(window, "SettingsMenuItem");
        WaitUntil(() => window.OwnedWindows.OfType<SettingsWindow>().Any());
        SettingsWindow settings = window.OwnedWindows.OfType<SettingsWindow>().Single();
        WaitUntil(() => settings.IsGitConfigLoaded);

        Find<CheckBox>(settings, "AutoNormaliseCheck").IsChecked.Should().BeTrue();
        ComboBox symbols = Find<ComboBox>(settings, "NormaliseSymbolBox");
        symbols.Items.Cast<NormaliseSymbolOption>().Select(option => option.Label).Should().Equal("_", "-", "(none)");
        symbols.SelectedItem = symbols.Items.Cast<NormaliseSymbolOption>().Single(option => option.Label == "(none)");
        Find<CheckBox>(settings, "AlwaysShowCheckoutDlgCheck").IsChecked = true;
        Find<CheckBox>(settings, "UseLocalChangesActionCheck").IsChecked = true;
        Find<CheckBox>(settings, "CommitAndPushForcedCheck").IsChecked = true;
        Click(settings, "SaveButton");
        WaitUntil(() => !window.OwnedWindows.OfType<SettingsWindow>().Any());

        IAppPreferences preferences = TestAppBuilder.Preferences;
        (preferences.AutoNormaliseSymbol, preferences.AlwaysShowCheckoutBranchDlg,
                preferences.UseDefaultCheckoutBranchAction, preferences.CommitAndPushForcedWhenAmend)
            .Should().Be(("", true, true, true));
    }

    private static void Reset()
    {
        TestAppBuilder.Preferences.AlwaysShowCheckoutBranchDlg = false;
        TestAppBuilder.Preferences.UseDefaultCheckoutBranchAction = false;
        TestAppBuilder.Preferences.AutoNormaliseBranchName = true;
        TestAppBuilder.Preferences.AutoNormaliseSymbol = "_";
        TestAppBuilder.Preferences.CommitAndPushForcedWhenAmend = false;
        TestAppBuilder.Preferences.CheckoutBranchAction = LocalChangesAction.DontChange;
        TestAppBuilder.Preferences.CloseCommitDialogAfterCommit = true;
        TestAppBuilder.Preferences.ResetConfirmations();
        TestAppBuilder.Preferences.SetAsks(Confirmation.Amend, false);
    }

    private static BranchTreeNode? BranchNode(TreeView tree, string name)
        => (tree.ItemsSource as IReadOnlyList<BranchTreeNode>)?
            .SelectMany(root => root.Descendants())
            .FirstOrDefault(node => node.Branch?.Name == name);

    private static MainWindow NewWindow() =>
        new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));

    private MainWindow OpenWindow()
    {
        MainWindow window = NewWindow();
        window.Show();
        Find<TextBox>(window, "PathBox").Text = _repo.Path;
        Click(window, "OpenButton");
        WaitUntil(() => Find<Button>(window, "OpenButton").IsEnabled);
        return window;
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

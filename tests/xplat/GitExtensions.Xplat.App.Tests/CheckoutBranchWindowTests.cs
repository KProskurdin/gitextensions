using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using GitCommands;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

internal sealed class CheckoutBranchWindowTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(20);

    private TestRepository _repo = null!;

    [SetUp]
    public void Setup()
    {
        _repo = new TestRepository();
        _repo.Run("remote", "add", "origin", Path.GetTempPath());
        _repo.Run("update-ref", "refs/remotes/origin/feature", "HEAD~1");
        Reset();
    }

    [TearDown]
    public void TearDown()
    {
        Reset();
        _repo.Dispose();
    }

    [AvaloniaTest]
    public void A_remote_branch_is_checked_out_as_a_local_branch_of_the_same_name()
    {
        MainWindow window = OpenWindow();
        CheckoutBranchWindow checkout = OpenCheckoutOf(window, "origin/feature");

        Find<RadioButton>(checkout, "RemoteBranchRadio").IsChecked.Should().BeTrue();
        Find<Control>(checkout, "RemoteOptions").IsVisible.Should().BeTrue();
        Find<AccessText>(checkout, "ResetBranchText").Text.Should().Be("Cr_eate local branch with same name:");
        Find<TextBlock>(checkout, "LocalBranchNameText").Text.Should().Be("'feature'");
        Find<TextBox>(checkout, "CustomBranchNameBox").Text.Should().Be("origin_feature");
        Find<Control>(checkout, "LocalChangesGroup").IsVisible.Should().BeFalse();
        WaitUntil(() => !string.IsNullOrEmpty(Find<TextBlock>(checkout, "ChangesText").Text));
        Find<TextBlock>(checkout, "ChangesText").Text.Should().Be("(+1-0)", "upstream's GetCommitCountString from HEAD to the branch");

        Click(checkout, "CheckoutButton");

        WaitUntil(() => _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim() == "feature");
        _repo.Run("rev-parse", "--abbrev-ref", "feature@{upstream}").Trim().Should().Be("origin/feature");
        WaitUntil(() => !window.OwnedWindows.OfType<CheckoutBranchWindow>().Any());
    }

    [AvaloniaTest]
    public void A_remote_branch_can_be_checked_out_with_a_custom_local_name()
    {
        MainWindow window = OpenWindow();
        CheckoutBranchWindow checkout = OpenCheckoutOf(window, "origin/feature");

        Find<RadioButton>(checkout, "CreateBranchRadio").IsChecked = true;
        Find<TextBox>(checkout, "CustomBranchNameBox").IsEnabled.Should().BeTrue();
        Find<TextBox>(checkout, "CustomBranchNameBox").Text = "mine";
        Click(checkout, "CheckoutButton");

        WaitUntil(() => _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim() == "mine");
        _repo.Run("rev-parse", "--abbrev-ref", "mine@{upstream}").Trim().Should().Be("origin/feature");
    }

    [AvaloniaTest]
    public void A_stash_checkout_asks_whether_to_apply_the_stash_again_and_can_remember_it()
    {
        _repo.Run("branch", "other");
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "local edit");
        MainWindow window = OpenWindow();
        CheckoutBranchWindow checkout = OpenCheckoutOf(window, "other");
        Find<Control>(checkout, "LocalChangesGroup").IsVisible.Should().BeTrue();

        Find<RadioButton>(checkout, "StashRadio").IsChecked = true;
        Click(checkout, "CheckoutButton");

        WaitUntil(() => checkout.OwnedWindows.OfType<MessageBoxWindow>().Any());
        MessageBoxWindow question = checkout.OwnedWindows.OfType<MessageBoxWindow>().Single();
        question.Title.Should().Be(CheckoutBranchDialog.ApplyStashedItemsAgainCaption);
        Find<CheckBox>(question, "VerificationCheck").IsChecked = true;
        question.GetLogicalDescendants().OfType<Button>()
            .Single(button => button.Content is AccessText { Text: "_Yes" })
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        WaitUntil(() => _repo.ReadFile("a.txt") == "local edit");
        WaitUntil(() => !window.IsBusy);
        _repo.Run("stash", "list").Trim().Should().BeEmpty();
        _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim().Should().Be("other");
        TestAppBuilder.Preferences.AutoPopStashAfterCheckoutBranch.Should().BeTrue();
        TestAppBuilder.Preferences.CheckoutBranchAction.Should().Be(LocalChangesAction.DontChange,
            "without \"Set as default\" the default stays");
    }

    [AvaloniaTest]
    public void Checkout_branch_from_the_menu_asks_for_an_existing_branch()
    {
        MainWindow window = OpenWindow();

        Click(window, "CheckoutBranchMenuItem");

        WaitUntil(() => window.OwnedWindows.OfType<CheckoutBranchWindow>().Any());
        CheckoutBranchWindow checkout = window.OwnedWindows.OfType<CheckoutBranchWindow>().Single();
        Find<ComboBox>(checkout, "BranchesBox").Text = "nope";
        Click(checkout, "CheckoutButton");
        WaitUntil(() => checkout.OwnedWindows.OfType<MessageBoxWindow>().Any());
        checkout.OwnedWindows.OfType<MessageBoxWindow>().Single().Close();
        checkout.Close();
    }

    private static void Reset()
    {
        TestAppBuilder.Preferences.AutoPopStashAfterCheckoutBranch = null;
        TestAppBuilder.Preferences.CheckoutBranchAction = LocalChangesAction.DontChange;
        TestAppBuilder.Preferences.AlwaysShowCheckoutBranchDlg = false;
        TestAppBuilder.Preferences.UseDefaultCheckoutBranchAction = false;
        TestAppBuilder.Preferences.CreateLocalBranchForRemote = false;
        TestAppBuilder.Preferences.ResetConfirmations();
    }

    private MainWindow OpenWindow()
    {
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        window.Show();
        window.OpenRepository(_repo.Path);
        WaitUntil(() => !window.IsBusy && Roots(window).Count > 0);
        return window;
    }

    private static CheckoutBranchWindow OpenCheckoutOf(MainWindow window, string branch)
    {
        WaitUntil(() => Node(window, branch) is not null);
        Find<TreeView>(window, "BranchTreeView").SelectedItem = Node(window, branch);
        Click(window, "TreeCheckoutMenuItem");
        WaitUntil(() => window.OwnedWindows.OfType<CheckoutBranchWindow>().Any());
        return window.OwnedWindows.OfType<CheckoutBranchWindow>().Single();
    }

    private static IReadOnlyList<RepoObjectNode> Roots(Window window)
        => Find<TreeView>(window, "BranchTreeView").ItemsSource as IReadOnlyList<RepoObjectNode> ?? [];

    private static RepoObjectNode? Node(Window window, string branch)
        => Roots(window).SelectMany(root => root.Descendants()).FirstOrDefault(node => node.Branch?.Name == branch);

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

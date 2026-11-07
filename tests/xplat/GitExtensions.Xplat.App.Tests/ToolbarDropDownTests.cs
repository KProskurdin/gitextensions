using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Threading;
using GitCommands;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

/// <summary>
///  The browse toolbar's drop-downs whose menus are built when they open: submodules, worktrees, working directory and the
///  current branch.
/// </summary>
internal sealed class ToolbarDropDownTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private TestRepository _repo = null!;

    [SetUp]
    public void SetUp() => _repo = new TestRepository();

    [TearDown]
    public void TearDown() => _repo.Dispose();

    [AvaloniaTest]
    [TestCase("WorktreesButton")]
    [TestCase("WorkingDirButton")]
    [TestCase("BranchSelectButton")]
    public void A_click_opens_the_drop_down_with_its_items(string name)
    {
        MainWindow window = OpenWindow();
        Button button = window.FindControl<Button>(name)!;
        WaitUntil(() => button.IsEffectivelyEnabled);

        Click(window, button);

        MenuFlyout flyout = (MenuFlyout)button.Flyout!;
        WaitUntil(() => flyout.IsOpen && flyout.Items.Count > 0);
        PopupWidth(flyout).Should().BeGreaterThan(50, "the menu is laid out with its items, not as a thin line");
        flyout.Hide();
        window.Close();
    }

    [AvaloniaTest]
    public void The_submodules_arrow_opens_the_drop_down_with_its_items()
    {
        MainWindow window = OpenWindow();
        SplitButton button = window.FindControl<SplitButton>("SubmodulesButton")!;
        WaitUntil(() => button.IsEffectivelyEnabled);

        // The split button's secondary part (its arrow) is the right end of the control.
        Point arrow = button.TranslatePoint(new Point(button.Bounds.Width - 4, button.Bounds.Height / 2), window)!.Value;
        window.MouseDown(arrow, MouseButton.Left);
        window.MouseUp(arrow, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        MenuFlyout flyout = (MenuFlyout)button.Flyout!;
        WaitUntil(() => flyout.IsOpen && flyout.Items.Count > 0);
        PopupWidth(flyout).Should().BeGreaterThan(50, "the menu is laid out with its items, not as a thin line");
        flyout.Hide();
        window.Close();
    }

    [AvaloniaTest]
    [TestCase("CommitMessageMenuButton")]
    [TestCase("CommitTemplatesButton")]
    public void The_commit_windows_menus_open_with_their_items(string name)
    {
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "changed");
        MainWindow window = OpenWindow();
        WaitUntil(() => window.FindControl<Button>("CommitDialogButton")!.IsEnabled);
        window.FindControl<Button>("CommitDialogButton")!.RaiseEvent(
            new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        WaitUntil(() => window.OwnedWindows.OfType<CommitWindow>().Any());
        CommitWindow commit = window.OwnedWindows.OfType<CommitWindow>().Single();
        Button button = commit.FindControl<Button>(name)!;
        WaitUntil(() => button.IsEffectivelyEnabled);
        MenuFlyout flyout = (MenuFlyout)button.Flyout!;

        // The button's click opens its flyout the same way (FlyoutBase.ShowAt); the commit window's toolstrip may lay the
        // button out of a small test window.
        flyout.ShowAt(button);

        WaitUntil(() => flyout.IsOpen && flyout.Items.Count > 0);
        PopupWidth(flyout).Should().BeGreaterThan(50, "the menu is laid out with its items, not as a thin line");
        flyout.Hide();
        commit.Close();
        window.Close();
    }

    // The width of the open flyout's popup content (FlyoutBase.Popup is protected).
    private static double PopupWidth(FlyoutBase flyout)
    {
        Dispatcher.UIThread.RunJobs();
        const System.Reflection.BindingFlags Members = System.Reflection.BindingFlags.Instance
                                                        | System.Reflection.BindingFlags.NonPublic
                                                        | System.Reflection.BindingFlags.Public;
        Popup popup = (Popup)(typeof(PopupFlyoutBase).GetProperties(Members)
                                  .FirstOrDefault(property => property.PropertyType == typeof(Popup))?.GetValue(flyout)
                              ?? typeof(PopupFlyoutBase).GetFields(Members)
                                  .First(field => field.FieldType == typeof(Popup)).GetValue(flyout))!;
        return popup.Child?.Bounds.Width ?? 0;
    }

    private MainWindow OpenWindow()
    {
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        window.Show();
        window.OpenRepository(_repo.Path);
        WaitUntil(() => !window.IsBusy);
        return window;
    }

    private static void Click(Window window, Control control)
    {
        Point center = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

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
            Thread.Sleep(10);
        }
    }
}

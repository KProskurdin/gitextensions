using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using AwesomeAssertions;
using GitCommands;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

/// <summary>
///  A repository opened through a symbolic link, whose path in the app differs from the one git prints.
/// </summary>
[Platform(Include = "Linux,MacOsX")]
internal sealed class SymlinkedRepositoryTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private TestRepository _repo = null!;
    private string _link = "";

    [SetUp]
    public void Setup()
    {
        _repo = new TestRepository();
        _link = _repo.Path + "-link";
        Directory.CreateSymbolicLink(_link, _repo.Path);
    }

    [TearDown]
    public void TearDown()
    {
        File.Delete(_link);
        _repo.Dispose();
    }

    [AvaloniaTest]
    public void The_worktrees_menu_marks_the_open_worktree()
    {
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        window.Show();
        window.OpenRepository(_link);
        WaitUntil(() => !window.IsBusy);

        Button button = window.FindControl<Button>("WorktreesButton")!;
        MenuFlyout flyout = (MenuFlyout)button.Flyout!;
        flyout.ShowAt(button);
        WaitUntil(() => flyout.Items.Count > 1);

        MenuItem openWorktree = flyout.Items.OfType<MenuItem>().First();
        openWorktree.IsChecked.Should().BeTrue();
        openWorktree.IsEnabled.Should().BeFalse("the open worktree cannot be switched to");
        flyout.Hide();
        window.Close();
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

using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using AwesomeAssertions;
using GitCommands;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

internal sealed class OverflowToolStripTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(20);

    private TestRepository _repo = null!;

    [SetUp]
    public void Setup() => _repo = new TestRepository();

    [TearDown]
    public void TearDown() => _repo.Dispose();

    [AvaloniaTest]
    public void The_buttons_that_do_not_fit_are_in_upstreams_overflow_drop_down_and_work_there()
    {
        // The headless font is wide: the working directory's path leaves no room for the last buttons in 900 pixels.
        MainWindow window = OpenWindow(width: 900);
        OverflowToolStrip strip = window.FindControl<OverflowToolStrip>("MainToolStrip")!;
        Button settings = window.FindControl<Button>("EditSettingsButton")!;
        int place = strip.Children.IndexOf(settings);
        WaitUntil(() => strip.HasOverflow);
        strip.OverflowItems.Should().EndWith(settings);

        strip.ToggleOverflow();
        Dispatcher.UIThread.RunJobs();

        strip.IsOverflowOpen.Should().BeTrue();
        strip.Children.Should().NotContain(settings, "the item moved into the drop-down");
        settings.IsEffectivelyVisible.Should().BeTrue();

        UiClicks.Raise(settings, Button.ClickEvent);
        WaitUntil(() => window.OwnedWindows.OfType<SettingsWindow>().Any());

        strip.IsOverflowOpen.Should().BeFalse("an item's click closes the drop-down");
        strip.Children.IndexOf(settings).Should().Be(place, "the item is back in its place");
        window.OwnedWindows.OfType<SettingsWindow>().Single().Close();
        window.Close();
    }

    [AvaloniaTest]
    public void A_window_wide_enough_has_no_overflow()
    {
        MainWindow window = OpenWindow(width: 2400);
        OverflowToolStrip strip = window.FindControl<OverflowToolStrip>("MainToolStrip")!;
        Dispatcher.UIThread.RunJobs();

        strip.HasOverflow.Should().BeFalse();
        strip.OverflowItems.Should().BeEmpty();
        window.Close();
    }

    private MainWindow OpenWindow(double width)
    {
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null)) { Width = width };
        window.Show();
        window.OpenRepository(_repo.Path);
        WaitUntil(() => !window.IsBusy);
        return window;
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

using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using GitCommands;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

internal sealed class HelpMenuTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    [AvaloniaTest]
    public void Changelog_shows_upstreams_change_log()
    {
        MainWindow main = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        main.Show();

        main.FindControl<MenuItem>("ChangelogMenuItem")!.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        WaitUntil(() => main.OwnedWindows.OfType<ChangeLogWindow>().Any());
        ChangeLogWindow changeLog = main.OwnedWindows.OfType<ChangeLogWindow>().Single();
        WaitUntil(() => changeLog.ChangeLogLoaded.IsCompleted);

        changeLog.FindControl<TextBox>("ChangeLog")!.Text.Should().StartWith("Changelog")
            .And.Contain("### Version", "it is upstream's ChangeLog.md");
        changeLog.Close();
        main.Close();
    }

    [AvaloniaTest]
    public void Donate_shows_upstreams_text_and_its_badge_opens_the_donation_page()
    {
        Window owner = new();
        owner.Show();
        List<string> opened = [];
        DonateWindow donate = new(opened.Add);
        _ = donate.ShowDialog(owner);

        donate.FindControl<TextBlock>("lblText")!.Text.Should().Be(DonateWindow.DonateText);
        donate.FindControl<Button>("pbxDonate")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        opened.Should().Equal("https://opencollective.com/gitextensions");
        owner.Close();
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
        }
    }
}

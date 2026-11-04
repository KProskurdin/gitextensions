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

    [AvaloniaTest]
    public void About_shows_the_environment_and_thanks_a_contributor_who_open_the_contributors()
    {
        Window owner = new();
        owner.Show();
        AboutWindow about = new("2.53.0");
        _ = about.ShowDialog(owner);
        WaitUntil(() => !string.IsNullOrEmpty(about.FindControl<SelectableTextBlock>("environmentIssueInfo")!.Text));

        string[] lines = about.FindControl<SelectableTextBlock>("environmentIssueInfo")!.Text!.Split(Environment.NewLine);
        lines[0].Should().StartWith("Git Extensions ");
        lines[1].Should().StartWith("Build ");
        lines[2].Should().Be("Git 2.53.0");
        lines.Should().Contain(line => line.StartsWith("Portable: ", StringComparison.Ordinal));
        about.ThanksText.Should().StartWith("Thanks to over ");
        about.FindControl<HyperlinkButton>("_NO_TRANSLATE_labelProductName")!.Content.Should().Be("Git Extensions");

        about.FindControl<HyperlinkButton>("_NO_TRANSLATE_ThanksTo")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        WaitUntil(() => about.OwnedWindows.OfType<ContributorsWindow>().Any());
        ContributorsWindow contributors = about.OwnedWindows.OfType<ContributorsWindow>().Single();
        contributors.FindControl<TextBox>("DevelopersText")!.Text.Should().StartWith("Team:\r\n")
            .And.Contain("\r\n\r\nContributors:\r\n");
        contributors.FindControl<TextBox>("DesignersText")!.Text.Should().NotBeEmpty();
        contributors.Close();
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

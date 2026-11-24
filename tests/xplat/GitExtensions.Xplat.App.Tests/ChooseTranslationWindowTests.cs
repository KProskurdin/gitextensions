using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using GitCommands;
using GitExtensions.Xplat.Core.Settings;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

// Upstream's FormChooseTranslation: the first-start language question.
internal sealed class ChooseTranslationWindowTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(20);

    // The app's screenshot aid (Screenshot.EnvironmentVariable, internal to the app).
    private const string ScreenshotVariable = "XPLAT_SCREENSHOT";

    [TearDown]
    public void TearDown() => TestAppBuilder.Preferences.Translation = ChooseTranslationWindow.English;

    [AvaloniaTest]
    public void The_languages_come_with_their_flags_and_a_choice_is_stored()
    {
        TestAppBuilder.Preferences.Translation = "";
        Window owner = new();
        owner.Show();
        ChooseTranslationWindow window = new(TestAppBuilder.Preferences);
        _ = window.ShowDialog(owner);
        Dispatcher.UIThread.RunJobs();

        List<TranslationChoice> choices =
            [.. window.FindControl<ListBox>("lvTranslations")!.Items.OfType<TranslationChoice>()];
        choices[0].Name.Should().Be("English");
        choices.Select(choice => choice.Name).Should().Contain(["German", "Japanese"]);
        choices.Skip(1).Select(choice => choice.Name).Should().BeInAscendingOrder(StringComparer.CurrentCulture);
        choices.Should().OnlyContain(choice => choice.FlagPath != null, "upstream's flags are next to the app");
        window.Title.Should().Be("Choose language");

        window.Choose(choices.Single(choice => choice.Name == "German"));

        TestAppBuilder.Preferences.Translation.Should().Be("German");
        window.IsVisible.Should().BeFalse();
        owner.Close();
    }

    [AvaloniaTest]
    public void Closing_without_a_choice_keeps_english()
    {
        TestAppBuilder.Preferences.Translation = "";
        ChooseTranslationWindow window = new(TestAppBuilder.Preferences);
        window.Show();

        window.Close();

        TestAppBuilder.Preferences.Translation.Should().Be("English");
    }

    [AvaloniaTest]
    public void The_browse_window_asks_for_a_language_first_when_none_is_chosen()
    {
        TestAppBuilder.Preferences.Translation = "";
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        window.Show();

        WaitUntil(() => window.OwnedWindows.OfType<ChooseTranslationWindow>().Any());
        window.OwnedWindows.OfType<ChooseTranslationWindow>().Single().Close();

        WaitUntil(() => !window.OwnedWindows.OfType<ChooseTranslationWindow>().Any());
        TestAppBuilder.Preferences.Translation.Should().Be("English");
        window.Close();
    }

    [AvaloniaTest]
    public void A_screenshot_run_does_not_ask_for_a_language()
    {
        TestAppBuilder.Preferences.Translation = "";
        string screenshot = Path.Combine(Path.GetTempPath(), $"xplat-screenshot-{Guid.NewGuid():N}.png");
        string? oldScreenshot = Environment.GetEnvironmentVariable(ScreenshotVariable);
        Environment.SetEnvironmentVariable(ScreenshotVariable, screenshot);
        try
        {
            MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
            bool asked = false;
            window.Show();

            WaitUntil(() =>
            {
                asked |= window.OwnedWindows.OfType<ChooseTranslationWindow>().Any();
                return !window.IsVisible;
            });

            asked.Should().BeFalse();
            File.Exists(screenshot).Should().BeTrue();
            TestAppBuilder.Preferences.Translation.Should().BeEmpty();
        }
        finally
        {
            Environment.SetEnvironmentVariable(ScreenshotVariable, oldScreenshot);
            File.Delete(screenshot);
        }
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
            Thread.Sleep(20);
        }
    }
}

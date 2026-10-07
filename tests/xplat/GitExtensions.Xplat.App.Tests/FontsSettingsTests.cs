using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using GitCommands;
using GitExtensions.Xplat.Core.Settings;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

internal sealed class FontsSettingsTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(20);

    [SetUp]
    public void Setup() => TestAppBuilder.Preferences.ResetFonts();

    [TearDown]
    public void TearDown()
    {
        // Saving the settings once more applies the defaults again for the next tests.
        TestAppBuilder.Preferences.ResetFonts();
        MainWindow window = NewWindow();
        window.Show();
        Save(window, OpenSettings(window));
        window.Close();
    }

    [AvaloniaTest]
    public void The_fonts_tab_lists_upstreams_fonts_and_stores_a_chosen_one()
    {
        TestAppBuilder.Preferences.SetFont(AppFont.Monospace, new FontSetting("No Such Font", 9, Bold: true));
        TestAppBuilder.Preferences.SetFont(AppFont.Code, new FontSetting("Courier New", 10));
        MainWindow window = NewWindow();
        window.Show();
        SettingsWindow settings = OpenSettings(window);

        settings.FontBoxes.Keys.Should().Equal(AppFont.Application, AppFont.Commit, AppFont.Code, AppFont.Monospace);
        (ComboBox codeFamily, NumericUpDown codeSize) = settings.FontBoxes[AppFont.Code];
        codeFamily.SelectedItem.Should().Be("Courier New", "a stored family is listed even when it is not installed");
        (codeSize.IsEnabled, codeSize.Value).Should().Be((true, 10m));
        (ComboBox commitFamily, NumericUpDown commitSize) = settings.FontBoxes[AppFont.Commit];
        commitFamily.SelectedItem.Should().Be(SettingsWindow.DefaultFontChoice);
        commitSize.IsEnabled.Should().BeFalse();
        settings.FontBoxes[AppFont.Monospace].Family.SelectedItem.Should().Be("No Such Font");

        codeSize.Value = 12;
        Save(window, settings);

        TestAppBuilder.Preferences.GetFont(AppFont.Code).Should().Be(new FontSetting("Courier New", 12));
        TestAppBuilder.Preferences.GetFont(AppFont.Commit).Should().BeNull();
        TestAppBuilder.Preferences.GetFont(AppFont.Monospace).Should()
            .Be(new FontSetting("No Such Font", 9, Bold: true), "an untouched font is kept as stored");
    }

    [AvaloniaTest]
    public void Saved_fonts_apply_to_the_views_at_once_and_default_brings_the_apps_own_back()
    {
        MainWindow window = NewWindow();
        window.Show();
        TextBlock hash = Find<TextBlock>(window, "DetailHash");
        SelectableTextBlock message = Find<SelectableTextBlock>(window, "DetailMessage");

        TestAppBuilder.Preferences.SetFont(AppFont.Commit, new FontSetting("Arial", 15));
        TestAppBuilder.Preferences.SetFont(AppFont.Monospace, new FontSetting("Courier New", 6));
        TestAppBuilder.Preferences.SetFont(AppFont.Application, new FontSetting("Arial", 12));
        Save(window, OpenSettings(window));

        (message.FontFamily.Name, message.FontSize).Should().Be(("Arial", 20));
        hash.FontFamily.Name.Should().StartWith("Courier New");
        hash.FontSize.Should().Be(8);
        Application.Current!.Resources["ControlContentThemeFontSize"].Should().Be(16.0);
        TextBlock author = Find<TextBlock>(window, "DetailAuthor");
        (author.FontFamily.Name, author.FontSize).Should().Be(("Arial", 16), "the window takes Fluent's font resources");

        SettingsWindow settings = OpenSettings(window);
        settings.FontBoxes[AppFont.Commit].Family.SelectedItem = SettingsWindow.DefaultFontChoice;
        Save(window, settings);

        TestAppBuilder.Preferences.GetFont(AppFont.Commit).Should().BeNull();
        (message.FontFamily.Name, message.FontSize).Should().Be(("Arial", 16), "the commit font follows the application's");
    }

    private static SettingsWindow OpenSettings(MainWindow window)
    {
        Click(window, "SettingsMenuItem");
        WaitUntil(() => window.OwnedWindows.OfType<SettingsWindow>().Any());
        SettingsWindow settings = window.OwnedWindows.OfType<SettingsWindow>().Single();
        WaitUntil(() => settings.IsGitConfigLoaded);
        return settings;
    }

    private static void Save(MainWindow window, SettingsWindow settings)
    {
        Click(settings, "SaveButton");
        WaitUntil(() => !window.OwnedWindows.OfType<SettingsWindow>().Any());
        Dispatcher.UIThread.RunJobs();
    }

    private static MainWindow NewWindow() =>
        new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));

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

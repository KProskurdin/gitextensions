using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using GitCommands;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

/// <summary>
///  Upstream's startup check of the settings (<c>Program.Main</c>) and the Checklist's "Check settings at startup" box.
/// </summary>
internal sealed class StartupSettingsCheckUiTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(20);

    [SetUp]
    public void Setup()
    {
        // No user name or email: a setting the checklist finds wrong.
        TestAppBuilder.GitConfig.Clear();
        TestAppBuilder.Preferences.CheckSettings = true;
    }

    [TearDown]
    public void TearDown()
    {
        TestAppBuilder.GitConfig.Clear();
        TestAppBuilder.Preferences.CheckSettings = true;
    }

    [AvaloniaTest]
    public void The_app_window_opens_the_checklist_when_a_setting_is_wrong_and_its_box_turns_the_check_off()
    {
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null))
        {
            CheckSettingsAtStartup = true,
        };
        window.Show();

        WaitUntil(() => window.OwnedWindows.OfType<SettingsWindow>().Any());
        SettingsWindow settings = window.OwnedWindows.OfType<SettingsWindow>().Single();
        WaitUntil(() => settings.ChecklistEntries.Count > 0);
        Find<TabControl>(settings, "Tabs").SelectedItem.Should().Be(Find<TabItem>(settings, "ChecklistTab"));
        settings.IsChecklistValid.Should().BeFalse();
        CheckBox box = Find<CheckBox>(settings, "CheckAtStartup");
        box.IsChecked.Should().BeTrue("a setting is still wrong");

        box.IsChecked = false;
        TestAppBuilder.Preferences.CheckSettings.Should().BeFalse("the box writes the setting at once, as upstream's");

        UiClicks.Raise(Find<Button>(settings, "CancelButton"), Button.ClickEvent);
        WaitUntil(() => !window.OwnedWindows.OfType<SettingsWindow>().Any());
        window.Close();
    }

    private static T Find<T>(Window window, string name) where T : Control
        => window.FindControl<T>(name) ??
           window.GetLogicalDescendants().OfType<T>().FirstOrDefault(control => control.Name == name) ??
           throw new InvalidOperationException($"Control {name} not found");

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

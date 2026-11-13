using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using GitCommands;
using GitCommands.Settings;
using GitExtensions.Extensibility.Settings;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

// Upstream's DetailedSettingsPage: the push and merge window groups at the header's level, the revision graph group at
// the global level only.
internal sealed class DetailedSettingsTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private TestRepository _repo = null!;

    [SetUp]
    public void Setup()
    {
        _repo = new TestRepository();
        TestAppBuilder.UseRevisionLinks();
    }

    [TearDown]
    public void TearDown() => _repo.Dispose();

    [AvaloniaTest]
    public void The_push_and_merge_window_settings_are_stored_at_the_chosen_level()
    {
        MainWindow window = OpenWindow();
        SettingsWindow settings = OpenSettings(window);
        TabItem detailed = Find<TabItem>(settings, "DetailedTab");
        Find<TabControl>(settings, "Tabs").SelectedItem = detailed;
        Dispatcher.UIThread.RunJobs();
        CheckBox fromServer = Find<CheckBox>(settings, "chkRemotesFromServer");
        fromServer.IsChecked.Should().BeNull("nothing is configured");
        fromServer.IsEffectivelyEnabled.Should().BeFalse("upstream shows the effective settings read-only");

        settings.SourceHeaderOf(detailed).Level = SettingLevel.Local;
        Dispatcher.UIThread.RunJobs();
        fromServer.IsEffectivelyEnabled.Should().BeTrue();
        Find<StackPanel>(settings, "RevisionGraphGroup").IsEffectivelyEnabled.Should()
            .BeFalse("the revision graph settings are the user's");
        fromServer.IsChecked = true;
        Find<CheckBox>(settings, "addLogMessages").IsChecked = true;
        Find<TextBox>(settings, "nbMessages").Text = "7";
        Click(settings, "SaveButton");
        WaitUntil(() => !window.OwnedWindows.OfType<SettingsWindow>().Any());

        DistributedSettings stored = AppServices.RevisionLinks.Open(_repo.Path, SettingLevel.Local);
        DetailedSettings.GetRemoteBranchesDirectlyFromRemote[stored].Should().BeTrue();
        DetailedSettings.AddMergeLogMessages[stored].Should().BeTrue();
        DetailedSettings.MergeLogMessagesCount[stored].Should().Be(7);
        window.Close();
    }

    private MainWindow OpenWindow()
    {
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        window.Show();
        window.OpenRepository(_repo.Path);
        WaitUntil(() => Find<ListBox>(window, "CommitList").ItemCount == 2 && !window.IsBusy);
        return window;
    }

    private static SettingsWindow OpenSettings(MainWindow window)
    {
        Click(window, "SettingsMenuItem");
        WaitUntil(() => window.OwnedWindows.OfType<SettingsWindow>().Any());
        SettingsWindow settings = window.OwnedWindows.OfType<SettingsWindow>().Single();
        WaitUntil(() => settings.IsGitConfigLoaded);
        return settings;
    }

    private static void Click(Window window, string name)
    {
        Control control = Find<Control>(window, name);
        UiClicks.Raise(control, control is MenuItem ? MenuItem.ClickEvent : Button.ClickEvent);
    }

    private static T Find<T>(Window window, string name)
        where T : Control
        => window.FindControl<T>(name)
           ?? window.GetLogicalDescendants().OfType<T>().FirstOrDefault(control => control.Name == name)
           ?? throw new InvalidOperationException($"No control named {name}");

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

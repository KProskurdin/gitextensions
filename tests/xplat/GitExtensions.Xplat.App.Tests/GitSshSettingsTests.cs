using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using GitCommands;
using GitExtensions.Xplat.Core.Settings;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

internal sealed class GitSshSettingsTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(20);

    [SetUp]
    public void Setup() => Reset();

    [TearDown]
    public void TearDown() => Reset();

    [AvaloniaTest]
    public void The_ssh_tab_stores_upstreams_gitssh_and_sets_GIT_SSH()
    {
        TestAppBuilder.Preferences.SshPath = "/opt/ssh/my-ssh";
        SettingsWindow settings = OpenSettings(out MainWindow window);

        settings.SelectedSshClient.Should().Be(SshClientKind.Other);
        Find<TextBox>(settings, "OtherSshBox").Text.Should().Be("/opt/ssh/my-ssh");
        Find<TextBox>(settings, "OtherSshBox").IsEnabled.Should().BeTrue();
        Find<StackPanel>(settings, "PuttyPanel").IsVisible.Should().BeFalse();

        Find<RadioButton>(settings, "PuttyRadio").IsChecked = true;
        Find<StackPanel>(settings, "PuttyPanel").IsVisible.Should().BeTrue();
        Find<TextBox>(settings, "OtherSshBox").IsEnabled.Should().BeFalse();
        Find<TextBox>(settings, "PlinkBox").Text = @"C:\PuTTY\plink.exe";
        Find<TextBox>(settings, "PuttygenBox").Text = @"C:\PuTTY\puttygen.exe";
        Find<TextBox>(settings, "PageantBox").Text = @"C:\PuTTY\pageant.exe";
        Find<CheckBox>(settings, "AutoStartPageantCheck").IsChecked = false;
        Save(window, settings);

        IAppPreferences preferences = TestAppBuilder.Preferences;
        (preferences.SshPath, preferences.Plink, preferences.Puttygen, preferences.Pageant,
                preferences.AutoStartPageant)
            .Should().Be((@"C:\PuTTY\plink.exe", @"C:\PuTTY\plink.exe", @"C:\PuTTY\puttygen.exe",
                @"C:\PuTTY\pageant.exe", false));
        Environment.GetEnvironmentVariable("GIT_SSH").Should().Be(@"C:\PuTTY\plink.exe");
    }

    [AvaloniaTest]
    public void OpenSSH_clears_gitssh_and_GIT_SSH()
    {
        TestAppBuilder.Preferences.SshPath = "/opt/ssh/my-ssh";
        SshClients.Apply("/opt/ssh/my-ssh");
        SettingsWindow settings = OpenSettings(out MainWindow window);

        Find<RadioButton>(settings, "OpenSshRadio").IsChecked = true;
        Save(window, settings);

        TestAppBuilder.Preferences.SshPath.Should().BeEmpty();
        Environment.GetEnvironmentVariable("GIT_SSH").Should().BeNull();
    }

    [AvaloniaTest]
    public void The_git_tab_shows_HOME_and_keeps_the_git_command()
    {
        SettingsWindow settings = OpenSettings(out MainWindow window);

        Find<TextBlock>(settings, "HomeText").Text.Should().Be(HomeSettings.Describe(
            Environment.GetEnvironmentVariable(HomeSettings.GitConfigGlobalVariable),
            Environment.GetEnvironmentVariable("HOME") ?? "", OperatingSystem.IsWindows()));
        Find<StackPanel>(settings, "HomeChoicePanel").IsVisible.Should().Be(OperatingSystem.IsWindows());
        Find<StackPanel>(settings, "LinuxToolsPanel").IsVisible.Should().Be(OperatingSystem.IsWindows());
        Find<TextBox>(settings, "GitCommandBox").Text = " /usr/local/bin/git ";
        Save(window, settings);

        TestAppBuilder.Preferences.GitCommand.Should().Be("/usr/local/bin/git");
    }

    [AvaloniaTest]
    [Platform("Win")]
    public void Another_HOME_must_be_a_folder_that_exists()
    {
        string folder = Path.Combine(Path.GetTempPath(), "xplat-home-" + Guid.NewGuid().ToString("N"));
        SettingsWindow settings = OpenSettings(out MainWindow window);
        settings.SelectedHomeChoice.Should().Be(HomeChoice.Default);

        Find<RadioButton>(settings, "OtherHomeRadio").IsChecked = true;
        Click(settings, "SaveButton");
        Find<TextBlock>(settings, "ErrorText").Text.Should().Be(HomeSettings.NoHomeDirectorySpecified);

        Find<TextBox>(settings, "OtherHomeBox").Text = folder;
        Click(settings, "SaveButton");
        Find<TextBlock>(settings, "ErrorText").Text.Should().Contain("not accessible");
        window.OwnedWindows.OfType<SettingsWindow>().Should().ContainSingle();

        Directory.CreateDirectory(folder);
        try
        {
            Save(window, settings);
            (TestAppBuilder.Preferences.CustomHomeDir, TestAppBuilder.Preferences.UserProfileHomeDir)
                .Should().Be((folder, false));
        }
        finally
        {
            Directory.Delete(folder);
        }
    }

    private static void Reset()
    {
        IAppPreferences preferences = TestAppBuilder.Preferences;
        preferences.SshPath = "";
        preferences.Plink = "";
        preferences.Puttygen = "";
        preferences.Pageant = "";
        preferences.AutoStartPageant = true;
        preferences.CustomHomeDir = "";
        preferences.UserProfileHomeDir = false;
        preferences.LinuxToolsDir = "";
        preferences.GitCommand = "";
        SshClients.Apply("");
    }

    private static SettingsWindow OpenSettings(out MainWindow window)
    {
        window = new MainWindow(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        window.Show();
        Click(window, "SettingsMenuItem");
        MainWindow owner = window;
        WaitUntil(() => owner.OwnedWindows.OfType<SettingsWindow>().Any());
        SettingsWindow settings = window.OwnedWindows.OfType<SettingsWindow>().Single();
        WaitUntil(() => settings.IsGitConfigLoaded);
        return settings;
    }

    private static void Save(MainWindow window, SettingsWindow settings)
    {
        Click(settings, "SaveButton");
        WaitUntil(() => !window.OwnedWindows.OfType<SettingsWindow>().Any());
    }

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

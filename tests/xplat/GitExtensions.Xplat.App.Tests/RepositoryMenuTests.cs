using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using GitCommands;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

internal sealed class RepositoryMenuTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private TestRepository _repo = null!;

    [SetUp]
    public void SetUp() => _repo = new TestRepository();

    [TearDown]
    public void TearDown() => _repo.Dispose();

    [AvaloniaTest]
    public void Repository_settings_opens_the_settings_on_the_git_config_page()
    {
        MainWindow main = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        main.Show();
        main.OpenRepository(_repo.Path);
        WaitUntil(() => !main.IsBusy);
        MenuItem item = main.FindControl<MenuItem>("RepositorySettingsMenuItem")!;
        item.IsEnabled.Should().BeTrue();

        item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        WaitUntil(() => main.OwnedWindows.OfType<SettingsWindow>().Any());
        SettingsWindow settings = main.OwnedWindows.OfType<SettingsWindow>().Single();
        settings.FindControl<TabControl>("Tabs")!.SelectedItem.Should().Be(settings.FindControl<TabItem>("GitConfigTab"));
        settings.Close();
        main.Close();
    }

    [AvaloniaTest]
    public void PuTTY_shows_upstreams_error_when_the_program_is_missing()
    {
        string missing = Path.Combine(_repo.Path, "no-pageant.exe");
        TestAppBuilder.Preferences.Pageant = missing;
        try
        {
            MainWindow main = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
            main.Show();
            main.OpenRepository(_repo.Path);
            WaitUntil(() => !main.IsBusy);
            main.FindControl<MenuItem>("PuttyMenuItem")!.IsVisible.Should().Be(OperatingSystem.IsWindows());

            main.FindControl<MenuItem>("StartAuthenticationAgentMenuItem")!
                .RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            WaitUntil(() => main.OwnedWindows.OfType<ErrorWindow>().Any());
            ErrorWindow error = main.OwnedWindows.OfType<ErrorWindow>().Single();
            error.FindControl<TextBlock>("MessageText")!.Text.Should().Be($"'{missing}'\n\nIs PuTTY installed?");
            error.Close();
            main.Close();
        }
        finally
        {
            TestAppBuilder.Preferences.Pageant = "";
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
        }
    }
}

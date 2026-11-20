using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using GitCommands;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

// Upstream FileViewer's settings button: UICommands.StartSettingsDialog(ParentForm, DiffViewerSettingsPage), where the
// commands are those of the browse window the viewer's form belongs to.
internal sealed class DiffViewSettingsTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(20);

    private TestRepository _repo = null!;

    [SetUp]
    public void Setup() => _repo = new TestRepository();

    [TearDown]
    public void TearDown() => _repo.Dispose();

    [AvaloniaTest]
    public void A_viewers_settings_are_those_of_its_own_browse_window_not_of_a_newer_one()
    {
        MainWindow first = NewWindow();
        first.Show();
        Open(first, _repo.Path);
        first.FindControl<TabControl>("DetailTabs")!.SelectedItem = first.FindControl<TabItem>("DiffTab");
        Dispatcher.UIThread.RunJobs();

        // A newer browse window without a repository, closed again.
        MainWindow second = NewWindow();
        second.Show();
        second.Close();

        DiffView diff = first.FindControl<DiffView>("CommitDiff")!;
        SettingsWindow settings = OpenViewerSettings(first, diff);

        settings.Owner.Should().BeSameAs(first);
        settings.ViewModel.BuildServerPage.HasRepository.Should().BeTrue("the settings are the first window's, with its repository");
        settings.Close();
    }

    [AvaloniaTest]
    public void A_viewer_in_a_window_a_browse_window_owns_opens_its_settings_over_its_own_window()
    {
        MainWindow browse = NewWindow();
        browse.Show();
        Open(browse, _repo.Path);
        DiffView diff = new();
        Window viewer = new() { Content = diff, Width = 900, Height = 600 };
        viewer.Show(browse);
        Dispatcher.UIThread.RunJobs();

        SettingsWindow settings = OpenViewerSettings(viewer, diff);

        settings.Owner.Should().BeSameAs(viewer);
        settings.ViewModel.BuildServerPage.HasRepository.Should().BeTrue("the browse window's repository is the viewer's");
        settings.Close();
    }

    [AvaloniaTest]
    public void A_viewer_no_browse_window_owns_hides_the_settings_button()
    {
        DiffView diff = new();
        Window window = new() { Content = diff, Width = 900, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        window.MouseMove(new Point(450, 300));
        Dispatcher.UIThread.RunJobs();

        diff.ViewModel.IsSettingsVisible.Should().BeFalse();
    }

    private static SettingsWindow OpenViewerSettings(Window window, DiffView diff)
    {
        // The toolbar, with its settings button, is the pointer's: it shows while the pointer is on the viewer.
        Point center = diff.TranslatePoint(new Point(diff.Bounds.Width / 2, diff.Bounds.Height / 2), window)!.Value;
        window.MouseMove(center);
        Dispatcher.UIThread.RunJobs();
        diff.ViewModel.IsSettingsVisible.Should().BeTrue();

        diff.ViewModel.ShowSettingsCommand.Execute(null);
        SettingsWindow? settings = null;
        WaitUntil(() => (settings = window.OwnedWindows.OfType<SettingsWindow>().SingleOrDefault()) is not null);
        WaitUntil(() => settings!.IsGitConfigLoaded);
        return settings!;
    }

    private static MainWindow NewWindow() =>
        new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));

    private static void Open(MainWindow window, string path)
    {
        window.OpenRepository(path);
        WaitUntil(() => !window.IsBusy);
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

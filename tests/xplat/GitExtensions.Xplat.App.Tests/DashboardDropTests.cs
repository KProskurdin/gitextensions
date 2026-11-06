using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using GitCommands;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

// Upstream's dashboard (UserRepositoriesList): a folder dropped on the repository list, and the Dashboard menu's
// "Recent repositories settings".
internal sealed class DashboardDropTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(20);

    [AvaloniaTest]
    public void A_repository_folder_dropped_on_the_list_opens()
    {
        using TestRepository repo = new();
        MainWindow window = NewWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Drop(window, repo.Path);

        WaitUntil(() => window.FindControl<Control>("BrowsePanel")!.IsVisible);
        window.Close();
    }

    [AvaloniaTest]
    public void A_folder_that_is_no_repository_gets_upstreams_message()
    {
        string folder = Path.Combine(Path.GetTempPath(), "xplat-drop-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            MainWindow window = NewWindow();
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Drop(window, folder);

            WaitUntil(() => window.OwnedWindows.OfType<MessageBoxWindow>().Any());
            MessageBoxWindow message = window.OwnedWindows.OfType<MessageBoxWindow>().Single();
            message.Title.Should().Be(MainWindow.CannotOpenFolderCaption);
            message.Close();
            window.FindControl<Control>("BrowsePanel")!.IsVisible.Should().BeFalse();
            window.Close();
        }
        finally
        {
            Directory.Delete(folder);
        }
    }

    [AvaloniaTest]
    public void The_dashboard_menu_opens_the_recent_repositories_settings()
    {
        MainWindow window = NewWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        window.FindControl<MenuItem>("DashboardRecentReposSettingsMenuItem")!
            .RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

        WaitUntil(() => window.OwnedWindows.OfType<RecentRepositoriesSettingsWindow>().Any());
        window.OwnedWindows.OfType<RecentRepositoriesSettingsWindow>().Single().Close();
        window.Close();
    }

    private static MainWindow NewWindow() =>
        new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));

    private static void Drop(MainWindow window, string folder)
    {
        Control list = window.FindControl<Control>("DashboardRepositories")!;
        Point point = list.TranslatePoint(new Point(list.Bounds.Width / 2, list.Bounds.Height / 2), window)!.Value;
        DataTransfer data = new();
        data.Add(DataTransferItem.CreateFile(LocalFolder(folder)));
        window.DragDrop(point, RawDragEventType.DragEnter, data, DragDropEffects.Copy, RawInputModifiers.None);
        window.DragDrop(point, RawDragEventType.DragOver, data, DragDropEffects.Copy, RawInputModifiers.None);
        window.DragDrop(point, RawDragEventType.Drop, data, DragDropEffects.Copy, RawInputModifiers.None);
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

    // A folder as a file manager drops it. Avalonia's storage items cannot be implemented outside Avalonia and the headless
    // platform has no storage provider, so this is Avalonia's own folder item for a local path.
    private static IStorageFolder LocalFolder(string path)
    {
        Type type = typeof(IStorageFolder).Assembly.GetType("Avalonia.Platform.Storage.FileIO.BclStorageFolder")
                    ?? throw new InvalidOperationException("Avalonia's BclStorageFolder is missing");
        return (IStorageFolder)Activator.CreateInstance(type,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public
                                                    | System.Reflection.BindingFlags.NonPublic,
            binder: null, [new DirectoryInfo(path)], culture: null)!;
    }
}

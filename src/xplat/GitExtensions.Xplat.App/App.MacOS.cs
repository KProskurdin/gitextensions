using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using GitExtensions.Xplat.Core.Platform;

namespace GitExtensions.Xplat.App;

/// <summary>
///  What only macOS asks of an app: the application menu, and folders that arrive as events from Finder and the Dock, not
///  on the command line.
/// </summary>
public partial class App
{
    private const string AboutText = "About Git Extensions";
    private const string SettingsText = "Settings...";

    /// <summary>
    ///  The application menu's own items, before the ones macOS adds (Services, Hide, Quit).
    /// </summary>
    public static NativeMenu ApplicationMenu(KeyGesture? settingsGesture, Action showAbout, Action showSettings)
    {
        NativeMenuItem about = new(AboutText);
        about.Click += (_, _) => showAbout();
        NativeMenuItem settings = new(SettingsText) { Gesture = settingsGesture };
        settings.Click += (_, _) => showSettings();
        return new NativeMenu { about, new NativeMenuItemSeparator(), settings };
    }

    private void SetUpMacOS(IClassicDesktopStyleApplicationLifetime desktop)
    {
        if (!OperatingSystem.IsMacOS() || desktop.MainWindow is not MainWindow)
        {
            return;
        }

        NativeMenu.SetMenu(this, ApplicationMenu(Hotkeys.GestureFor(BrowseCommand.Settings),
            () => (desktop.MainWindow as MainWindow)?.ShowAbout(),
            () => (desktop.MainWindow as MainWindow)?.ShowSettings()));

        if (this.TryGetFeature<IActivatableLifetime>() is { } activatable)
        {
            activatable.Activated += (_, e) =>
            {
                if (e is FileActivatedEventArgs { Files: [{ } first, ..] }
                    && first.TryGetLocalPath() is { } path && desktop.MainWindow is MainWindow window)
                {
                    window.OpenRepository(OpenedItem.Folder(path, Directory.Exists));
                }
            };
        }
    }
}

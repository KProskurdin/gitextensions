using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Platform;
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
    ///  The Dock's icon on macOS: the largest logo, from which the bundle's .icns is made too (publish-app.sh).
    /// </summary>
    public const string DockIconAsset = "avares://GitExtensions/Assets/git-extensions-logo-512px.png";

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

    /// <summary>
    ///  Shows the logo in the Dock on macOS, where the windows' icon (the style in App.axaml) does nothing; see
    ///  <see cref="MacApplicationIcon"/>. Every mode of the app, as git's editor and password prompt are in the Dock too.
    /// </summary>
    public static void ShowMacOSDockIcon()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        using Stream logo = AssetLoader.Open(new Uri(DockIconAsset));
        using MemoryStream png = new();
        logo.CopyTo(png);
        if (!MacApplicationIcon.Set(png.ToArray()))
        {
            Trace.WriteLine("macOS could not read the Dock icon.");
        }
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

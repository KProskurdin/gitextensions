using Avalonia.Controls;
using GitCommands.Settings;
using GitExtensions.Xplat.Core.Diff;

namespace GitExtensions.Xplat.App;

// Upstream FileViewer's toolbar (fileviewerToolbar) and menu (contextMenu): their state and commands are the view model's;
// here the pointer that shows the toolbar, the menu's opening and the window the settings open over.
public partial class DiffView
{
    /// <summary>
    ///  Opens the settings window on a page over a window (the browse window's); null hides the toolbar's settings button.
    /// </summary>
    public static Func<Window, string, Task>? ShowSettings { get; set; }

    private void WireMenu()
    {
        // Upstream's contextMenu_Opening.
        DiffContextMenu.Opening += (_, _) =>
        {
            SyncSelection();
            ViewModel.RefreshMenu();
        };

        // As upstream, the toolbar shows while the pointer is on the viewer; the settings button opens the settings over
        // this viewer's window, when the app can.
        PointerEntered += (_, _) =>
        {
            ViewModel.OpenSettings = ShowSettings is { } showSettings
                ? page => TopLevel.GetTopLevel(this) is Window owner ? showSettings(owner, page) : Task.CompletedTask
                : null;
            ViewModel.IsPointerOver = true;
        };
        PointerExited += (_, _) => ViewModel.IsPointerOver = false;
    }
}

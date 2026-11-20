using Avalonia.Controls;
using GitCommands.Settings;
using GitExtensions.Xplat.Core.Diff;

namespace GitExtensions.Xplat.App;

// Upstream FileViewer's toolbar (fileviewerToolbar) and menu (contextMenu): their state and commands are the view model's;
// here the pointer that shows the toolbar, the menu's opening and the window the settings open over.
public partial class DiffView
{
    private void WireMenu()
    {
        // Upstream's contextMenu_Opening.
        DiffContextMenu.Opening += (_, _) =>
        {
            SyncSelection();
            ViewModel.RefreshMenu();
        };

        // As upstream, the toolbar shows while the pointer is on the viewer; the settings button opens the settings over
        // this viewer's window, run by the browse window that owns it (upstream's UICommands of the viewer's form). A
        // window no browse window owns hides the button. Found when the pointer comes, as a window gets its owner when
        // it is shown, after the viewer is made.
        PointerEntered += (_, _) =>
        {
            ViewModel.OpenSettings = TopLevel.GetTopLevel(this) is Window owner
                                     && ISettingsWindowHost.Of(owner) is { } host
                ? page => host.ShowSettingsAsync(owner, page)
                : null;
            ViewModel.IsPointerOver = true;
        };
        PointerExited += (_, _) => ViewModel.IsPointerOver = false;
    }
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using IWin32Window = System.Windows.Forms.IWin32Window;

namespace GitExtensions.Xplat.Ui;

/// <summary>
///  Shows a window modally and returns when it closes, as a WinForms <c>ShowDialog</c> does: upstream code (the shared core's
///  message boxes, plugins) expects the answer when the call returns. The window runs in a nested dispatcher frame, as a
///  WinForms modal dialog runs its own message loop. The window keeps its own answer (e.g. <see cref="IDialogWindow"/>).
/// </summary>
public static class ModalWindow
{
    /// <summary>
    ///  Creates the window with <paramref name="create"/>, shows it over <paramref name="owner"/> (an
    ///  <see cref="WindowOwner"/>), or over the active window when the owner is not one, and returns it once it has closed.
    ///  Called from another thread, it runs on the UI thread and waits for it.
    /// </summary>
    public static TWindow Show<TWindow>(Func<TWindow> create, IWin32Window? owner)
        where TWindow : Window
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            return Dispatcher.UIThread.Invoke(() => Show(create, owner));
        }

        TWindow window = create();
        DispatcherFrame frame = new();
        window.Closed += (_, _) => frame.Continue = false;
        if (OwnerWindow(owner) is { } parent)
        {
            _ = window.ShowDialog(parent);
        }
        else
        {
            window.Show();
        }

        Dispatcher.UIThread.PushFrame(frame);
        return window;
    }

    /// <summary>
    ///  The window behind <paramref name="owner"/>, or the active window of the app (its main window when none is active).
    /// </summary>
    public static Window? OwnerWindow(IWin32Window? owner)
        => (owner as WindowOwner)?.Window ?? ActiveWindow();

    private static Window? ActiveWindow()
        => Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.Windows.FirstOrDefault(window => window.IsActive) ?? desktop.MainWindow
            : null;
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using DialogResult = System.Windows.Forms.DialogResult;
using IWin32Window = System.Windows.Forms.IWin32Window;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;
using MessageBoxDefaultButton = System.Windows.Forms.MessageBoxDefaultButton;
using MessageBoxIcon = System.Windows.Forms.MessageBoxIcon;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Shows the message boxes of upstream code (the shared core's <c>MessageBoxes</c>, and plugins) as
///  <see cref="MessageBoxWindow"/>. Upstream expects the answer when the call returns, so the box runs in a nested
///  dispatcher frame, as a WinForms modal dialog runs its own message loop.
/// </summary>
public static class MessageBoxHost
{
    /// <summary>
    ///  Answers the WinForms shim's message boxes from now on. Not done for the headless tests: a box nobody answers would
    ///  stop a test.
    /// </summary>
    public static void Install() => System.Windows.Forms.MessageBox.Handler = Show;

    public static DialogResult Show(IWin32Window? owner, string text, string caption, MessageBoxButtons buttons,
        MessageBoxIcon icon, MessageBoxDefaultButton defaultButton)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            return Dispatcher.UIThread.Invoke(() => Show(owner, text, caption, buttons, icon, defaultButton));
        }

        MessageBoxWindow box = new(text, caption, buttons, defaultButton);
        DispatcherFrame frame = new();
        box.Closed += (_, _) => frame.Continue = false;
        if (((owner as WindowOwner)?.Window ?? ActiveWindow()) is { } window)
        {
            _ = box.ShowDialog(window);
        }
        else
        {
            box.Show();
        }

        Dispatcher.UIThread.PushFrame(frame);
        return box.Result;
    }

    private static Window? ActiveWindow()
        => Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.Windows.FirstOrDefault(window => window.IsActive) ?? desktop.MainWindow
            : null;
}

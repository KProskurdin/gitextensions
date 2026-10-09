using Avalonia.Controls;
using IWin32Window = System.Windows.Forms.IWin32Window;

namespace GitExtensions.Xplat.Ui;

/// <summary>
///  An Avalonia window as the WinForms owner upstream code passes around.
/// </summary>
public sealed class WindowOwner(Window window) : IWin32Window
{
    public Window Window => window;

    public IntPtr Handle => window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
}

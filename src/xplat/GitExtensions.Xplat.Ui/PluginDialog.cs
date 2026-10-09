using Avalonia.Controls;
using DialogResult = System.Windows.Forms.DialogResult;
using IWin32Window = System.Windows.Forms.IWin32Window;

namespace GitExtensions.Xplat.Ui;

/// <summary>
///  The base of the stand-ins for upstream plugins' WinForms forms. An upstream plugin is compiled unchanged and creates its
///  form by name, then calls <c>ShowDialog(args.OwnerForm)</c>; the stand-in has that name and those members, and shows an
///  Avalonia window that reimplements the form instead, as a <see cref="ModalWindow"/>.
/// </summary>
public abstract class PluginDialog : IDisposable
{
    public DialogResult DialogResult { get; private set; }

    public DialogResult ShowDialog(IWin32Window? owner)
    {
        Window window = ModalWindow.Show(CreateWindow, owner);
        DialogResult = (window as IDialogWindow)?.DialogResult ?? DialogResult.Cancel;
        return DialogResult;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///  Creates the window, on the UI thread, each time the dialog is shown. It implements <see cref="IDialogWindow"/> when
    ///  the plugin reads the answer.
    /// </summary>
    protected abstract Window CreateWindow();
}

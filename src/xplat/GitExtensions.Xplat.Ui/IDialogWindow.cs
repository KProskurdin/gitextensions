using DialogResult = System.Windows.Forms.DialogResult;

namespace GitExtensions.Xplat.Ui;

/// <summary>
///  A window that reimplements a WinForms dialog and keeps the <see cref="DialogResult"/> it closed with, as a WinForms form
///  does. A window closed without one answers <see cref="DialogResult.Cancel"/>, as closing a WinForms dialog does.
/// </summary>
public interface IDialogWindow
{
    DialogResult DialogResult { get; }
}

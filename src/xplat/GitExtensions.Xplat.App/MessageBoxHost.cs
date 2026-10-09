using GitExtensions.Xplat.Ui;
using DialogResult = System.Windows.Forms.DialogResult;
using IWin32Window = System.Windows.Forms.IWin32Window;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;
using MessageBoxDefaultButton = System.Windows.Forms.MessageBoxDefaultButton;
using MessageBoxIcon = System.Windows.Forms.MessageBoxIcon;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Shows the message boxes of upstream code (the shared core's <c>MessageBoxes</c>, and plugins) as
///  <see cref="MessageBoxWindow"/>. Upstream expects the answer when the call returns, so the box is a
///  <see cref="ModalWindow"/>, which runs in a nested dispatcher frame as a WinForms modal dialog runs its own message loop.
/// </summary>
public static class MessageBoxHost
{
    /// <summary>
    ///  Answers the WinForms shim's message boxes and task dialogs from now on. Not done for the headless tests: a box
    ///  nobody answers would stop a test.
    /// </summary>
    public static void Install()
    {
        Answer(Show);
        System.Windows.Forms.TaskDialog.Handler = page =>
            Task.FromResult(ModalWindow.Show(() => new MessageBoxWindow(page), owner: null).ChosenButton);
    }

    /// <summary>
    ///  Lets <paramref name="handler"/> answer the shim's message boxes, or no one when it is null (a box then answers
    ///  <see cref="DialogResult.None"/>). Tests use it to record the boxes and answer them.
    /// </summary>
    public static void Answer(
        Func<IWin32Window?, string, string, MessageBoxButtons, MessageBoxIcon, MessageBoxDefaultButton, DialogResult>? handler)
        => System.Windows.Forms.MessageBox.Handler = handler;

    public static DialogResult Show(IWin32Window? owner, string text, string caption, MessageBoxButtons buttons,
        MessageBoxIcon icon, MessageBoxDefaultButton defaultButton)
    {
        return ModalWindow.Show(() => new MessageBoxWindow(text, caption, buttons, defaultButton), owner).Result;
    }
}

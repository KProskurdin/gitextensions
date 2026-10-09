using Avalonia.Controls;
using DialogResult = System.Windows.Forms.DialogResult;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;
using MessageBoxDefaultButton = System.Windows.Forms.MessageBoxDefaultButton;
using TaskDialogButton = System.Windows.Forms.TaskDialogButton;
using TaskDialogPage = System.Windows.Forms.TaskDialogPage;

namespace GitExtensions.Xplat.App;

/// <summary>
///  The new shell's WinForms message box, for the message boxes upstream code shows (the shared core and plugins): the
///  text, the caption as title, and WinForms' buttons. <see cref="Result"/> is the button pressed.
/// </summary>
public partial class MessageBoxWindow : Window
{
    public MessageBoxWindow(string text, string caption, MessageBoxButtons buttons,
        MessageBoxDefaultButton defaultButton = MessageBoxDefaultButton.Button1)
    {
        InitializeComponent();
        Title = string.IsNullOrEmpty(caption) ? "Git Extensions" : caption;
        MessageText.Text = text;

        IReadOnlyList<DialogResult> results = ButtonsOf(buttons);
        int defaultIndex = defaultButton switch
        {
            MessageBoxDefaultButton.Button2 => 1,
            MessageBoxDefaultButton.Button3 => 2,
            _ => 0,
        };

        // As WinForms: closing the box is Cancel when there is a Cancel button; with OK alone it is OK.
        Result = results.Contains(DialogResult.Cancel) ? DialogResult.Cancel
            : results is [DialogResult.OK] ? DialogResult.OK
            : DialogResult.None;
        for (int index = 0; index < results.Count; index++)
        {
            DialogResult result = results[index];
            Button button = new()
            {
                Content = TextOf(result),
                Name = $"{result}Button",
                MinWidth = 80,
                IsDefault = index == Math.Min(defaultIndex, results.Count - 1),
                IsCancel = result == DialogResult.Cancel,
            };
            button.Click += (_, _) =>
            {
                Result = result;
                Close(result);
            };
            ButtonsPanel.Children.Add(button);
        }
    }

    /// <summary>
    ///  A WinForms task dialog shown as a message box: the heading above the text, and the page's own buttons.
    ///  <see cref="ChosenButton"/> is the button pressed; closing the box is WinForms' Cancel.
    /// </summary>
    public MessageBoxWindow(TaskDialogPage page)
    {
        InitializeComponent();
        Title = string.IsNullOrEmpty(page.Caption) ? "Git Extensions" : page.Caption;
        MessageText.Text = string.Join(Environment.NewLine + Environment.NewLine,
            new string?[] { page.Heading, page.Text }.Where(part => !string.IsNullOrEmpty(part)));
        for (int index = 0; index < page.Buttons.Count; index++)
        {
            TaskDialogButton choice = page.Buttons[index];
            Button button = new() { Content = choice.Text, MinWidth = 80, IsDefault = index == 0 };
            button.Click += (_, _) =>
            {
                ChosenButton = choice;
                Close();
            };
            ButtonsPanel.Children.Add(button);
        }
    }

    /// <summary>
    ///  The button pressed, or the result of closing the box.
    /// </summary>
    public DialogResult Result { get; private set; }

    /// <summary>
    ///  The task dialog button pressed.
    /// </summary>
    public TaskDialogButton ChosenButton { get; private set; } = TaskDialogButton.Cancel;

    private static IReadOnlyList<DialogResult> ButtonsOf(MessageBoxButtons buttons) => buttons switch
    {
        MessageBoxButtons.OKCancel => [DialogResult.OK, DialogResult.Cancel],
        MessageBoxButtons.AbortRetryIgnore => [DialogResult.Abort, DialogResult.Retry, DialogResult.Ignore],
        MessageBoxButtons.YesNoCancel => [DialogResult.Yes, DialogResult.No, DialogResult.Cancel],
        MessageBoxButtons.YesNo => [DialogResult.Yes, DialogResult.No],
        MessageBoxButtons.RetryCancel => [DialogResult.Retry, DialogResult.Cancel],
        MessageBoxButtons.CancelTryContinue => [DialogResult.Cancel, DialogResult.TryAgain, DialogResult.Continue],
        _ => [DialogResult.OK],
    };

    private static string TextOf(DialogResult result) => result switch
    {
        DialogResult.TryAgain => "Try again",
        _ => result.ToString(),
    };
}

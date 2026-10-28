using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using DialogResult = System.Windows.Forms.DialogResult;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;
using MessageBoxDefaultButton = System.Windows.Forms.MessageBoxDefaultButton;
using TaskDialogButton = System.Windows.Forms.TaskDialogButton;
using TaskDialogCommandLinkButton = System.Windows.Forms.TaskDialogCommandLinkButton;
using TaskDialogPage = System.Windows.Forms.TaskDialogPage;

namespace GitExtensions.Xplat.App;

/// <summary>
///  The new shell's WinForms message box, for the message boxes upstream code shows (the shared core and plugins): the
///  text, the caption as title, and WinForms' buttons. <see cref="Result"/> is the button pressed.
/// </summary>
public partial class MessageBoxWindow : Window
{
    private const string CommandLinkArrow = "➔";

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
    ///  A WinForms task dialog shown as a message box: the heading above the text, the command links in rows of their own,
    ///  the page's other buttons and its verification check box. <see cref="ChosenButton"/> is the button pressed; closing
    ///  the box is WinForms' Cancel. The verification choice is written back to the page.
    /// </summary>
    public MessageBoxWindow(TaskDialogPage page)
    {
        InitializeComponent();
        Title = string.IsNullOrEmpty(page.Caption) ? "Git Extensions" : page.Caption;
        HeadingText.Text = page.Heading;
        HeadingText.IsVisible = !string.IsNullOrEmpty(page.Heading);
        MessageText.Text = page.Text;
        if (page.Verification is { } verification)
        {
            VerificationCheck.Content = new AccessText { Text = verification.Text?.Replace('&', '_') };
            VerificationCheck.IsChecked = verification.Checked;
            VerificationCheck.IsVisible = true;
            Closed += (_, _) => verification.Checked = VerificationCheck.IsChecked == true;
        }

        for (int index = 0; index < page.Buttons.Count; index++)
        {
            TaskDialogButton choice = page.Buttons[index];
            bool isCommandLink = choice is TaskDialogCommandLinkButton;

            // WinForms' access keys are '&', Avalonia's '_'.
            AccessText label = new() { Text = choice.Text?.Replace('&', '_') };
            Button button = new()
            {
                Content = isCommandLink ? CommandLinkContent(label) : label,
                MinWidth = 80,
                IsDefault = page.DefaultButton is { } defaultButton ? choice == defaultButton : index == 0,
                IsCancel = choice == TaskDialogButton.Cancel,
                HorizontalAlignment = isCommandLink ? HorizontalAlignment.Stretch : HorizontalAlignment.Right,
                HorizontalContentAlignment = isCommandLink ? HorizontalAlignment.Left : HorizontalAlignment.Center,
            };
            if (isCommandLink)
            {
                button.Classes.Add("tool");
                button.Padding = new Thickness(8, 6);
            }

            button.Click += (_, _) =>
            {
                ChosenButton = choice;
                Close();
            };
            (isCommandLink ? CommandLinksPanel : ButtonsPanel).Children.Add(button);
        }

        CommandLinksPanel.IsVisible = CommandLinksPanel.Children.Count > 0;
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

    // WinForms draws command links flat, with an arrow and a frame only under the pointer.
    private static StackPanel CommandLinkContent(AccessText label)
    {
        TextBlock arrow = new() { Text = CommandLinkArrow, VerticalAlignment = VerticalAlignment.Center };
        arrow.Bind(TextBlock.ForegroundProperty, arrow.GetResourceObservable("SystemControlHighlightAccentBrush"));
        return new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { arrow, label } };
    }
}

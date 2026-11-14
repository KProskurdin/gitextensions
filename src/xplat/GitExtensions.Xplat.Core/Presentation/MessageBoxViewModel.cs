using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DialogResult = System.Windows.Forms.DialogResult;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;
using MessageBoxDefaultButton = System.Windows.Forms.MessageBoxDefaultButton;
using TaskDialogButton = System.Windows.Forms.TaskDialogButton;
using TaskDialogCommandLinkButton = System.Windows.Forms.TaskDialogCommandLinkButton;
using TaskDialogPage = System.Windows.Forms.TaskDialogPage;

namespace GitExtensions.Xplat.Core.Presentation;

/// <summary>
///  A button of the message box: its text (WinForms' access key marks turned into Avalonia's), the name tests find it by,
///  whether Enter or Escape presses it, and, for a task dialog's command link, its description.
/// </summary>
public sealed record MessageBoxChoice(
    string Text,
    string Name,
    bool IsDefault,
    bool IsCancel,
    DialogResult Result,
    TaskDialogButton? TaskButton = null,
    bool IsCommandLink = false,
    string? Description = null);

/// <summary>
///  The view model of the new shell's WinForms message box, for the message boxes upstream code shows (the shared core and
///  plugins) and for <see cref="IDialogHost"/>'s questions: the caption as title, the text, WinForms' buttons, and for a
///  task dialog its heading, command links and verification check box. <see cref="Result"/> or
///  <see cref="ChosenButton"/> is the button pressed.
/// </summary>
public sealed partial class MessageBoxViewModel : DialogViewModel
{
    private const string DefaultCaption = "Git Extensions";

    private readonly TaskDialogPage? _page;

    /// <summary>
    ///  A WinForms message box: the text, the caption as title, and WinForms' buttons.
    /// </summary>
    public MessageBoxViewModel(IDialogHost host, string text, string caption, MessageBoxButtons buttons,
        MessageBoxDefaultButton defaultButton = MessageBoxDefaultButton.Button1)
        : base(host)
    {
        Title = string.IsNullOrEmpty(caption) ? DefaultCaption : caption;
        Text = text;
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
        Choices =
        [
            .. results.Select((result, index) => new MessageBoxChoice(TextOf(result), $"{result}Button",
                IsDefault: index == Math.Min(defaultIndex, results.Count - 1), IsCancel: result == DialogResult.Cancel,
                result))
        ];
    }

    /// <summary>
    ///  A WinForms task dialog shown as a message box: the heading above the text, the command links in rows of their own,
    ///  the page's other buttons and its verification check box, which <see cref="OnClosed"/> writes back to the page.
    /// </summary>
    public MessageBoxViewModel(IDialogHost host, TaskDialogPage page)
        : base(host)
    {
        _page = page;
        Title = string.IsNullOrEmpty(page.Caption) ? DefaultCaption : page.Caption;
        Heading = string.IsNullOrEmpty(page.Heading) ? null : page.Heading;
        Text = page.Text ?? "";
        VerificationText = page.Verification?.Text?.Replace('&', '_');
        VerificationChecked = page.Verification?.Checked == true;
        Choices =
        [
            .. page.Buttons.Select((choice, index) => new MessageBoxChoice(
                choice.Text?.Replace('&', '_') ?? "",
                "",
                IsDefault: page.DefaultButton is { } defaultButton ? choice == defaultButton : index == 0,
                IsCancel: choice == TaskDialogButton.Cancel || choice == TaskDialogButton.Close,
                DialogResult.None,
                choice,
                choice is TaskDialogCommandLinkButton,
                (choice as TaskDialogCommandLinkButton)?.DescriptionText))
        ];
    }

    public string Title { get; }

    /// <summary>
    ///  A task dialog's heading, shown larger above the text; null for none.
    /// </summary>
    public string? Heading { get; }

    public string Text { get; }

    /// <summary>
    ///  The buttons, command links included, in the page's order.
    /// </summary>
    public IReadOnlyList<MessageBoxChoice> Choices { get; }

    /// <summary>
    ///  A task dialog's verification check box text; null for none.
    /// </summary>
    public string? VerificationText { get; }

    [ObservableProperty]
    public partial bool VerificationChecked { get; set; }

    /// <summary>
    ///  The button pressed, or the result of closing the box.
    /// </summary>
    public DialogResult Result { get; private set; }

    /// <summary>
    ///  The task dialog button pressed; WinForms' Cancel when the box was closed.
    /// </summary>
    public TaskDialogButton ChosenButton { get; private set; } = TaskDialogButton.Cancel;

    /// <summary>
    ///  Writes the verification choice back to the task dialog's page, when the window closes.
    /// </summary>
    public void OnClosed()
    {
        if (_page?.Verification is { } verification)
        {
            verification.Checked = VerificationChecked;
        }
    }

    [RelayCommand]
    private void Choose(MessageBoxChoice choice)
    {
        if (choice.TaskButton is { } taskButton)
        {
            ChosenButton = taskButton;
            Host.Close(null);
            return;
        }

        Result = choice.Result;
        Host.Close(choice.Result);
    }

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

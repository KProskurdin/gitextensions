using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using GitExtensions.Xplat.Core.Presentation;
using DialogResult = System.Windows.Forms.DialogResult;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;
using MessageBoxDefaultButton = System.Windows.Forms.MessageBoxDefaultButton;
using TaskDialogButton = System.Windows.Forms.TaskDialogButton;
using TaskDialogPage = System.Windows.Forms.TaskDialogPage;

namespace GitExtensions.Xplat.App;

/// <summary>
///  The new shell's WinForms message box, bound to <see cref="MessageBoxViewModel"/>, for the message boxes upstream code
///  shows (the shared core and plugins): the text, the caption as title, and WinForms' buttons. <see cref="Result"/> is
///  the button pressed.
/// </summary>
public partial class MessageBoxWindow : Window
{
    private const string CommandLinkArrow = "➔";

    // A command link's description is smaller than its text, as in WinForms.
    private const double DescriptionFontSize = 11;

    /// <summary>
    ///  The design-time constructor (<see cref="DesignTime"/>): the window's XAML only.
    /// </summary>
    [Obsolete(DesignTime.ConstructorOnly)]
#pragma warning disable CS8618 // The fields the window's data fills stay unset: no code of the window runs.
    public MessageBoxWindow() => InitializeComponent();
#pragma warning restore CS8618

    public MessageBoxWindow(string text, string caption, MessageBoxButtons buttons,
        MessageBoxDefaultButton defaultButton = MessageBoxDefaultButton.Button1)
    {
        ViewModel = new MessageBoxViewModel(new WindowDialogHost(this), text, caption, buttons, defaultButton);
        DataContext = ViewModel;
        InitializeComponent();
        AddButtons();
    }

    /// <summary>
    ///  A WinForms task dialog shown as a message box: the heading above the text, the command links in rows of their own,
    ///  the page's other buttons and its verification check box. <see cref="ChosenButton"/> is the button pressed; closing
    ///  the box is WinForms' Cancel. The verification choice is written back to the page.
    /// </summary>
    public MessageBoxWindow(TaskDialogPage page)
    {
        ViewModel = new MessageBoxViewModel(new WindowDialogHost(this), page);
        DataContext = ViewModel;
        InitializeComponent();
        AddButtons();
        Closed += (_, _) => ViewModel.OnClosed();
    }

    public MessageBoxViewModel ViewModel { get; }

    /// <summary>
    ///  The button pressed, or the result of closing the box.
    /// </summary>
    public DialogResult Result => ViewModel.Result;

    /// <summary>
    ///  The task dialog button pressed.
    /// </summary>
    public TaskDialogButton ChosenButton => ViewModel.ChosenButton;

    // View concern: the buttons are made from the choices, named as WinForms' results, and press the choice's command.
    private void AddButtons()
    {
        foreach (MessageBoxChoice choice in ViewModel.Choices)
        {
            // A task dialog's buttons have access keys (WinForms' '&', which the view model turned into Avalonia's '_'); a
            // message box's are plain text, as WinForms shows them.
            AccessText label = new() { Text = choice.Text };
            Button button = new()
            {
                Content = choice.TaskButton is null ? choice.Text
                    : choice.IsCommandLink ? CommandLinkContent(label, choice.Description)
                    : label,
                MinWidth = 80,
                IsDefault = choice.IsDefault,
                IsCancel = choice.IsCancel,
                HorizontalAlignment = choice.IsCommandLink ? HorizontalAlignment.Stretch : HorizontalAlignment.Right,
                HorizontalContentAlignment =
                    choice.IsCommandLink ? HorizontalAlignment.Left : HorizontalAlignment.Center,
            };
            if (choice.Name.Length > 0)
            {
                button.Name = choice.Name;
            }

            if (choice.IsCommandLink)
            {
                button.Classes.Add("tool");
                button.Padding = new Thickness(8, 6);
            }

            button.Click += (_, _) => ViewModel.ChooseCommand.Execute(choice);
            (choice.IsCommandLink ? CommandLinksPanel : ButtonsPanel).Children.Add(button);
        }

        CommandLinksPanel.IsVisible = CommandLinksPanel.Children.Count > 0;
    }

    // WinForms draws command links flat, with an arrow and a frame only under the pointer, and the description under the
    // text in a smaller font.
    private static StackPanel CommandLinkContent(AccessText label, string? description)
    {
        TextBlock arrow = new() { Text = CommandLinkArrow, VerticalAlignment = VerticalAlignment.Center };
        arrow.Bind(TextBlock.ForegroundProperty, arrow.GetResourceObservable("SystemControlHighlightAccentBrush"));
        if (string.IsNullOrEmpty(description))
        {
            return new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { arrow, label } };
        }

        TextBlock descriptionText = new()
        {
            Text = description, FontSize = DescriptionFontSize, TextWrapping = Avalonia.Media.TextWrapping.Wrap,
        };
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { arrow, new StackPanel { Spacing = 2, Children = { label, descriptionText } } },
        };
    }
}

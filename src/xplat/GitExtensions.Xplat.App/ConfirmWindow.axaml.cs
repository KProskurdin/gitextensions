using Avalonia.Controls;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Asks before an action that cannot be undone. Closes with true only when the confirm button is pressed.
/// </summary>
public partial class ConfirmWindow : Window
{
    public ConfirmWindow(string message, string confirmText)
    {
        InitializeComponent();
        MessageText.Text = message;
        ConfirmButton.Content = confirmText;
        CancelButton.Click += (_, _) => Close(false);
        ConfirmButton.Click += (_, _) => Close(true);
    }
}

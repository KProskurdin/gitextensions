using Avalonia.Controls;
using GitExtensions.Xplat.Core.Settings;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Asks before an action that cannot be undone. Closes with true only when the confirm button is pressed.
/// </summary>
public partial class ConfirmWindow : Window
{
    public ConfirmWindow(string message, string confirmText, string? caption = null, bool offerDontShowAgain = false)
    {
        InitializeComponent();
        MessageText.Text = message;
        ConfirmButton.Content = confirmText;
        if (caption is not null)
        {
            Title = caption;
        }

        DontShowAgainCheck.Content = Confirmations.DontShowAgain;
        DontShowAgainCheck.IsVisible = offerDontShowAgain;
        CancelButton.Click += (_, _) => Close(false);
        ConfirmButton.Click += (_, _) => Close(true);
    }

    /// <summary>
    ///  True when the user ticked "Don't show me this message again".
    /// </summary>
    public bool DontShowAgain => DontShowAgainCheck.IsChecked == true;

    /// <summary>
    ///  Asks before <paramref name="confirmation"/>'s action, as upstream's <c>MessageBoxes.ConfirmSuppressible</c>: true at
    ///  once when the question is turned off; the check box turns it off whatever the answer. The left panel checkout is
    ///  upstream's plain question, without the check box.
    /// </summary>
    public static async Task<bool> AskAsync(Window owner, IAppPreferences preferences, Confirmation confirmation,
        string message, string confirmText, string caption)
    {
        if (!preferences.Asks(confirmation))
        {
            return true;
        }

        ConfirmWindow window = new(message, confirmText, caption,
            offerDontShowAgain: confirmation != Confirmation.BranchCheckout);
        bool confirmed = await window.ShowDialog<bool>(owner);
        if (window.DontShowAgain)
        {
            preferences.SetAsks(confirmation, false);
            preferences.Save();
        }

        return confirmed;
    }
}

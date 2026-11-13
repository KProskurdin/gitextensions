using Avalonia.Controls;
using GitExtensions.Xplat.Core.Presentation;
using GitExtensions.Xplat.Core.Settings;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Asks before an action that cannot be undone, bound to <see cref="ConfirmViewModel"/>. Closes with true only when the
///  confirm button is pressed.
/// </summary>
public partial class ConfirmWindow : Window
{
    /// <summary>
    ///  The design-time constructor (<see cref="DesignTime"/>): the window's XAML only.
    /// </summary>
    [Obsolete(DesignTime.ConstructorOnly)]
#pragma warning disable CS8618 // The fields the window's data fills stay unset: no code of the window runs.
    public ConfirmWindow() => InitializeComponent();
#pragma warning restore CS8618

    public ConfirmWindow(string message, string confirmText, string? caption = null, bool offerDontShowAgain = false,
        string? cancelText = null, string? dontShowAgainText = null)
    {
        ViewModel = new ConfirmViewModel(new WindowDialogHost(this), message, confirmText, caption, offerDontShowAgain,
            cancelText, dontShowAgainText);
        DataContext = ViewModel;
        InitializeComponent();
    }

    public ConfirmViewModel ViewModel { get; }

    /// <summary>
    ///  True when the user ticked "Don't show me this message again".
    /// </summary>
    public bool DontShowAgain => ViewModel.DontShowAgain;

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

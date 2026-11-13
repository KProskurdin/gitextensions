using Avalonia.Controls;
using GitExtensions.Xplat.Core.Operations;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Asks the user what git or ssh asked while talking to a remote: the new shell's version of upstream's native
///  <c>GitExtSshAskPass</c>, bound to <see cref="AskPassViewModel"/>. The app shows only this window when git starts it as
///  the askpass program (see <see cref="GitAskPass"/>); the answer goes to standard output, where git or ssh reads it.
/// </summary>
public partial class AskPassWindow : Window
{
    // The character a password or passphrase is hidden behind.
    private const char SecretChar = '●';

    /// <summary>
    ///  The design-time constructor (<see cref="DesignTime"/>): the window's XAML only.
    /// </summary>
    [Obsolete(DesignTime.ConstructorOnly)]
#pragma warning disable CS8618 // The fields the window's data fills stay unset: no code of the window runs.
    public AskPassWindow() => InitializeComponent();
#pragma warning restore CS8618

    public AskPassWindow(string prompt)
    {
        ViewModel = new AskPassViewModel(new WindowDialogHost(this), prompt);
        DataContext = ViewModel;
        InitializeComponent();
        if (ViewModel.IsSecret)
        {
            AnswerBox.PasswordChar = SecretChar;
        }

        Opened += (_, _) => UiActions.Run(OnOpenedAsync, _ => Close());
    }

    public AskPassViewModel ViewModel { get; }

    /// <summary>
    ///  The typed answer, or null when the window was cancelled or closed; git then stops the operation.
    /// </summary>
    public string? Answer => ViewModel.Answer;

    private async Task OnOpenedAsync()
    {
        AnswerBox.Focus();

        // The screenshot aid shows the prompt and cancels it, so a script can check that the app is reached.
        if (Screenshot.RequestedFile is { } screenshot)
        {
            await Screenshot.SaveAsync(this, screenshot);
            Close();
        }
    }
}

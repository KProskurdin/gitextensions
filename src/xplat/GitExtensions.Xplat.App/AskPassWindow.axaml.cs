using Avalonia.Controls;
using GitExtensions.Xplat.Core.Operations;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Asks the user what git or ssh asked while talking to a remote: the new shell's version of upstream's native
///  <c>GitExtSshAskPass</c>. The app shows only this window when git starts it as the askpass program
///  (see <see cref="GitAskPass"/>); the answer goes to standard output, where git or ssh reads it.
/// </summary>
public partial class AskPassWindow : Window
{
    public AskPassWindow(string prompt)
    {
        InitializeComponent();
        PromptText.Text = prompt.Trim();
        if (GitAskPass.IsSecret(prompt))
        {
            AnswerBox.PasswordChar = '●';
        }

        OkButton.Click += (_, _) =>
        {
            Answer = AnswerBox.Text ?? "";
            Close();
        };
        CancelButton.Click += (_, _) => Close();
        Opened += (_, _) => UiActions.Run(OnOpenedAsync, _ => Close());
    }

    /// <summary>
    ///  The typed answer, or null when the window was cancelled or closed; git then stops the operation.
    /// </summary>
    public string? Answer { get; private set; }

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

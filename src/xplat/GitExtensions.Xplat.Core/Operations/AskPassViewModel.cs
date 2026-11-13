using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitExtensions.Xplat.Core.Presentation;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  The view model of the askpass window (upstream's native GitExtSshAskPass dialog): git's or ssh's prompt, one answer,
///  OK and Cancel. <see cref="Answer"/> is the typed answer after OK, null otherwise; git then stops the operation.
/// </summary>
public sealed partial class AskPassViewModel : DialogViewModel
{
    public AskPassViewModel(IDialogHost host, string prompt)
        : base(host)
    {
        Prompt = prompt.Trim();
        IsSecret = GitAskPass.IsSecret(prompt);
        Text = "";
    }

    public string Prompt { get; }

    /// <summary>
    ///  The prompt asks for a password or passphrase, so the answer is hidden.
    /// </summary>
    public bool IsSecret { get; }

    /// <summary>
    ///  What is typed.
    /// </summary>
    [ObservableProperty]
    public partial string Text { get; set; }

    /// <summary>
    ///  The typed answer, or null when the window was cancelled or closed.
    /// </summary>
    public string? Answer { get; private set; }

    [RelayCommand]
    private void Ok()
    {
        Answer = Text;
        Host.Close(null);
    }

    [RelayCommand]
    private void Cancel() => Host.Close(null);
}

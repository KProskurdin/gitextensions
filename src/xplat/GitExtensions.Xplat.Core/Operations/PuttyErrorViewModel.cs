using CommunityToolkit.Mvvm.Input;
using GitExtensions.Xplat.Core.Presentation;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  The view model of upstream's <c>FormPuttyError</c> ("Authentication error"), which a plink failure to authenticate
///  opens: Load SSH key picks a key and gives it to Pageant, then retries with it; Retry retries without one; Cancel gives
///  up. <see cref="Answer"/> is null after Cancel or a closed window.
/// </summary>
public sealed partial class PuttyErrorViewModel : DialogViewModel
{
    /// <summary>
    ///  Upstream's form, whose texts the window takes.
    /// </summary>
    public const string Category = "FormPuttyError";

    private readonly Func<Task<string?>> _pickKey;
    private readonly Func<string, string?> _giveKeyToPageant;

    /// <param name="pickKey">Picks a PuTTY key file, or returns null (upstream's <c>BrowseForPrivateKey.Browse</c>).</param>
    /// <param name="giveKeyToPageant">Gives the key to Pageant, returning the error if any.</param>
    public PuttyErrorViewModel(IDialogHost host, Func<Task<string?>> pickKey, Func<string, string?> giveKeyToPageant)
        : base(host)
    {
        _pickKey = pickKey;
        _giveKeyToPageant = giveKeyToPageant;
    }

    /// <summary>
    ///  Retry, with the key loaded if any, or null when the window was cancelled or closed.
    /// </summary>
    public PuttyKeyAnswer? Answer { get; private set; }

    // Upstream's LoadSSHKey_Click with BrowseForPrivateKey.BrowseAndLoad: only a key Pageant took closes the window.
    [RelayCommand]
    private Task LoadSshKeyAsync() => GuardAsync(async () =>
    {
        if (await _pickKey() is not { Length: > 0 } path)
        {
            return;
        }

        if (await Task.Run(() => _giveKeyToPageant(path)) is { } error)
        {
            await Host.ShowMessageAsync(error, Host.Translate(CloneViewModel.SshKeyTitle));
            return;
        }

        Answer = new PuttyKeyAnswer(path);
        Host.Close(Answer);
    });

    [RelayCommand]
    private void Retry()
    {
        Answer = new PuttyKeyAnswer(null);
        Host.Close(Answer);
    }

    [RelayCommand]
    private void Cancel() => Host.Close(null);
}

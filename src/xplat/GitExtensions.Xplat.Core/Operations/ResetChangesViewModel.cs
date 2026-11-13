using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitExtensions.Xplat.Core.Presentation;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  Upstream's <c>FormResetChanges.ActionEnum</c>; closing the window cancels.
/// </summary>
public enum ResetChangesAction
{
    Cancel,
    Reset,
    ResetAndDelete,
}

/// <summary>
///  The view model of upstream's <c>FormResetChanges</c>: the question, "Also delete new files", and Reset or Cancel.
///  Closes its window with a <see cref="ResetChangesAction"/>.
/// </summary>
public sealed partial class ResetChangesViewModel : DialogViewModel
{
    // Upstream's default question.
    private const string DefaultMessage = "Are you sure you want to reset your changes?";

    /// <param name="hasExistingFiles">Whether changed tracked files are among those reset.</param>
    /// <param name="hasNewFiles">Whether new (untracked) files are among those reset.</param>
    /// <param name="confirmationMessage">Upstream's question in place of the default one, as the file lists ask it.</param>
    public ResetChangesViewModel(IDialogHost host, bool hasExistingFiles, bool hasNewFiles,
        string? confirmationMessage = null)
        : base(host)
    {
        Message = confirmationMessage ?? host.Translate(DefaultMessage);

        // As upstream: only new files means they are deleted; no new files means there is nothing to delete.
        CanChooseDeleteNewFiles = hasExistingFiles && hasNewFiles;
        DeleteNewFiles = !CanChooseDeleteNewFiles && !hasExistingFiles;
    }

    public string Message { get; }

    /// <summary>
    ///  "Also delete new files" can be changed: there are both changed and new files.
    /// </summary>
    public bool CanChooseDeleteNewFiles { get; }

    [ObservableProperty]
    public partial bool DeleteNewFiles { get; set; }

    [RelayCommand]
    private void Reset() => Host.Close(DeleteNewFiles ? ResetChangesAction.ResetAndDelete : ResetChangesAction.Reset);

    [RelayCommand]
    private void Cancel() => Host.Close(ResetChangesAction.Cancel);
}

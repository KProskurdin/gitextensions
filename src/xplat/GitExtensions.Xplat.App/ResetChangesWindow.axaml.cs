using Avalonia.Controls;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormResetChanges</c>: asks before changes are reset, and whether new files and directories go too.
/// </summary>
public partial class ResetChangesWindow : Window
{
    /// <summary>
    ///  Upstream's <c>FormResetChanges.ActionEnum</c>; closing the window cancels.
    /// </summary>
    public enum ResetAction
    {
        Cancel,
        Reset,
        ResetAndDelete
    }

    /// <param name="hasExistingFiles">Whether changed tracked files are among those reset.</param>
    /// <param name="hasNewFiles">Whether new (untracked) files are among those reset.</param>
    public ResetChangesWindow(bool hasExistingFiles, bool hasNewFiles)
    {
        InitializeComponent();

        // As upstream: only new files means they are deleted; no new files means there is nothing to delete.
        if (!hasExistingFiles || !hasNewFiles)
        {
            DeleteNewFilesCheck.IsEnabled = false;
            DeleteNewFilesCheck.IsChecked = !hasExistingFiles;
        }

        ResetButton.Click += (_, _) =>
            Close(DeleteNewFilesCheck.IsChecked == true ? ResetAction.ResetAndDelete : ResetAction.Reset);
        CancelButton.Click += (_, _) => Close(ResetAction.Cancel);
    }

    /// <summary>
    ///  Upstream's <c>FormResetChanges.ShowResetDialog</c>.
    /// </summary>
    public static async Task<ResetAction> ShowResetDialogAsync(Window owner, bool hasExistingFiles, bool hasNewFiles)
        => await new ResetChangesWindow(hasExistingFiles, hasNewFiles).ShowDialog<ResetAction?>(owner)
           ?? ResetAction.Cancel;
}

using Avalonia.Controls;
using GitCommands;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Settings;
using GitExtensions.Xplat.Ui;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;
using TaskDialogButton = System.Windows.Forms.TaskDialogButton;
using TaskDialogIcon = System.Windows.Forms.TaskDialogIcon;
using TaskDialogPage = System.Windows.Forms.TaskDialogPage;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>GitUICommands.WorktreeSwitch</c>, <c>WorktreeCreate</c> and <c>WorktreeDelete</c>, which the worktree
///  window, the toolbar's worktree drop-down and the left panel share.
/// </summary>
/// <param name="openRepository">Upstream's <c>FormBrowse.SetWorkingDir</c>: opens a folder in the browse window.</param>
public sealed class WorktreeFlow(
    RepositoryOperationsViewModel actions,
    IAppPreferences preferences,
    Func<string, Task> openRepository)
{
    /// <summary>
    ///  Upstream's <c>WorktreeSwitch</c>: after the question (unless turned off), opens the worktree in the browse window
    ///  when its folder exists. True when it was opened.
    /// </summary>
    public async Task<bool> SwitchAsync(Window owner, string worktreePath)
    {
        if (!await ConfirmWindow.AskAsync(owner, preferences, Confirmation.SwitchWorktree,
                string.Format(UpstreamTranslation.ByText(Confirmations.SwitchWorktreeQuestion), worktreePath), "Open",
                UpstreamTranslation.ByText(ManageWorktreeDialog.SwitchCaption)))
        {
            return false;
        }

        if (!Directory.Exists(worktreePath))
        {
            return false;
        }

        await openRepository(Path.GetFullPath(worktreePath));
        return true;
    }

    /// <summary>
    ///  Upstream's <c>WorktreeCreate</c>: the create window, then the offer to open the new worktree. True when one was
    ///  created.
    /// </summary>
    public async Task<bool> CreateAsync(Window owner, string repositoryPath, string mainWorktreePath)
    {
        if (await new CreateWorktreeWindow(repositoryPath, mainWorktreePath, actions, preferences)
                .ShowDialog<string?>(owner) is not { } directory)
        {
            return false;
        }

        await SwitchAsync(owner, directory);
        return true;
    }

    /// <summary>
    ///  Upstream's <c>WorktreeDelete</c>: after the question, deletes the worktree's folder and prunes, so git forgets it.
    ///  True when it was deleted.
    /// </summary>
    public async Task<bool> DeleteAsync(Window owner, string repositoryPath, string worktreePath)
    {
        MessageBoxWindow question = new(new TaskDialogPage
        {
            Text = string.Format(UpstreamTranslation.ByText(ManageWorktreeDialog.DeleteQuestion), worktreePath),
            Caption = UpstreamTranslation.ByText(ManageWorktreeDialog.DeleteCaption),
            Heading = UpstreamTranslation.ByText(ManageWorktreeDialog.CannotBeUndone),
            Buttons = { TaskDialogButton.Yes, TaskDialogButton.No },
            Icon = TaskDialogIcon.Warning,
            SizeToContent = true,
        });
        await question.ShowDialog(owner);
        if (question.ChosenButton != TaskDialogButton.Yes)
        {
            return false;
        }

        (bool deleted, string? errorMessage) = await Task.Run(() =>
            (worktreePath.TryDeleteDirectory(out string? error), error));
        if (!deleted)
        {
            await new MessageBoxWindow(
                $"{string.Format(UpstreamTranslation.ByText(ManageWorktreeDialog.DeleteFailed), worktreePath)}\n{errorMessage}",
                UpstreamTranslation.ByText(ManageWorktreeDialog.Error), MessageBoxButtons.OK).ShowDialog(owner);
            return false;
        }

        await PruneAsync(repositoryPath);
        return true;
    }

    /// <summary>
    ///  Upstream's <c>worktree prune</c>: git forgets the worktrees whose folders are gone.
    /// </summary>
    public Task<bool> PruneAsync(string repositoryPath) => actions.PruneWorktreesAsync(repositoryPath);
}

using Avalonia.Controls;
using GitCommands;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;
using GitExtensions.Xplat.Ui;
using TaskDialogButton = System.Windows.Forms.TaskDialogButton;
using TaskDialogIcon = System.Windows.Forms.TaskDialogIcon;
using TaskDialogPage = System.Windows.Forms.TaskDialogPage;
using TaskDialogVerificationCheckBox = System.Windows.Forms.TaskDialogVerificationCheckBox;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's auto stash around a pull (core <see cref="PullAutoStash"/>) for a repository: the stash and pop run as the
///  stash window's do, and the question is upstream's task dialog.
/// </summary>
public static class PullAutoStashFlow
{
    /// <summary>
    ///  Runs <paramref name="request"/> with upstream's auto stash, when it asks for it.
    /// </summary>
    /// <param name="owner">The window the question belongs to.</param>
    public static Task<bool> PullAsync(RepositoryOperationsViewModel actions, IAppPreferences preferences, string path,
        PullRequest request, Func<Window> owner)
        => new PullAutoStash(preferences, new PullAutoStashServices
        {
            HasLocalChanges = () => Task.Run(() =>
            {
                GitModule module = GitModules.Open(path);
                return !module.IsBareRepository() &&
                       module.GitStatus(UntrackedFilesMode.No, IgnoreSubmodulesMode.All).Count > 0;
            }),
            Stash = includeUntracked => actions.StashAsync(path, "", includeUntracked, keepIndex: false),
            IsInTheMiddleOfAction = () => Task.Run(() => GitModules.Open(path).InTheMiddleOfAction()),
            AskPop = () => AskPopAsync(owner()),
            Pop = () => actions.PopStashAsync(path, "stash@{0}"),
        }).PullAsync(request, pulled => actions.PullAsync(path, pulled));

    // Upstream's PopStash question, with the texts its checkout question shares.
    private static async Task<StashPopAnswer> AskPopAsync(Window owner)
    {
        TaskDialogPage page = new()
        {
            Text = UpstreamTranslation.ByText(CheckoutBranchDialog.ApplyStashedItemsAgain),
            Caption = UpstreamTranslation.ByText(CheckoutBranchDialog.ApplyStashedItemsAgainCaption),
            Icon = TaskDialogIcon.Information,
            Buttons = { TaskDialogButton.Yes, TaskDialogButton.No },
            Verification =
                new TaskDialogVerificationCheckBox { Text = UpstreamTranslation.ByText(Confirmations.DontShowAgain) },
            AllowCancel = true,
            SizeToContent = true,
        };
        MessageBoxWindow box = new(page);
        await box.ShowDialog(owner);
        return new StashPopAnswer(box.ChosenButton == TaskDialogButton.Yes,
            page.Verification.Checked && box.ChosenButton != TaskDialogButton.Cancel);
    }
}

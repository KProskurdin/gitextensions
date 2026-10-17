using Avalonia.Controls;
using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;
using GitExtensions.Xplat.Ui;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;
using TaskDialogButton = System.Windows.Forms.TaskDialogButton;
using TaskDialogIcon = System.Windows.Forms.TaskDialogIcon;
using TaskDialogPage = System.Windows.Forms.TaskDialogPage;
using TaskDialogVerificationCheckBox = System.Windows.Forms.TaskDialogVerificationCheckBox;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>StartCheckoutBranch</c> with <c>FormCheckoutBranch.DoDefaultActionOrShow</c> and <c>PerformCheckout</c>:
///  a local branch is checked out at once when the window is not needed, else the checkout window asks; then the reset
///  warning, the default local-changes action, the stash around the checkout and the question whether to pop it, the
///  checkout scripts and the submodule update.
/// </summary>
/// <param name="withScripts">Runs the checkout between the before and after checkout scripts (and plugin events).</param>
/// <param name="updateSubmodules">Upstream's <c>UpdateSubmodules</c> after the checkout moved HEAD.</param>
internal sealed class CheckoutBranchFlow(
    RepositoryOperationsViewModel actions,
    IAppPreferences preferences,
    Func<string, Func<Task<bool>>, Task<bool>> withScripts,
    Func<string, Task<bool>> updateSubmodules)
{
    /// <summary>
    ///  Checks out <paramref name="branch"/> (a local branch, or a remote one when <paramref name="remote"/>), with the
    ///  window when upstream shows it. True when the checkout happened.
    /// </summary>
    /// <param name="containObjectIds">Upstream's <c>containObjectIds</c>: only the branches that contain these commits.</param>
    public async Task<bool> StartAsync(Window owner, string path, string branch = "", bool remote = false,
        IReadOnlyList<ObjectId>? containObjectIds = null)
    {
        (GitModule module, CheckoutBranchSource source, CheckoutBranchDialog dialog) =
            await LoadAsync(path, branch, remote, containObjectIds);
        if (dialog.ChecksOutWithoutWindow(preferences.AlwaysShowCheckoutBranchDlg,
                preferences.UseDefaultCheckoutBranchAction))
        {
            return await PerformAsync(owner, path, module, source, dialog, shown: false);
        }

        return await NewWindow(path, module, source, dialog).ShowDialog<bool>(owner);
    }

    /// <summary>
    ///  The checkout window on <paramref name="path"/>, not shown (for the screenshot aid).
    /// </summary>
    public async Task<CheckoutBranchWindow> NewWindowAsync(string path, string branch = "", bool remote = false)
    {
        (GitModule module, CheckoutBranchSource source, CheckoutBranchDialog dialog) =
            await LoadAsync(path, branch, remote);
        return NewWindow(path, module, source, dialog);
    }

    private async Task<(GitModule Module, CheckoutBranchSource Source, CheckoutBranchDialog Dialog)> LoadAsync(
        string path,
        string branch, bool remote, IReadOnlyList<ObjectId>? containObjectIds = null)
    {
        GitModule module = GitModules.Open(path);
        bool checkDirty = preferences.CheckForUncommittedChangesInCheckoutBranch;
        CheckoutBranchSource source = await Task.Run(() => new CheckoutBranchSource(
            [.. module.GetRefs(RefsFilter.Heads).Select(head => head.Name)],
            [.. module.GetRefs(RefsFilter.Remotes).Select(head => head.Name)],
            module.GetRemoteNames(),
            module.GetLocalTrackingBranchName,
            checkDirty ? module.IsDirtyDir() : null,
            containObjectIds is null ? null : Containing(containObjectIds, local: true),
            containObjectIds is null ? null : Containing(containObjectIds, local: false)));
        return (module, source, new CheckoutBranchDialog(source, preferences.CheckoutBranchAction,
            preferences.CreateLocalBranchForRemote, branch, remote));

        IReadOnlyList<string> Containing(IReadOnlyList<ObjectId> ids, bool local)
            => CheckoutBranchDialog.ContainingBranches(ids, id => module.GetAllBranchesWhichContainGivenCommit(id,
                getLocal: local, getRemote: !local, cancellationToken: default));
    }

    private CheckoutBranchWindow NewWindow(string path, GitModule module, CheckoutBranchSource source,
        CheckoutBranchDialog dialog)
        => new(dialog, module, owner => PerformAsync(owner, path, module, source, dialog, shown: true))
        {
            NormaliseName = name =>
                BranchNames.Normalise(name, preferences.AutoNormaliseBranchName, preferences.AutoNormaliseSymbol),
        };

    private async Task<bool> PerformAsync(Window owner, string path, GitModule module, CheckoutBranchSource source,
        CheckoutBranchDialog dialog, bool shown)
    {
        if (!await ConfirmResetAsync(owner, module, dialog))
        {
            return false;
        }

        if (dialog.NewDefaultAction is { } newDefault)
        {
            preferences.CheckoutBranchAction = newDefault;
            preferences.Save();
        }

        LocalChangesAction localChanges = dialog.EffectiveChanges(shown, preferences.UseDefaultCheckoutBranchAction);
        bool stash = false;
        if (localChanges == LocalChangesAction.Stash)
        {
            bool? dirty = source.IsDirty;
            if (dirty is null && shown)
            {
                dirty = await Task.Run(module.IsDirtyDir);
            }

            stash = dirty == true;
            if (stash && !await actions.StashAsync(path, "", preferences.IncludeUntrackedFilesInAutoStash,
                    keepIndex: false))
            {
                return false;
            }
        }

        ObjectId original = await Task.Run(module.GetCurrentCheckout);
        string branch = dialog.Branch.Trim();
        if (!await withScripts(path, () => actions.CheckoutBranchAsync(path, branch, dialog.Arguments(localChanges))))
        {
            return false;
        }

        if (stash && await AskPopStashAsync(owner))
        {
            await actions.PopStashAsync(path, "stash@{0}");
        }

        if (await Task.Run(module.GetCurrentCheckout) != original)
        {
            await updateSubmodules(path);
        }

        return true;
    }

    // Upstream: resetting a local branch to a remote one that does not contain it loses commits, so it asks first.
    private static async Task<bool> ConfirmResetAsync(Window owner, GitModule module, CheckoutBranchDialog dialog)
    {
        if (!dialog.IsRemote || dialog.NewBranchMode != CheckoutNewBranchMode.Reset ||
            dialog.LocalBranchName.Length == 0)
        {
            return true;
        }

        string? warning = await Task.Run(() =>
        {
            ObjectId local = module.RevParse(GitRefName.RefsHeadsPrefix + dialog.LocalBranchName);
            ObjectId remote = module.RevParse(GitRefName.RefsRemotesPrefix + dialog.Branch.Trim());
            ObjectId mergeBase = local.IsZero || remote.IsZero ? default : module.GetMergeBase(local, remote);
            return dialog.ResetWarning(local, remote, mergeBase, UpstreamTranslation.ByText);
        });
        if (warning is null)
        {
            return true;
        }

        MessageBoxWindow question = new(warning, UpstreamTranslation.ByText(CheckoutBranchDialog.ResetCaption),
            MessageBoxButtons.YesNo);
        await question.ShowDialog(owner);
        return question.Result == System.Windows.Forms.DialogResult.Yes;
    }

    // Upstream's AutoPopStashAfterCheckoutBranch question; dismissing the dialog is not an answer to remember.
    private async Task<bool> AskPopStashAsync(Window owner)
    {
        if (preferences.AutoPopStashAfterCheckoutBranch is { } remembered)
        {
            return remembered;
        }

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
        bool pop = box.ChosenButton == TaskDialogButton.Yes;
        if (page.Verification.Checked && box.ChosenButton != TaskDialogButton.Cancel)
        {
            preferences.AutoPopStashAfterCheckoutBranch = pop;
            preferences.Save();
        }

        return pop;
    }
}

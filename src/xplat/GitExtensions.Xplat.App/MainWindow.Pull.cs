using GitCommands;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Platform;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Ui;
using DialogResult = System.Windows.Forms.DialogResult;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;
using TaskDialogButton = System.Windows.Forms.TaskDialogButton;
using TaskDialogPage = System.Windows.Forms.TaskDialogPage;

namespace GitExtensions.Xplat.App;

public partial class MainWindow
{
    // git's argument for every remote, which upstream's pull passes for "[ All ]".
    private const string AllRemotesArgument = "--all";

    // Upstream's FormPull.PullChanges, which every pull runs through (the dialog, the toolbar's and the hotkeys' quick
    // pulls, the left panel's fetch and merge or rebase): after a pull that did not fail, the submodules; after one that
    // failed from <paramref name="remote"/>, upstream's prune offer.
    private async Task<bool> PullAndUpdateSubmodulesAsync(string path, Func<Task<bool>> pull, string? remote = null)
    {
        bool done = await pull();
        if (done)
        {
            await UpdateSubmodulesAfterPullAsync(path);
        }
        else if (remote is not null)
        {
            await OfferPruneAfterRejectedPullAsync(path, remote);
        }

        return done;
    }

    // Upstream's HandlePullOnExit: a pull from a remote that failed because the branch's remote ref no longer exists
    // offers to delete the remote's stale remote-tracking branches (Yes, No, Cancel; No by default).
    private async Task OfferPruneAfterRejectedPullAsync(string path, string remote)
    {
        if (!_repository.Remotes.Contains(remote) || !PullDialog.IsRefRemoved(_actions.OutputLines))
        {
            return;
        }

        MessageBoxWindow question = new(new TaskDialogPage
        {
            Text = UpstreamTranslation.ByText(PullDialog.PruneBranchesBranch),
            Caption = UpstreamTranslation.ByText(PullDialog.PruneBranchesCaption),
            Heading = UpstreamTranslation.ByText(PullDialog.PruneBranchesMainInstruction),
            Buttons = { TaskDialogButton.Yes, TaskDialogButton.No, TaskDialogButton.Cancel },
            DefaultButton = TaskDialogButton.No,
        });
        await question.ShowDialog(this);
        if (question.ChosenButton == TaskDialogButton.Yes)
        {
            await _actions.RunProgramAsync(string.Format(UpstreamTranslation.ByText(PullDialog.PruneFromCaption), remote),
                AppSettings.GitCommand, PullDialog.PruneArguments(remote), path);
        }
    }

    // Upstream's InitModules: submodules not initialized yet are initialized and updated as Settings > General's "Update
    // submodules on checkout" says, else after its question; otherwise upstream's UpdateSubmodules (as after a checkout).
    private async Task UpdateSubmodulesAfterPullAsync(string path)
    {
        if (!await Task.Run(() => PullDialog.HasUninitializedSubmodules(GitModules.Open(path))))
        {
            await UpdateSubmodulesAfterCheckoutAsync(path);
            return;
        }

        bool initialize = _preferences.UpdateSubmodulesOnCheckout
                          ?? AppSettings.DontConfirmUpdateSubmodulesOnCheckout
                          ?? await AskInitializeSubmodulesAsync();
        if (initialize)
        {
            await UpdateSubmodulesAsync(path, submodulePath: null);
        }
    }

    // Upstream's LoadPuttyKey (FormPull) and StartPageant (FormPush): with plink, the PuTTY keys of the remote (every
    // remote for "--all"; none for a URL) are given to Pageant before the command, which an error stops, as upstream's
    // exception does.
    private async Task<bool> LoadPuttyKeysAsync(string path, string remote)
    {
        if (!GitSshHelpers.IsPlink)
        {
            return true;
        }

        IReadOnlyList<string> remotes = remote == AllRemotesArgument ? [.. _repository.Remotes]
            : _repository.Remotes.Contains(remote) ? [remote]
            : [];
        if (await Task.Run(() => Putty.LoadKeys(GitModules.Open(path), remotes, UpstreamTranslation.ByText)) is not
            { } error)
        {
            return true;
        }

        await new ErrorWindow(error).ShowDialog(this);
        return false;
    }

    // Upstream's AskIfSubmodulesShouldBeInitialized.
    private async Task<bool> AskInitializeSubmodulesAsync()
    {
        MessageBoxWindow question = new(UpstreamTranslation.ByText(PullDialog.InitSubmodulesQuestion),
            UpstreamTranslation.ByText(PullDialog.SubmodulesCaption), MessageBoxButtons.YesNo);
        await question.ShowDialog(this);
        return question.Result == DialogResult.Yes;
    }
}

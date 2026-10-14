using Avalonia.Controls;
using GitCommands.Git;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Settings;
using GitExtensions.Xplat.Ui;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;
using TaskDialogButton = System.Windows.Forms.TaskDialogButton;
using TaskDialogCommandLinkButton = System.Windows.Forms.TaskDialogCommandLinkButton;
using TaskDialogIcon = System.Windows.Forms.TaskDialogIcon;
using TaskDialogPage = System.Windows.Forms.TaskDialogPage;
using TaskDialogVerificationCheckBox = System.Windows.Forms.TaskDialogVerificationCheckBox;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Where a push goes and from which branch, for upstream's handling of a rejected push.
/// </summary>
/// <param name="PushedBranch">The branch chosen in the push dialog; the current branch for a quick push.</param>
internal sealed record PushOrigin(
    string PushedBranch,
    string CurrentBranch,
    string? CurrentBranchRemote,
    bool ToUrl = false,
    bool IsBare = false);

/// <summary>
///  Upstream's <c>FormPush.HandlePushOnExit</c> in the new shell: a push the remote rejected is offered a pull (with the
///  default action, rebase or merge) or a force push with lease, as upstream's task dialog over the process window, and is
///  then pushed again. The choice can be kept (<see cref="IAppPreferences.AutoPullOnPushRejectedAction"/>).
/// </summary>
internal sealed class PushRejectedFlow(RepositoryOperationsViewModel actions, IAppPreferences preferences)
{
    /// <summary>
    ///  Pushes <paramref name="request"/> and handles a rejection as upstream does; true when the push finally succeeded.
    /// </summary>
    /// <param name="owner">The window the questions belong to; the process window shows git's output over it.</param>
    public async Task<bool> PushAsync(Func<Window> owner, string repositoryPath, PushRequest request, PushOrigin origin)
    {
        bool pushed = await actions.PushAsync(repositoryPath, request);
        while (!pushed && PushRejection.Find(actions.OutputLines, origin.PushedBranch, origin.CurrentBranch,
                   origin.ToUrl, origin.IsBare, request.Kind) is { } currentBranchRejected)
        {
            (GitPullAction pullAction, bool forcePush) = await AskAsync(owner(), request.Remote, currentBranchRejected);
            if (forcePush)
            {
                request = PushRejection.WithForceWithLease(request);
            }
            else
            {
                PullAction? pull = PushRejection.PullFor(pullAction, preferences.DefaultPullAction, out string? error);
                string remoteBranch = request.RemoteBranch.Length > 0 ? request.RemoteBranch : request.LocalBranch;
                if (error is null && pull is not null &&
                    PushRejection.ChecksRebasedMergeCommits(preferences.DefaultPullAction, request, origin.ToUrl,
                        origin.PushedBranch, origin.CurrentBranch, origin.CurrentBranchRemote) &&
                    await actions.RebasesMergeCommitAsync(repositoryPath, request.Remote, remoteBranch,
                        origin.CurrentBranch))
                {
                    error = PushRejection.RebasesMergeCommit;
                }

                if (error is not null)
                {
                    await new MessageBoxWindow(error, "Error", MessageBoxButtons.OK).ShowDialog(owner());
                    return false;
                }

                if (pull is not { } action ||
                    !await actions.PullAsync(repositoryPath,
                        new PullRequest(request.Remote, remoteBranch, action, Prune: false,
                            AutoStash: preferences.AutoStash, FetchTags: null)))
                {
                    return false;
                }
            }

            pushed = await actions.PushAsync(repositoryPath, request);
        }

        return pushed;
    }

    // Upstream's AskForAutoPullOnPushRejectedAction: the kept choice, else the question with its command links.
    private async Task<(GitPullAction PullAction, bool ForcePush)> AskAsync(Window owner, string destination,
        bool allOptions)
    {
        if (preferences.AutoPullOnPushRejectedAction is { } kept)
        {
            return (kept, false);
        }

        TaskDialogCommandLinkButton pullDefault = new(string.Format(
            UpstreamTranslation.ByText(PushRejection.PullDefaultButton),
            UpstreamTranslation.ByText(PushRejection.DefaultButtonAction(preferences.DefaultPullAction))));
        TaskDialogCommandLinkButton pullRebase = new(UpstreamTranslation.ByText(PushRejection.PullRebaseButton));
        TaskDialogCommandLinkButton pullMerge = new(UpstreamTranslation.ByText(PushRejection.PullMergeButton));
        TaskDialogCommandLinkButton pushForce = new(UpstreamTranslation.ByText(PushRejection.PushForceButton));
        TaskDialogPage page = new()
        {
            Text = UpstreamTranslation.ByText(allOptions ? PushRejection.MergeText : PushRejection.ForceText),
            Heading = UpstreamTranslation.ByText(allOptions ? PushRejection.MergeHeading : PushRejection.ForceHeading),
            Caption = string.Format(UpstreamTranslation.ByText(PushRejection.Caption), destination),
            Buttons = { TaskDialogButton.Cancel },
            Icon = TaskDialogIcon.Error,
            Verification = new TaskDialogVerificationCheckBox
            {
                Text = UpstreamTranslation.ByText(Confirmations.DontShowAgain),
            },
            AllowCancel = true,
            SizeToContent = true,
        };
        if (allOptions)
        {
            page.Buttons.Add(pullDefault);
            page.Buttons.Add(pullRebase);
            page.Buttons.Add(pullMerge);
        }

        page.Buttons.Add(pushForce);

        MessageBoxWindow box = new(page);
        await box.ShowDialog(owner);
        TaskDialogButton result = box.ChosenButton;
        bool forcePush = result == pushForce;
        GitPullAction? pullAction = result == pullDefault ? GitPullAction.Default
            : result == pullRebase ? GitPullAction.Rebase
            : result == pullMerge ? GitPullAction.Merge
            : forcePush ? null
            : GitPullAction.None;

        // As upstream: a kept force push stores no action, so the question comes again next time.
        if (page.Verification.Checked)
        {
            preferences.AutoPullOnPushRejectedAction = pullAction;
        }

        return (pullAction ?? GitPullAction.None, forcePush);
    }
}
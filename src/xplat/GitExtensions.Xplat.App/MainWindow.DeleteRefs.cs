using Avalonia.Controls;
using GitCommands;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Ui;
using GitUI.ScriptsEngine;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;
using TaskDialogButton = System.Windows.Forms.TaskDialogButton;
using TaskDialogIcon = System.Windows.Forms.TaskDialogIcon;
using TaskDialogPage = System.Windows.Forms.TaskDialogPage;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's Commands > Delete branch... and Delete tag... (<c>StartDeleteBranchDialog</c>, <c>StartDeleteTagDialog</c>):
///  upstream's FormDeleteBranch with its worktree handling, and FormDeleteTag with the push scripts around the remote's
///  delete.
/// </summary>
public partial class MainWindow
{
    private void WireDeleteRefs()
    {
        DeleteBranchMenuItem.Click += (_, _) => RunOnRepository(path => ShowDeleteBranchAsync(path, []));
        DeleteTagMenuItem.Click += (_, _) => RunOnRepository(path => ShowDeleteTagAsync(path, tag: null));
    }

    private async Task<bool> ShowDeleteBranchAsync(string path, IReadOnlyList<string> defaultBranches,
        Window? owner = null)
    {
        string? current = _repository.CurrentBranch.Length > 0 ? _repository.CurrentBranch : null;
        IReadOnlyList<string> branches =
            [.. _repository.Branches.Where(branch => !branch.IsRemote).Select(branch => branch.Name)];
        return await new DeleteBranchWindow(branches, current, defaultBranches,
                (window, selected) => DeleteBranchesAsync(window, path, selected))
            .ShowDialog<bool>(owner ?? this);
    }

    // Upstream's Delete_Click after its checks: the worktrees holding a branch (HandleWorktreeBranches), then the unmerged
    // question and branch -D.
    private async Task<bool> DeleteBranchesAsync(Window owner, string path, IReadOnlyList<string> branches)
    {
        IReadOnlyList<WorktreeInfo> worktrees = await _repositoryService.GetWorktreesAsync(path);
        if (worktrees.Count > 1)
        {
            branches = await HandleWorktreeBranchesAsync(owner, path, branches, worktrees);
            if (branches.Count == 0)
            {
                return false;
            }
        }

        return await DeleteLocalBranchesAsync(path, branches, owner);
    }

    // Upstream's HandleWorktreeBranches: stale worktrees are pruned, a branch of the main worktree is refused, and those of
    // linked worktrees are deleted with their worktrees after upstream's question. The branches left to delete.
    private async Task<IReadOnlyList<string>> HandleWorktreeBranchesAsync(Window owner, string path,
        IReadOnlyList<string> branches, IReadOnlyList<WorktreeInfo> worktrees)
    {
        WorktreeBranches held = DeleteBranchDialog.ClassifyWorktreeBranches(branches, worktrees, path);
        if (held.IsEmpty)
        {
            return branches;
        }

        WorktreeFlow flow = WorktreeFlow();
        if (held.HasDeletedWorktrees)
        {
            await flow.PruneAsync(path);
        }

        string caption = UpstreamTranslation.ByText(DeleteBranchDialog.DeleteBranchCaption);
        HashSet<string> excluded = [];
        if (held.MainWorktreeBranches is [(string mainBranch, WorktreeInfo main), ..])
        {
            string message = string.Format(
                UpstreamTranslation.ByText(DeleteBranchDialog.CannotDeleteBranchInMainWorktree), mainBranch, main.Path);
            await new MessageBoxWindow(message, caption, MessageBoxButtons.OK).ShowDialog(owner);
            excluded.Add(mainBranch);
        }

        if (held.LinkedWorktreeBranches.Count > 0)
        {
            string list = string.Join("\n",
                held.LinkedWorktreeBranches.Select(item => $"• {item.Branch}  →  {item.Worktree.Path}"));
            MessageBoxWindow question = new(new TaskDialogPage
            {
                Text = string.Format(UpstreamTranslation.ByText(DeleteBranchDialog.BranchUsedByWorktreeQuestion), list),
                Caption = caption,
                Heading = UpstreamTranslation.ByText(ManageWorktreeDialog.CannotBeUndone),
                Icon = TaskDialogIcon.Warning,
                Buttons = { TaskDialogButton.Yes, TaskDialogButton.No },
                DefaultButton = TaskDialogButton.No,
                AllowCancel = true,
                SizeToContent = true,
            });
            await question.ShowDialog(owner);
            if (question.ChosenButton == TaskDialogButton.Yes)
            {
                bool anyDeleted = false;
                foreach ((string branch, WorktreeInfo worktree) in held.LinkedWorktreeBranches)
                {
                    (bool deleted, string? error) = await Task.Run(() =>
                        (worktree.Path.TryDeleteDirectory(out string? message), message));
                    anyDeleted |= deleted;
                    if (!deleted)
                    {
                        await new MessageBoxWindow(
                            string.Format(UpstreamTranslation.ByText(ManageWorktreeDialog.DeleteFailed), worktree.Path)
                            + "\n" + error,
                            UpstreamTranslation.ByText(ManageWorktreeDialog.Error), MessageBoxButtons.OK)
                            .ShowDialog(owner);
                        excluded.Add(branch);
                    }
                }

                if (anyDeleted)
                {
                    await flow.PruneAsync(path);
                }
            }
            else
            {
                excluded.UnionWith(held.LinkedWorktreeBranches.Select(item => item.Branch));
            }
        }

        return [.. branches.Where(branch => !excluded.Contains(branch))];
    }

    // Upstream's StartDeleteRemoteBranchDialog: the remote branches deleted between the push scripts, then their local
    // tracking branches in the delete branch window when asked.
    private async Task<bool> ShowDeleteRemoteBranchAsync(string path, string remoteBranch)
        => await new DeleteRemoteBranchWindow(path, remoteBranch,
                (remote, branches) => WithScriptsAsync(path, ScriptEvent.BeforePush, ScriptEvent.AfterPush,
                    () => _actions.DeleteRemoteBranchesAsync(path, remote, branches)),
                (owner, tracking) => ShowDeleteBranchAsync(path, tracking, owner))
            .ShowDialog<bool>(this);

    private async Task<bool> ShowDeleteTagAsync(string path, string? tag)
        => await new DeleteTagWindow(tag, _repository.Tags, _repository.Remotes, _repository.TrackingRemote ?? "",
                (name, remote) => DeleteTagAsync(path, name, remote))
            .ShowDialog<bool>(this);

    // Upstream's OkClick: the local tag, then, when asked, the remote's between the push scripts (RemoveRemoteTag).
    private async Task DeleteTagAsync(string path, string tag, string? remote)
    {
        await _actions.DeleteTagAsync(path, tag);
        if (remote is not null)
        {
            await WithScriptsAsync(path, ScriptEvent.BeforePush, ScriptEvent.AfterPush,
                () => _actions.DeleteRemoteTagAsync(path, remote, tag));
        }
    }
}

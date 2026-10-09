using GitCommands.Git;
using GitExtensions.Extensibility.Git;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  Runs user-initiated operations and reports their outcome. Holds no UI types. Create and use it on the UI thread.
///  Every operation that changes the repository raises <see cref="RepositoryChanged"/> so a view can reload it.
/// </summary>
public sealed class RepositoryOperationsViewModel : ObservableObject
{
    private const string NoEditorMessage = "No editor is available for the rebase todo list.";

    private readonly IGitOperations _operations;
    private readonly List<GitOutputLine> _output = [];
    private readonly Lock _outputLock = new();
    private bool _isBusy;
    private string _statusMessage = "";
    private string? _errorMessage;
    private IReadOnlyList<string> _outputLines = [];
    private string _outputTitle = "";
    private RemoteOperationState _remoteState;
    private bool _keepOutputOpen;
    private CancellationTokenSource? _cancellation;

    public RepositoryOperationsViewModel(IGitOperations operations)
    {
        _operations = operations;
    }

    /// <summary>
    ///  The command git runs to edit a rebase todo list or a reworded message (see <see cref="GitEditorCommand"/>); set by the
    ///  app. Without it, interactive rebase is refused and a resumed rebase keeps each message.
    /// </summary>
    public string? EditorCommand { get; set; }

    public event EventHandler<RepositoryChangedEventArgs>? RepositoryChanged;

    /// <summary>
    ///  Raised when a remote operation (fetch, pull, push, clone) starts, so a view can show its output as it arrives.
    /// </summary>
    public event EventHandler? RemoteOperationStarted;

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    /// <summary>
    ///  Describes the last successful operation, e.g. "Committed".
    /// </summary>
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    /// <summary>
    ///  Set when an operation fails or its input is invalid. A view shows it and then calls <see cref="ClearError"/>.
    /// </summary>
    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public void ClearError() => ErrorMessage = null;

    /// <summary>
    ///  The output of the last remote operation, as a console shows it: a progress line is replaced by the line after it.
    /// </summary>
    public IReadOnlyList<string> OutputLines
    {
        get => _outputLines;
        private set => SetProperty(ref _outputLines, value);
    }

    /// <summary>
    ///  What the last remote operation does, e.g. "Push to origin".
    /// </summary>
    public string OutputTitle
    {
        get => _outputTitle;
        private set => SetProperty(ref _outputTitle, value);
    }

    public RemoteOperationState RemoteState
    {
        get => _remoteState;
        private set
        {
            if (SetProperty(ref _remoteState, value))
            {
                RaisePropertyChanged(nameof(CanCancel));
            }
        }
    }

    /// <summary>
    ///  True while a remote operation runs; <see cref="Cancel"/> then stops git.
    /// </summary>
    /// <summary>
    ///  True when the output of the last remote operation is its result, e.g. the next commit a bisect checked out, so its
    ///  window stays open after success whatever the close setting says (upstream's <c>useDialogSettings: false</c>).
    /// </summary>
    public bool KeepOutputOpen
    {
        get => _keepOutputOpen;
        private set => SetProperty(ref _keepOutputOpen, value);
    }

    public bool CanCancel => _remoteState == RemoteOperationState.Running;

    public void Cancel() => _cancellation?.Cancel();

    public Task<bool> StageAsync(string repositoryPath, IReadOnlyList<string> paths)
        => RunAsync("Staged", repositoryPath, () => _operations.StageAsync(repositoryPath, paths));

    public Task<bool> UnstageAsync(string repositoryPath, IReadOnlyList<string> paths)
        => RunAsync("Unstaged", repositoryPath, () => _operations.UnstageAsync(repositoryPath, paths));

    /// <summary>
    ///  Stages or unstages the lines selected in a file's diff (see <see cref="IGitOperations.StageLinesAsync"/>).
    /// </summary>
    public Task<bool> StageLinesAsync(string repositoryPath, string diffText, int selectionStart, int selectionLength,
        bool unstage, bool isNewFile = false)
        => RunAsync(unstage ? "Unstaged selected lines" : "Staged selected lines", repositoryPath,
            () => _operations.StageLinesAsync(repositoryPath, diffText, selectionStart, selectionLength, unstage,
                isNewFile));

    public Task<bool> CommitAsync(string repositoryPath, string message, bool amend, bool signOff, string author)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return RejectAsync("Enter a commit message.");
        }

        return RunAsync(amend ? "Amended" : "Committed", repositoryPath,
            () => _operations.CommitAsync(repositoryPath, message, amend, signOff, author.Trim()));
    }

    public Task<bool> CreateBranchAsync(string repositoryPath, string name, bool checkout, string? startPoint = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return RejectAsync("Enter a branch name.");
        }

        return RunAsync($"Created {name.Trim()}", repositoryPath,
            () => _operations.CreateBranchAsync(repositoryPath, name.Trim(), checkout, startPoint));
    }

    public Task<bool> CheckoutAsync(string repositoryPath, string branch,
        GitCommands.LocalChangesAction localChanges = GitCommands.LocalChangesAction.DontChange)
        => RunAsync($"Checked out {branch}", repositoryPath,
            () => _operations.CheckoutAsync(repositoryPath, branch, localChanges));

    /// <summary>
    ///  Checks out <paramref name="remoteBranch"/> as a local tracking branch with the same name after the remote.
    /// </summary>
    public Task<bool> CheckoutRemoteAsync(string repositoryPath, string remoteBranch)
        => RunAsync($"Checked out {remoteBranch}", repositoryPath,
            () => _operations.CheckoutRemoteAsync(repositoryPath, remoteBranch));

    public Task<bool> DeleteBranchAsync(string repositoryPath, string branch, bool force)
        => RunAsync($"Deleted {branch}", repositoryPath,
            () => _operations.DeleteBranchAsync(repositoryPath, branch, force));

    public Task<bool> DeleteRemoteBranchAsync(string repositoryPath, string remote, string branch)
        => RunAsync($"Deleted {remote}/{branch}", repositoryPath,
            () => _operations.DeleteRemoteBranchAsync(repositoryPath, remote, branch));

    public Task<bool> RenameBranchAsync(string repositoryPath, string branch, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
        {
            return RejectAsync("Enter a new branch name.");
        }

        return RunAsync($"Renamed {branch} to {newName.Trim()}", repositoryPath,
            () => _operations.RenameBranchAsync(repositoryPath, branch, newName.Trim()));
    }

    public Task<bool> FetchAsync(string repositoryPath, string remote, bool prune)
        => RunRemoteAsync($"Fetch from {remote}", "Fetched", repositoryPath,
            (output, cancellation) => _operations.FetchAsync(repositoryPath, remote, prune, output, cancellation));

    public Task<bool> PushTagsAsync(string repositoryPath, string remote)
        => RunRemoteAsync($"Push tags to {remote}", "Pushed tags", repositoryPath,
            (output, cancellation) => _operations.PushTagsAsync(repositoryPath, remote, output, cancellation));

    public Task<bool> PullAsync(string repositoryPath, string remote, string branch, bool rebase)
        => RunRemoteAsync($"Pull {branch} from {remote}", "Pulled", repositoryPath,
            (output, cancellation) =>
                _operations.PullAsync(repositoryPath, remote, branch, rebase, output, cancellation));

    public Task<bool> PushAsync(string repositoryPath, string remote, string branch)
        => RunRemoteAsync($"Push {branch} to {remote}", "Pushed", repositoryPath,
            (output, cancellation) => _operations.PushAsync(repositoryPath, remote, branch, output, cancellation));

    public Task<bool> PushAsync(string repositoryPath, PushRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Remote) || string.IsNullOrWhiteSpace(request.LocalBranch))
        {
            return RejectAsync("Choose a remote and a branch to push.");
        }

        return RunRemoteAsync($"Push {request.LocalBranch} to {request.Remote}", "Pushed", repositoryPath,
            (output, cancellation) => _operations.PushAsync(repositoryPath, request, output, cancellation));
    }

    public Task<bool> PullAsync(string repositoryPath, PullRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Remote))
        {
            return RejectAsync("Choose a remote to pull from.");
        }

        bool fetchOnly = request.Action == PullAction.FetchOnly;
        string what = request.RemoteBranch.Length == 0 ? request.Remote : $"{request.Remote}/{request.RemoteBranch}";
        return RunRemoteAsync(fetchOnly ? $"Fetch {what}" : $"Pull {what}", fetchOnly ? "Fetched" : "Pulled",
            repositoryPath,
            (output, cancellation) => _operations.PullAsync(repositoryPath, request, output, cancellation));
    }

    public Task<bool> StashAsync(string repositoryPath, string message, bool includeUntracked, bool keepIndex,
        IReadOnlyList<string>? paths = null)
        => RunAsync("Stashed", repositoryPath,
            () => _operations.StashAsync(repositoryPath, message.Trim(), includeUntracked, keepIndex, paths));

    public Task<bool> ApplyStashAsync(string repositoryPath, string stashName)
        => RunAsync("Stash applied", repositoryPath, () => _operations.ApplyStashAsync(repositoryPath, stashName));

    public Task<bool> PopStashAsync(string repositoryPath, string stashName)
        => RunAsync("Stash popped", repositoryPath, () => _operations.PopStashAsync(repositoryPath, stashName));

    public Task<bool> DropStashAsync(string repositoryPath, string stashName)
        => RunAsync("Stash dropped", repositoryPath, () => _operations.DropStashAsync(repositoryPath, stashName));

    public Task<bool> RenameRemoteAsync(string repositoryPath, string name, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
        {
            return RejectAsync("Enter the new name of the remote.");
        }

        return RunAsync($"Renamed remote {name} to {newName.Trim()}", repositoryPath,
            () => _operations.RenameRemoteAsync(repositoryPath, name, newName.Trim()));
    }

    public Task<bool> SetRemoteUrlAsync(string repositoryPath, string name, string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return RejectAsync("Enter the URL or path of the remote.");
        }

        return RunAsync($"Changed the URL of {name}", repositoryPath,
            () => _operations.SetRemoteUrlAsync(repositoryPath, name, url.Trim()));
    }

    public Task<bool> DeleteRemoteTagAsync(string repositoryPath, string remote, string name)
        => RunRemoteAsync($"Delete tag {name} on {remote}", $"Deleted tag {name} on {remote}", repositoryPath,
            (output, cancellation) =>
                _operations.DeleteRemoteTagAsync(repositoryPath, remote, name, output, cancellation));

    public Task<bool> AddWorktreeAsync(string repositoryPath, string path, string branch, string newBranch)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(branch))
        {
            return RejectAsync("Enter the folder of the new worktree and the branch to check out in it.");
        }

        return RunAsync($"Added worktree {path.Trim()}", repositoryPath,
            () => _operations.AddWorktreeAsync(repositoryPath, path.Trim(), branch.Trim(), newBranch.Trim()));
    }

    public Task<bool> RemoveWorktreeAsync(string repositoryPath, string path, bool force)
        => RunAsync($"Removed worktree {path}", repositoryPath,
            () => _operations.RemoveWorktreeAsync(repositoryPath, path, force));

    public Task<bool> PruneWorktreesAsync(string repositoryPath)
        => RunAsync("Pruned worktrees", repositoryPath, () => _operations.PruneWorktreesAsync(repositoryPath));

    public Task<bool> UpdateSubmodulesAsync(string repositoryPath, string? path)
        => RunRemoteAsync(path is null ? "Update submodules" : $"Update submodule {path}", "Submodules updated",
            repositoryPath,
            (output, cancellation) => _operations.UpdateSubmodulesAsync(repositoryPath, path, output, cancellation));

    public Task<bool> SyncSubmodulesAsync(string repositoryPath, string? path)
        => RunAsync("Submodules synchronized", repositoryPath,
            () => _operations.SyncSubmodulesAsync(repositoryPath, path));

    public Task<bool> CreateTagAsync(string repositoryPath, string name, string commit, string message)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return RejectAsync("Enter a tag name.");
        }

        return RunAsync($"Created tag {name.Trim()}", repositoryPath,
            () => _operations.CreateTagAsync(repositoryPath, name.Trim(), commit, message.Trim()));
    }

    public Task<bool> DeleteTagAsync(string repositoryPath, string name)
        => RunAsync($"Deleted tag {name}", repositoryPath, () => _operations.DeleteTagAsync(repositoryPath, name));

    public Task<bool> CherryPickAsync(string repositoryPath, string commit)
        => RunAsync($"Cherry-picked {ShortHash(commit)}", repositoryPath,
            () => _operations.CherryPickAsync(repositoryPath, commit));

    public Task<bool> ResetAsync(string repositoryPath, string commit, ResetMode mode)
        => RunAsync($"Reset to {ShortHash(commit)}", repositoryPath,
            () => _operations.ResetAsync(repositoryPath, commit, mode));

    public Task<bool> RevertAsync(string repositoryPath, string commit)
        => RunAsync($"Reverted {ShortHash(commit)}", repositoryPath,
            () => _operations.RevertAsync(repositoryPath, commit));

    public Task<bool> RebaseAsync(string repositoryPath, string branch)
        => RunAsync($"Rebased onto {branch}", repositoryPath, () => _operations.RebaseAsync(repositoryPath, branch));

    public Task<bool> AbortRebaseAsync(string repositoryPath)
        => RunAsync("Rebase aborted", repositoryPath, () => _operations.AbortRebaseAsync(repositoryPath));

    public Task<bool> ContinueRebaseAsync(string repositoryPath)
        => RunAsync("Rebase continued", repositoryPath,
            () => _operations.ContinueRebaseAsync(repositoryPath, EditorCommand));

    public Task<bool> SkipRebaseAsync(string repositoryPath)
        => RunAsync("Commit skipped", repositoryPath, () => _operations.SkipRebaseAsync(repositoryPath, EditorCommand));

    /// <summary>
    ///  Rebases the checked-out branch onto <paramref name="onto"/> interactively, showing git's output as a remote operation
    ///  does. Needs <see cref="EditorCommand"/>.
    /// </summary>
    public Task<bool> RebaseInteractiveAsync(string repositoryPath, string onto)
    {
        if (EditorCommand is not { } editor)
        {
            return RejectAsync(NoEditorMessage);
        }

        return RunRemoteAsync($"Rebase interactively onto {ShortHash(onto)}", "Rebased", repositoryPath,
            (output, cancellation) =>
                _operations.RebaseInteractiveAsync(repositoryPath, onto, editor, output, cancellation));
    }

    /// <summary>
    ///  Runs a program (a user script) with its output shown as a remote operation's is, as upstream's FormProcess runs a
    ///  script; the repository is reloaded after it, as upstream notifies a change.
    /// </summary>
    public Task<bool> RunProgramAsync(string title, string program, string arguments, string workingDirectory)
        => RunRemoteAsync(title, $"{title} finished", workingDirectory,
            (output, cancellation) => Task.Run(
                () => GitOutputRunner.RunAsync(workingDirectory, arguments, output, cancellation, program: program),
                cancellation));

    public Task<bool> EditRebaseTodoAsync(string repositoryPath)
        => EditorCommand is { } editor
            ? RunAsync("Todo list edited", repositoryPath,
                () => _operations.EditRebaseTodoAsync(repositoryPath, editor))
            : RejectAsync(NoEditorMessage);

    public Task<bool> ContinueMergeAsync(string repositoryPath)
        => RunAsync("Merge continued", repositoryPath, () => _operations.ContinueMergeAsync(repositoryPath, EditorCommand));

    public Task<bool> ContinuePatchAsync(string repositoryPath)
        => RunAsync("Patch applied", repositoryPath, () => _operations.ContinuePatchAsync(repositoryPath));

    public Task<bool> SkipPatchAsync(string repositoryPath)
        => RunAsync("Patch skipped", repositoryPath, () => _operations.SkipPatchAsync(repositoryPath));

    public Task<bool> AbortPatchAsync(string repositoryPath)
        => RunAsync("Patch aborted", repositoryPath, () => _operations.AbortPatchAsync(repositoryPath));

    /// <summary>
    ///  Starts a bisect with git's output shown as a remote operation's is, as upstream's FormBisect runs it in a process
    ///  dialog.
    /// </summary>
    public Task<bool> StartBisectAsync(string repositoryPath)
        => RunRemoteAsync("Bisect start", "Bisect started", repositoryPath,
            (output, cancellation) => _operations.StartBisectAsync(repositoryPath, output, cancellation));

    /// <summary>
    ///  Marks <paramref name="commit"/> (the checked-out commit when null) and shows which commit git checks out next, or the
    ///  first bad commit; that output stays on screen (<see cref="KeepOutputOpen"/>).
    /// </summary>
    public Task<bool> MarkBisectAsync(string repositoryPath, GitBisectOption option, string? commit = null)
    {
        string mark = option switch
        {
            GitBisectOption.Good => "good",
            GitBisectOption.Bad => "bad",
            _ => "skipped",
        };
        string what = commit is null ? "current revision" : ShortHash(commit);
        return RunRemoteAsync($"Bisect: mark {what} {mark}", $"Marked {what} {mark}", repositoryPath,
            (output, cancellation) => _operations.MarkBisectAsync(repositoryPath, option, commit, output, cancellation),
            keepOutputOpen: true);
    }

    public Task<bool> StopBisectAsync(string repositoryPath)
        => RunRemoteAsync("Bisect reset", "Bisect stopped", repositoryPath,
            (output, cancellation) => _operations.StopBisectAsync(repositoryPath, output, cancellation));

    private static string ShortHash(string commit) => commit.Length > 8 ? commit[..8] : commit;

    public Task<bool> MergeAsync(string repositoryPath, string branch)
        => RunAsync($"Merged {branch}", repositoryPath, () => _operations.MergeAsync(repositoryPath, branch));

    public Task<bool> AbortMergeAsync(string repositoryPath)
        => RunAsync("Merge aborted", repositoryPath, () => _operations.AbortMergeAsync(repositoryPath));

    public Task<bool> DeleteUntrackedAsync(string repositoryPath, IReadOnlyList<string> paths)
        => RunAsync($"Deleted {paths.Count} untracked file(s)", repositoryPath,
            () => _operations.DeleteUntrackedAsync(repositoryPath, paths));

    public Task<bool> AddRemoteAsync(string repositoryPath, string name, string url)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return RejectAsync("Enter a remote name.");
        }

        if (string.IsNullOrWhiteSpace(url))
        {
            return RejectAsync("Enter a remote URL or path.");
        }

        return RunAsync($"Added remote {name.Trim()}", repositoryPath,
            () => _operations.AddRemoteAsync(repositoryPath, name.Trim(), url.Trim()));
    }

    public Task<bool> RemoveRemoteAsync(string repositoryPath, string name)
        => RunAsync($"Removed remote {name}", repositoryPath,
            () => _operations.RemoveRemoteAsync(repositoryPath, name));

    public Task<bool> DiscardChangesAsync(string repositoryPath, IReadOnlyList<string> paths)
        => RunAsync($"Discarded changes to {paths.Count} file(s)", repositoryPath,
            () => _operations.DiscardChangesAsync(repositoryPath, paths));

    public Task<bool> ResolveConflictsAsync(string repositoryPath, IReadOnlyList<string> paths, bool ours)
        => RunAsync($"Kept {(ours ? "ours" : "theirs")} for {paths.Count} file(s)", repositoryPath,
            () => _operations.ResolveConflictsAsync(repositoryPath, paths, ours));

    /// <summary>
    ///  Runs the merge tool on <paramref name="path"/>. The repository is reloaded afterwards, since the tool usually stages
    ///  the resolved file.
    /// </summary>
    public Task<bool> RunMergeToolAsync(string repositoryPath, string path)
        => RunAsync($"Merge tool closed for {path}", repositoryPath,
            () => _operations.RunMergeToolAsync(repositoryPath, path));

    /// <summary>
    ///  Opens a file in the configured diff tool. Nothing is reloaded: the tool only shows the change.
    /// </summary>
    public async Task<bool> RunDiffToolAsync(string repositoryPath, string path, string? commit, bool staged)
    {
        ErrorMessage = null;
        try
        {
            await _operations.RunDiffToolAsync(repositoryPath, path, commit, staged);
            StatusMessage = $"Diff tool started for {path}";
            return true;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            return false;
        }
    }

    /// <summary>
    ///  Marks conflicted files as resolved by staging them as they are in the working tree.
    /// </summary>
    public Task<bool> MarkResolvedAsync(string repositoryPath, IReadOnlyList<string> paths)
        => RunAsync("Marked as resolved", repositoryPath, () => _operations.StageAsync(repositoryPath, paths));

    /// <summary>
    ///  Creates a repository in <paramref name="folder"/>, creating the folder when it does not exist. The new
    ///  repository is reported through <see cref="RepositoryChanged"/>, so a view can open it.
    /// </summary>
    public Task<bool> InitAsync(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            return RejectAsync("Enter a folder to create the repository in.");
        }

        return RunAsync("Initialized repository", folder.Trim(), () => _operations.InitAsync(folder.Trim()));
    }

    /// <summary>
    ///  Clones <paramref name="sourceUrl"/> into <paramref name="targetPath"/>. The clone is reported through
    ///  <see cref="RepositoryChanged"/> with the target path, so a view can open it.
    /// </summary>
    public Task<bool> CloneAsync(string sourceUrl, string targetPath)
    {
        if (string.IsNullOrWhiteSpace(sourceUrl))
        {
            return RejectAsync("Enter a repository URL or path to clone.");
        }

        if (string.IsNullOrWhiteSpace(targetPath))
        {
            return RejectAsync("Enter a folder to clone into.");
        }

        return RunRemoteAsync($"Clone {sourceUrl.Trim()}", "Cloned", targetPath,
            (output, cancellation) =>
                _operations.CloneAsync(sourceUrl.Trim(), targetPath.Trim(), output, cancellation));
    }

    private async Task<bool> RunRemoteAsync(string title, string successMessage, string repositoryPath,
        Func<IProgress<GitOutputLine>, CancellationToken, Task> operation, bool keepOutputOpen = false)
    {
        lock (_outputLock)
        {
            _output.Clear();
        }

        OutputLines = [];
        OutputTitle = title;
        KeepOutputOpen = keepOutputOpen;
        using CancellationTokenSource cancellation = new();
        _cancellation = cancellation;
        RemoteState = RemoteOperationState.Running;
        RemoteOperationStarted?.Invoke(this, EventArgs.Empty);

        OutputReporter progress = new(SynchronizationContext.Current, AppendOutput);
        ErrorMessage = null;
        IsBusy = true;
        try
        {
            await operation(progress, cancellation.Token);
            StatusMessage = successMessage;
            RemoteState = RemoteOperationState.Succeeded;
            RepositoryChanged?.Invoke(this, new RepositoryChangedEventArgs(repositoryPath));
            return true;
        }
        catch (OperationCanceledException)
        {
            // A cancelled fetch or pull may have updated some refs, so the repository is still reloaded.
            StatusMessage = "Cancelled";
            RemoteState = RemoteOperationState.Cancelled;
            RepositoryChanged?.Invoke(this, new RepositoryChangedEventArgs(repositoryPath));
            return false;
        }
        catch (Exception ex)
        {
            StatusMessage = "";
            RemoteState = RemoteOperationState.Failed;
            ErrorMessage = null;
            ErrorMessage = ex.Message;
            return false;
        }
        finally
        {
            _cancellation = null;
            IsBusy = false;
        }
    }

    // A console redraws a progress line in place; the line after it replaces it here too.
    private void AppendOutput(GitOutputLine line)
    {
        IReadOnlyList<string> snapshot;
        lock (_outputLock)
        {
            if (_output.Count > 0 && _output[^1].IsProgress)
            {
                _output[^1] = line;
            }
            else
            {
                _output.Add(line);
            }

            snapshot = [.. _output.Select(entry => entry.Text)];
        }

        OutputLines = snapshot;
    }

    private Task<bool> RejectAsync(string message)
    {
        ErrorMessage = null;
        ErrorMessage = message;
        return Task.FromResult(false);
    }

    private async Task<bool> RunAsync(string successMessage, string repositoryPath, Func<Task> operation)
    {
        ErrorMessage = null;
        IsBusy = true;
        try
        {
            await operation();
            StatusMessage = successMessage;
            RepositoryChanged?.Invoke(this, new RepositoryChangedEventArgs(repositoryPath));
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = "";
            ErrorMessage = null;
            ErrorMessage = ex.Message;
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }
}

public sealed class RepositoryChangedEventArgs(string repositoryPath) : EventArgs
{
    public string RepositoryPath { get; } = repositoryPath;
}

/// <summary>
///  Passes git's output to the view model on the context the operation started on (the UI thread in the app), in order.
///  Without a context the lines are passed on directly. <see cref="Progress{T}"/> is not used: without a context it posts
///  each line to the thread pool, which can reorder them.
/// </summary>
internal sealed class OutputReporter(SynchronizationContext? context, Action<GitOutputLine> append)
    : IProgress<GitOutputLine>
{
    public void Report(GitOutputLine value)
    {
        if (context is null)
        {
            append(value);
        }
        else
        {
            context.Post(_ => append(value), null);
        }
    }
}

public enum RemoteOperationState
{
    None,
    Running,
    Succeeded,
    Failed,
    Cancelled
}

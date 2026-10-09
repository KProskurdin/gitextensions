using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Operations;

namespace GitExtensions.Xplat.Core.Tests;

/// <summary>
///  Records each call and keeps it pending until the test completes or fails it.
/// </summary>
internal sealed class FakeGitOperations : IGitOperations
{
    private readonly List<(string Name, string Arguments, TaskCompletionSource Completion)> _calls = [];
    private readonly Dictionary<int, (IProgress<GitOutputLine>? Output, CancellationToken Token)> _remote = [];

    public int Count => _calls.Count;

    public string NameAt(int index) => _calls[index].Name;

    public string ArgumentsAt(int index) => _calls[index].Arguments;

    public Task StageAsync(string repositoryPath, IReadOnlyList<string> paths) =>
        Record("Stage", $"{repositoryPath} {string.Join(",", paths)}");

    public Task UnstageAsync(string repositoryPath, IReadOnlyList<string> paths) =>
        Record("Unstage", $"{repositoryPath} {string.Join(",", paths)}");

    public Task StageLinesAsync(string repositoryPath, string diffText, int selectionStart, int selectionLength,
        bool unstage, bool isNewFile = false) => Record("StageLines",
        $"{repositoryPath} {selectionStart}+{selectionLength} unstage={unstage} new={isNewFile}");

    public Task CommitAsync(string repositoryPath, string message, bool amend, bool signOff, string author) =>
        Record("Commit", $"{repositoryPath} {message} amend={amend} signOff={signOff} author={author}");

    public Task CreateBranchAsync(string repositoryPath, string name, bool checkout, string? startPoint) =>
        Record("CreateBranch", $"{repositoryPath} {name} checkout={checkout}");

    public Task CheckoutAsync(string repositoryPath, string branch,
        GitCommands.LocalChangesAction localChanges = GitCommands.LocalChangesAction.DontChange) =>
        Record("Checkout", $"{repositoryPath} {branch}");

    public Task CheckoutRemoteAsync(string repositoryPath, string remoteBranch) =>
        Record("CheckoutRemote", $"{repositoryPath} {remoteBranch}");

    public Task DeleteBranchAsync(string repositoryPath, string branch, bool force) =>
        Record("DeleteBranch", $"{repositoryPath} {branch} force={force}");

    public Task FetchAsync(string repositoryPath, string remote, bool prune, IProgress<GitOutputLine>? output = null,
        CancellationToken cancellationToken = default) => RecordRemote("Fetch",
        $"{repositoryPath} {remote} prune={prune}", output, cancellationToken);

    public Task PushTagsAsync(string repositoryPath, string remote, IProgress<GitOutputLine>? output = null,
        CancellationToken cancellationToken = default) =>
        RecordRemote("PushTags", $"{repositoryPath} {remote}", output, cancellationToken);

    public Task PullAsync(string repositoryPath, string remote, string branch, bool rebase,
        IProgress<GitOutputLine>? output = null, CancellationToken cancellationToken = default) => RecordRemote("Pull",
        $"{repositoryPath} {remote} {branch} rebase={rebase}", output, cancellationToken);

    public Task PushAsync(string repositoryPath, string remote, string branch, IProgress<GitOutputLine>? output = null,
        CancellationToken cancellationToken = default) =>
        RecordRemote("Push", $"{repositoryPath} {remote} {branch}", output, cancellationToken);

    public Task PushAsync(string repositoryPath, PushRequest request, IProgress<GitOutputLine>? output = null,
        CancellationToken cancellationToken = default) =>
        RecordRemote("PushRequest", $"{repositoryPath} {request}", output, cancellationToken);

    public Task PullAsync(string repositoryPath, PullRequest request, IProgress<GitOutputLine>? output = null,
        CancellationToken cancellationToken = default) =>
        RecordRemote("PullRequest", $"{repositoryPath} {request}", output, cancellationToken);

    public Task RenameRemoteAsync(string repositoryPath, string name, string newName) =>
        Record("RenameRemote", $"{repositoryPath} {name} {newName}");

    public Task SetRemoteUrlAsync(string repositoryPath, string name, string url) =>
        Record("SetRemoteUrl", $"{repositoryPath} {name} {url}");

    public Task DeleteRemoteTagAsync(string repositoryPath, string remote, string name,
        IProgress<GitOutputLine>? output = null, CancellationToken cancellationToken = default) =>
        RecordRemote("DeleteRemoteTag", $"{repositoryPath} {remote} {name}", output, cancellationToken);

    public Task AddWorktreeAsync(string repositoryPath, string path, string branch, string newBranch) =>
        Record("AddWorktree", $"{repositoryPath} {path} {branch} {newBranch}");

    public Task RemoveWorktreeAsync(string repositoryPath, string path, bool force) =>
        Record("RemoveWorktree", $"{repositoryPath} {path} force={force}");

    public Task PruneWorktreesAsync(string repositoryPath) => Record("PruneWorktrees", repositoryPath);

    public Task UpdateSubmodulesAsync(string repositoryPath, string? path, IProgress<GitOutputLine>? output = null,
        CancellationToken cancellationToken = default) => RecordRemote("UpdateSubmodules", $"{repositoryPath} {path}",
        output, cancellationToken);

    public Task SyncSubmodulesAsync(string repositoryPath, string? path) =>
        Record("SyncSubmodules", $"{repositoryPath} {path}");

    public Task CloneAsync(string sourceUrl, string targetPath, IProgress<GitOutputLine>? output = null,
        CancellationToken cancellationToken = default, int? depth = null) =>
        RecordRemote("Clone", depth is null ? $"{sourceUrl} {targetPath}" : $"{sourceUrl} {targetPath} --depth {depth}", output,
            cancellationToken);

    public Task InitAsync(string folder) => Record("Init", folder);

    public Task StashAsync(string repositoryPath, string message, bool includeUntracked, bool keepIndex,
        IReadOnlyList<string>? paths) => Record("Stash",
        $"{repositoryPath} {message} untracked={includeUntracked} keepIndex={keepIndex} paths={string.Join(",", paths ?? [])}");

    public Task ApplyStashAsync(string repositoryPath, string stashName) =>
        Record("ApplyStash", $"{repositoryPath} {stashName}");

    public Task PopStashAsync(string repositoryPath, string stashName) =>
        Record("PopStash", $"{repositoryPath} {stashName}");

    public Task DropStashAsync(string repositoryPath, string stashName) =>
        Record("DropStash", $"{repositoryPath} {stashName}");

    public Task CreateTagAsync(string repositoryPath, string name, string commit, string message) =>
        Record("CreateTag", $"{repositoryPath} {name} {commit} message={message}");

    public Task DeleteRemoteBranchAsync(string repositoryPath, string remote, string branch) =>
        Record("DeleteRemoteBranch", $"{repositoryPath} {remote} {branch}");

    public Task RenameBranchAsync(string repositoryPath, string branch, string newName) =>
        Record("RenameBranch", $"{repositoryPath} {branch} {newName}");

    public Task DeleteTagAsync(string repositoryPath, string name) => Record("DeleteTag", $"{repositoryPath} {name}");

    public Task CherryPickAsync(string repositoryPath, string commit) =>
        Record("CherryPick", $"{repositoryPath} {commit}");

    public Task ResetAsync(string repositoryPath, string commit, ResetMode mode) =>
        Record("Reset", $"{repositoryPath} {commit} {mode}");

    public Task RevertAsync(string repositoryPath, string commit) => Record("Revert", $"{repositoryPath} {commit}");

    public Task RebaseAsync(string repositoryPath, string branch) => Record("Rebase", $"{repositoryPath} {branch}");

    public Task AbortRebaseAsync(string repositoryPath) => Record("AbortRebase", repositoryPath);

    public Task ContinueRebaseAsync(string repositoryPath, string? editorCommand = null) =>
        Record("ContinueRebase", $"{repositoryPath} editor={editorCommand}");

    public Task SkipRebaseAsync(string repositoryPath, string? editorCommand = null) =>
        Record("SkipRebase", $"{repositoryPath} editor={editorCommand}");

    public Task EditRebaseTodoAsync(string repositoryPath, string editorCommand) =>
        Record("EditRebaseTodo", $"{repositoryPath} editor={editorCommand}");

    public Task RebaseInteractiveAsync(string repositoryPath, string onto, string editorCommand,
        IProgress<GitOutputLine>? output = null, CancellationToken cancellationToken = default) => RecordRemote(
        "RebaseInteractive", $"{repositoryPath} {onto} editor={editorCommand}", output, cancellationToken);

    public Task ContinueMergeAsync(string repositoryPath, string? editorCommand = null) =>
        Record("ContinueMerge", $"{repositoryPath} editor={editorCommand}");

    public Task ContinuePatchAsync(string repositoryPath) => Record("ContinuePatch", repositoryPath);

    public Task SkipPatchAsync(string repositoryPath) => Record("SkipPatch", repositoryPath);

    public Task AbortPatchAsync(string repositoryPath) => Record("AbortPatch", repositoryPath);

    public Task StartBisectAsync(string repositoryPath, IProgress<GitOutputLine>? output = null,
        CancellationToken cancellationToken = default) =>
        RecordRemote("StartBisect", repositoryPath, output, cancellationToken);

    public Task MarkBisectAsync(string repositoryPath, GitCommands.Git.GitBisectOption option, string? commit,
        IProgress<GitOutputLine>? output = null, CancellationToken cancellationToken = default) =>
        RecordRemote("MarkBisect", $"{repositoryPath} {option} {commit ?? "current"}", output, cancellationToken);

    public Task StopBisectAsync(string repositoryPath, IProgress<GitOutputLine>? output = null,
        CancellationToken cancellationToken = default) =>
        RecordRemote("StopBisect", repositoryPath, output, cancellationToken);

    public Task MergeAsync(string repositoryPath, string branch) => Record("Merge", $"{repositoryPath} {branch}");

    public Task AbortMergeAsync(string repositoryPath) => Record("AbortMerge", repositoryPath);

    public Task DiscardChangesAsync(string repositoryPath, IReadOnlyList<string> paths) =>
        Record("DiscardChanges", $"{repositoryPath} {string.Join(",", paths)}");

    public Task DeleteUntrackedAsync(string repositoryPath, IReadOnlyList<string> paths) =>
        Record("DeleteUntracked", $"{repositoryPath} {string.Join(",", paths)}");

    public Task AddRemoteAsync(string repositoryPath, string name, string url) =>
        Record("AddRemote", $"{repositoryPath} {name} {url}");

    public Task RemoveRemoteAsync(string repositoryPath, string name) =>
        Record("RemoveRemote", $"{repositoryPath} {name}");

    public Task ResolveConflictsAsync(string repositoryPath, IReadOnlyList<string> paths, bool ours) =>
        Record("ResolveConflicts", $"{repositoryPath} {string.Join(",", paths)} ours={ours}");

    public Task RunMergeToolAsync(string repositoryPath, string path) =>
        Record("RunMergeTool", $"{repositoryPath} {path}");

    public Task RunDiffToolAsync(string repositoryPath, string path, string? commit, bool staged) =>
        Record("RunDiffTool", $"{repositoryPath} {path} {commit} staged={staged}");

    public void Complete(int index) => _calls[index].Completion.SetResult();

    public void Fail(int index, Exception exception) => _calls[index].Completion.SetException(exception);

    /// <summary>
    ///  Where a remote call reports its output; null when the caller passed none.
    /// </summary>
    public IProgress<GitOutputLine>? OutputAt(int index) => _remote[index].Output;

    public CancellationToken TokenAt(int index) => _remote[index].Token;

    private Task RecordRemote(string name, string arguments, IProgress<GitOutputLine>? output,
        CancellationToken cancellationToken)
    {
        _remote[_calls.Count] = (output, cancellationToken);
        return Record(name, arguments);
    }

    private Task Record(string name, string arguments)
    {
        TaskCompletionSource completion = new();
        _calls.Add((name, arguments, completion));
        return completion.Task;
    }
}

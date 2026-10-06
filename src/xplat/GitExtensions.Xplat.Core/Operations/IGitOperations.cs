using GitCommands;
using GitExtensions.Extensibility.Git;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  Changes a repository or talks to its remotes. Implementations run each operation off the calling thread and throw
///  <see cref="GitOperationException"/> with git's own message when git exits with an error. The remote operations report
///  git's output to <c>output</c> while they run when one is given, and stop git when <c>cancellationToken</c> is cancelled.
/// </summary>
public interface IGitOperations
{
    Task StageAsync(string repositoryPath, IReadOnlyList<string> paths);

    Task UnstageAsync(string repositoryPath, IReadOnlyList<string> paths);

    /// <summary>
    ///  Stages only the lines selected in the diff of one file, or with <paramref name="unstage"/> removes them from the
    ///  index. <paramref name="diffText"/> is the diff as git printed it; the selection is a character range in it. An
    ///  untracked file's diff (from /dev/null) adds the file with only the selected lines. <paramref name="isNewFile"/> marks
    ///  the staged diff of a file that is new in the index, so unstaging lines keeps the file staged with the other lines.
    /// </summary>
    Task StageLinesAsync(string repositoryPath, string diffText, int selectionStart, int selectionLength, bool unstage,
        bool isNewFile = false);

    Task CommitAsync(string repositoryPath, string message, bool amend, bool signOff, string author);

    /// <summary>
    ///  Creates branch <paramref name="name"/> at <paramref name="startPoint"/>, or at HEAD when none is given.
    /// </summary>
    Task CreateBranchAsync(string repositoryPath, string name, bool checkout, string? startPoint = null);

    /// <summary>
    ///  Checks out <paramref name="branch"/>. <paramref name="localChanges"/> says what happens to local changes, as in upstream's
    ///  checkout dialog: keep them (git refuses when they conflict), merge them, stash them and apply them again after, or
    ///  discard them.
    /// </summary>
    Task CheckoutAsync(string repositoryPath, string branch,
        LocalChangesAction localChanges = LocalChangesAction.DontChange);

    /// <summary>
    ///  Creates a local branch that tracks <paramref name="remoteBranch"/> (e.g. "origin/feature") and checks it out.
    /// </summary>
    Task CheckoutRemoteAsync(string repositoryPath, string remoteBranch);

    Task DeleteBranchAsync(string repositoryPath, string branch, bool force);

    Task DeleteRemoteBranchAsync(string repositoryPath, string remote, string branch);

    Task RenameBranchAsync(string repositoryPath, string branch, string newName);

    Task FetchAsync(string repositoryPath, string remote, bool prune, IProgress<GitOutputLine>? output = null,
        CancellationToken cancellationToken = default);

    Task PushTagsAsync(string repositoryPath, string remote, IProgress<GitOutputLine>? output = null,
        CancellationToken cancellationToken = default);

    Task PullAsync(string repositoryPath, string remote, string branch, bool rebase,
        IProgress<GitOutputLine>? output = null, CancellationToken cancellationToken = default);

    Task PushAsync(string repositoryPath, string remote, string branch, IProgress<GitOutputLine>? output = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///  Pushes with the options of upstream's push dialog: remote, local and remote branch, force, and tracking.
    /// </summary>
    Task PushAsync(string repositoryPath, PushRequest request, IProgress<GitOutputLine>? output = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///  Pulls (merge or rebase) or only fetches, with the options of upstream's pull dialog.
    /// </summary>
    Task PullAsync(string repositoryPath, PullRequest request, IProgress<GitOutputLine>? output = null,
        CancellationToken cancellationToken = default);

    Task MergeAsync(string repositoryPath, string branch);

    Task AbortMergeAsync(string repositoryPath);

    /// <summary>
    ///  Takes the ours or theirs version of conflicted files and stages them as resolved.
    /// </summary>
    Task ResolveConflictsAsync(string repositoryPath, IReadOnlyList<string> paths, bool ours);

    /// <summary>
    ///  Runs the configured merge tool on one conflicted file and waits until the tool is closed. Throws
    ///  <see cref="GitOperationException"/> when no merge tool is configured, because git would otherwise fall back to a
    ///  terminal tool that cannot run without a console.
    /// </summary>
    Task RunMergeToolAsync(string repositoryPath, string path);

    /// <summary>
    ///  Opens the configured diff tool on one file: its working-tree changes (or, with <paramref name="staged"/>, its staged
    ///  changes), or its change in <paramref name="commit"/> against the first parent. Returns once git has started the tool.
    /// </summary>
    Task RunDiffToolAsync(string repositoryPath, string path, string? commit, bool staged);

    /// <summary>
    ///  Restores the files from the index, dropping their unstaged edits. Untracked files are not touched.
    /// </summary>
    Task DiscardChangesAsync(string repositoryPath, IReadOnlyList<string> paths);

    /// <summary>
    ///  Deletes untracked files from the working tree. Only the given files are removed, never directories.
    /// </summary>
    Task DeleteUntrackedAsync(string repositoryPath, IReadOnlyList<string> paths);

    Task AddRemoteAsync(string repositoryPath, string name, string url);

    Task RemoveRemoteAsync(string repositoryPath, string name);

    /// <summary>
    ///  Adds a worktree at <paramref name="path"/> that checks out <paramref name="branch"/>, or a new branch
    ///  <paramref name="newBranch"/> started at <paramref name="branch"/> when one is given.
    /// </summary>
    Task AddWorktreeAsync(string repositoryPath, string path, string branch, string newBranch);

    Task RemoveWorktreeAsync(string repositoryPath, string path, bool force);

    /// <summary>
    ///  Forgets worktrees whose folders were deleted.
    /// </summary>
    Task PruneWorktreesAsync(string repositoryPath);

    /// <summary>
    ///  Initializes and updates every submodule (recursively), or only <paramref name="path"/> when given.
    /// </summary>
    Task UpdateSubmodulesAsync(string repositoryPath, string? path, IProgress<GitOutputLine>? output = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///  Copies the submodule URLs from .gitmodules to the repository's config, for all submodules or only <paramref name="path"/>.
    /// </summary>
    Task SyncSubmodulesAsync(string repositoryPath, string? path);

    Task RenameRemoteAsync(string repositoryPath, string name, string newName);

    Task SetRemoteUrlAsync(string repositoryPath, string name, string url);

    /// <summary>
    ///  Deletes tag <paramref name="name"/> on <paramref name="remote"/>; the local tag is kept.
    /// </summary>
    Task DeleteRemoteTagAsync(string repositoryPath, string remote, string name,
        IProgress<GitOutputLine>? output = null, CancellationToken cancellationToken = default);

    /// <summary>
    ///  Stashes the working changes, or only <paramref name="paths"/> when given.
    /// </summary>
    Task StashAsync(string repositoryPath, string message, bool includeUntracked, bool keepIndex,
        IReadOnlyList<string>? paths = null);

    Task ApplyStashAsync(string repositoryPath, string stashName);

    Task PopStashAsync(string repositoryPath, string stashName);

    Task DropStashAsync(string repositoryPath, string stashName);

    /// <summary>
    ///  Creates tag <paramref name="name"/> at <paramref name="commit"/>. An empty <paramref name="message"/> makes a lightweight tag; otherwise the tag is annotated.
    /// </summary>
    Task CreateTagAsync(string repositoryPath, string name, string commit, string message);

    Task DeleteTagAsync(string repositoryPath, string name);

    Task CherryPickAsync(string repositoryPath, string commit);

    Task RevertAsync(string repositoryPath, string commit);

    /// <summary>
    ///  Replays the checked-out branch onto <paramref name="branch"/>.
    /// </summary>
    Task RebaseAsync(string repositoryPath, string branch);

    /// <summary>
    ///  Replays the checked-out branch onto <paramref name="onto"/> after the user edits the todo list: git opens
    ///  <paramref name="editorCommand"/> (see <see cref="GitEditorCommand"/>) with the list, and again for each reworded or
    ///  squashed message. Commits are squashed automatically when git's <c>rebase.autosquash</c> is set, as upstream's
    ///  FormRebase defaults it.
    /// </summary>
    Task RebaseInteractiveAsync(string repositoryPath, string onto, string editorCommand,
        IProgress<GitOutputLine>? output = null, CancellationToken cancellationToken = default);

    Task AbortRebaseAsync(string repositoryPath);

    /// <summary>
    ///  Resumes a stopped rebase. When <paramref name="editorCommand"/> is given, git opens it for the message of a resolved
    ///  commit and of the reworded or squashed steps that follow, as upstream; otherwise git keeps each message.
    /// </summary>
    Task ContinueRebaseAsync(string repositoryPath, string? editorCommand = null);

    /// <summary>
    ///  Drops the commit the rebase stopped at and resumes, opening an editor as <see cref="ContinueRebaseAsync"/> does.
    /// </summary>
    Task SkipRebaseAsync(string repositoryPath, string? editorCommand = null);

    /// <summary>
    ///  Opens the remaining todo list of a stopped interactive rebase in <paramref name="editorCommand"/>.
    /// </summary>
    Task EditRebaseTodoAsync(string repositoryPath, string editorCommand);

    /// <summary>
    ///  Moves the checked-out branch to <paramref name="commit"/>. <paramref name="mode"/> decides what happens to the index and working tree.
    /// </summary>
    Task ResetAsync(string repositoryPath, string commit, ResetMode mode);

    Task CloneAsync(string sourceUrl, string targetPath, IProgress<GitOutputLine>? output = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///  Creates a repository in <paramref name="folder"/>, creating the folder when it does not exist.
    /// </summary>
    Task InitAsync(string folder);
}

public sealed class GitOperationException(string message) : Exception(message);

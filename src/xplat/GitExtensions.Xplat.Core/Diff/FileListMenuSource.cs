using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Scripts;

namespace GitExtensions.Xplat.Core.Diff;

/// <summary>
///  What a file list's window gives its menu (upstream's <c>BindContextMenu</c>), without UI types: the selection, the
///  actions the window has, and how the list is read again.
/// </summary>
public sealed record FileListMenuSource
{
    public required Func<string?> RepositoryPath { get; init; }

    /// <summary>
    ///  The selected files with the revisions the list shows them between; null without a selection.
    /// </summary>
    public required Func<DiffMenuSelection?> Selection { get; init; }

    /// <summary>
    ///  Upstream's <c>DescribeRevision</c>, for the reset and difftool items.
    /// </summary>
    public required Func<ObjectId, string> DescribeRevision { get; init; }

    /// <summary>
    ///  Upstream's <c>RequestRefresh</c>: reads the list again after a change.
    /// </summary>
    public required Action Refresh { get; init; }

    /// <summary>
    ///  Stage, unstage and stop tracking of the window's repository; null leaves out the items that need them.
    /// </summary>
    public Func<RepositoryOperationsViewModel?> Actions { get; init; } = () => null;

    /// <summary>
    ///  Whether the window can show a file history (and so Blame without a blame of its own).
    /// </summary>
    public Func<bool> HasFileHistory { get; init; } = () => false;

    /// <summary>
    ///  Upstream's <c>openInFileTreeTab</c>; null hides "Show in File tree" and keeps File history a normal item.
    /// </summary>
    public Action<string>? ShowInFileTree { get; init; }

    public Func<DiffMenuSelection, bool> CanShowInFileTree { get; init; } = _ => true;

    /// <summary>
    ///  Upstream's <c>filterFileInGrid</c> with the grid's path filter; null hides "Filter file in grid".
    /// </summary>
    public Action<string>? FilterFileInGrid { get; init; }

    /// <summary>
    ///  Upstream's <c>blame</c>; null opens the file history on its Blame tab.
    /// </summary>
    public Action? Blame { get; init; }

    /// <summary>
    ///  Whether Blame shows checked (the File tree tab); null for a plain item.
    /// </summary>
    public Func<bool>? BlameChecked { get; init; }

    /// <summary>
    ///  Upstream's <c>getCurrentRevision</c>: the commit a folder's history starts from; null for the working directory.
    /// </summary>
    public Func<string?> CurrentCheckout { get; init; } = () => null;

    /// <summary>
    ///  The list's tree for upstream's tree items; null hides them.
    /// </summary>
    public FileListTree? Tree { get; init; }

    /// <summary>
    ///  The viewer of the selected file (upstream's <c>getSupportLinePatching</c>); null hides "Cherry pick changes".
    /// </summary>
    public Func<DiffViewModel?>? Viewer { get; init; }

    /// <summary>
    ///  Upstream's submodule commands for the selected submodules; null hides the submodule items.
    /// </summary>
    public Func<FileListSubmodules?> Submodules { get; init; } = () => null;

    /// <summary>
    ///  The list's "Find in commit files using git-grep"; null hides its items.
    /// </summary>
    public IFileListGitGrep? GitGrep { get; init; }

    /// <summary>
    ///  Upstream's <c>stage</c> and <c>unstage</c> of <c>BindContextMenu</c> (FormCommit's lists); null for the menu's own.
    /// </summary>
    public Action? Stage { get; init; }

    public Action? Unstage { get; init; }

    /// <summary>
    ///  The list's files for "Find file" and how to select one; null hides the item.
    /// </summary>
    public FileListFind? FindFile { get; init; }

    /// <summary>
    ///  The path of the file selected last (upstream's <c>FocusedItem</c>); null when unknown.
    /// </summary>
    public Func<string?> FocusedPath { get; init; } = () => null;

    /// <summary>
    ///  Runs a user script on the selection (upstream's <c>AddUserScripts</c> items); null offers none.
    /// </summary>
    public Action<ScriptDefinition>? RunScript { get; init; }

    /// <summary>
    ///  The index of <see cref="FocusedPath"/> in the selection's files, or -1.
    /// </summary>
    public int FocusedIndex(DiffMenuSelection selection)
        => FocusedPath() is { } focused
            ? selection.Files.Select(file => file.Path).ToList().IndexOf(focused)
            : -1;
}

/// <summary>
///  What the file list menu asks of the window it is shown over: its questions, pickers and windows, and the system's
///  programs. The app implements it over the menu's owner window; tests record what was asked.
/// </summary>
public interface IFileListMenuHost
{
    /// <summary>
    ///  Upstream's translation of an English text.
    /// </summary>
    string Translate(string english);

    void ShowError(string message);

    /// <summary>
    ///  Git's error of a rename or move, titled as upstream's "Rename / move", completing when it is closed.
    /// </summary>
    Task ShowMoveErrorAsync(string message);

    /// <summary>
    ///  Upstream's FormResetChanges with <paramref name="question"/>.
    /// </summary>
    Task<ResetChangesAction> AskResetAsync(bool hasExistingFiles, bool hasNewFiles, string question);

    /// <summary>
    ///  Upstream's "New name" prompt of "Rename / move"; null when cancelled. An empty name is an answer.
    /// </summary>
    Task<string?> AskNewNameAsync(string initialValue);

    /// <summary>
    ///  A question with Yes and No; true for Yes.
    /// </summary>
    Task<bool> ConfirmAsync(string text, string caption);

    /// <summary>
    ///  Upstream's save dialog for one file, starting at <paramref name="suggestion"/>; null when cancelled.
    /// </summary>
    Task<string?> PickSaveFileAsync(string suggestion);

    /// <summary>
    ///  Upstream's folder dialog for several files, starting at <paramref name="start"/>; null when cancelled.
    /// </summary>
    Task<string?> PickFolderAsync(string start);

    /// <summary>
    ///  The file with the program the system opens it with.
    /// </summary>
    Task OpenFileAsync(string path);

    /// <summary>
    ///  The system's "Open with" dialog for the file (Windows).
    /// </summary>
    void OpenWith(string path);

    /// <summary>
    ///  The file in Git Extensions' editor.
    /// </summary>
    void EditFile(string path);

    /// <summary>
    ///  The file in Visual Studio at the viewer's line (Windows).
    /// </summary>
    Task OpenInVisualStudioAsync(string path);

    void OpenFolder(string folder);

    void ShowFileHistory(string repositoryPath, string file, string? revision, bool showBlame);

    /// <summary>
    ///  Upstream's search window over <paramref name="candidates"/>; the file taken, or null.
    /// </summary>
    Task<string?> FindFileAsync(Func<string, IEnumerable<string>> candidates);

    /// <summary>
    ///  Upstream's AddToGitIgnore window; true when the patterns were added.
    /// </summary>
    Task<bool> AddToIgnoreAsync(string repositoryPath, bool localExclude, IReadOnlyList<string> patterns);

    /// <summary>
    ///  Upstream's <c>CherryPickAllChanges</c>: the viewer's whole diff applied to the working directory and the index.
    /// </summary>
    Task CherryPickAsync(string repositoryPath, (string Text, int Start, int Length) range, string status);

    /// <summary>
    ///  Git's interactive patch of the file in a terminal (upstream's <c>InteractiveAdd_Click</c>, <c>ResetChunkOfFile_Click</c>).
    /// </summary>
    Task RunInteractivePatchAsync(string repositoryPath, string path, bool reset);

    Task SetClipboardAsync(string text);
}

/// <summary>
///  A file list's "Find in commit files using git-grep": its box and its window.
/// </summary>
public interface IFileListGitGrep
{
    bool BoxVisible { get; }

    void SetBoxVisible(bool visible);

    void ShowWindow(string text);
}

/// <summary>
///  Upstream FileStatusList's submodule items, each for the selected submodules' paths in the repository.
/// </summary>
/// <param name="Open">Upstream's <c>OpenSubmoduleAsync</c>: the submodule in a new instance.</param>
/// <param name="Update">Upstream's <c>UpdateSubmodule_Click</c>.</param>
/// <param name="Reset">Upstream's <c>ResetSubmoduleChanges_Click</c>, after its question.</param>
/// <param name="Stash">Upstream's <c>StashSubmoduleChanges_Click</c>.</param>
/// <param name="Commit">Upstream's <c>CommitSubmoduleChanges_Click</c>: a commit window on each submodule.</param>
public sealed record FileListSubmodules(
    Func<string, Task> Open,
    Func<IReadOnlyList<string>, Task> Update,
    Func<IReadOnlyList<string>, Task> Reset,
    Func<IReadOnlyList<string>, Task> Stash,
    Func<IReadOnlyList<string>, Task> Commit);

/// <summary>
///  A file list shown as a tree, for upstream's tree items (<c>FileStatusList.TreeContextMenu.cs</c>).
/// </summary>
/// <param name="HasSubnodes">Whether a selected node has nodes below it.</param>
/// <param name="SelectAll">Selects the nodes below the selected ones.</param>
/// <param name="CollapseAll">Collapses the selected nodes and the nodes below them.</param>
/// <param name="ExpandAll">Expands the selected nodes and the nodes below them.</param>
/// <param name="AnyRootExpanded">Whether "Collapse root folders" shows: a root folder is expanded.</param>
/// <param name="CollapseRootFolders">Selects the selected node's root folder and collapses the root folders.</param>
public sealed record FileListTree(
    Func<bool> HasSubnodes,
    Action SelectAll,
    Action CollapseAll,
    Action ExpandAll,
    Func<bool> AnyRootExpanded,
    Action CollapseRootFolders);

/// <summary>
///  What upstream's "Find file" searches in a file list and how it selects the file taken.
/// </summary>
/// <param name="Files">All the list's files (upstream's <c>GitItemStatuses</c>): path and path before a rename.</param>
/// <param name="Select">Selects the file with the path in the list.</param>
public sealed record FileListFind(Func<IReadOnlyList<(string Path, string? OldPath)>> Files, Action<string> Select);

using GitCommands;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Repository;
using GitUIPluginInterfaces;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  A file of the stash window's list, with what shows its diff.
/// </summary>
/// <param name="Change">The file and its change.</param>
/// <param name="Group">Upstream's group header for the working directory's changes ("Commit index" or "Working
/// directory"); empty for a stash, whose files are one group.</param>
/// <param name="Commit">The commit whose diff shows the file: the stash, or its third parent for an untracked file the
/// stash kept; null for a change in the working directory.</param>
public sealed record StashFile(FileChange Change, string Group, string? Commit);

/// <summary>
///  Upstream's <c>FormStash</c> without its window: the stashes with the working directory's changes first, which one is
///  shown after a load, a drop or a stash, what can be done with it, and the files it holds.
/// </summary>
public sealed class StashDialog
{
    // Upstream's texts.
    public const string CurrentWorkingDirChanges = "Current working directory changes";
    public const string NoStashes = "There are no stashes.";
    public const string IndexGroup = "Commit index";
    public const string WorkspaceGroup = "Working directory";
    public const string DropConfirmTitle = "Drop Stash Confirmation";
    public const string DropConfirmText = "Are you sure you want to drop the stash? This action cannot be undone.";
    public const string DropConfirmHeading = "This action cannot be undone.";
    public const string DontShowAgain = "&Don't show me this message again";

    // Upstream's _currentWorkingDirStashItem: not a stash, the changes a new stash would take.
    private readonly GitStash _workingDirectory = new(-1, CurrentWorkingDirChanges);
    private bool _manageStashes;
    private int _lastSelectedStashIndex = -1;

    /// <param name="manageStashes">Upstream's <c>ManageStashes</c>: the first load shows the newest stash rather than the
    /// working directory.</param>
    /// <param name="initialStash">The stash to show first ("stash@{n}"), as upstream's left panel opens one.</param>
    public StashDialog(bool manageStashes = true, string? initialStash = null)
    {
        _manageStashes = manageStashes;
        if (initialStash is not null
            && int.TryParse(initialStash.SubstringAfter('{').SubstringUntil('}'), out int index))
        {
            _lastSelectedStashIndex = index + 1;
        }
    }

    /// <summary>
    ///  The working directory's changes, then the stashes, newest first.
    /// </summary>
    public IReadOnlyList<GitStash> Items { get; private set; } = [];

    public int SelectedIndex { get; private set; } = -1;

    public GitStash? Selected => SelectedIndex >= 0 && SelectedIndex < Items.Count ? Items[SelectedIndex] : null;

    public bool IsWorkingDirectorySelected => Selected == _workingDirectory;

    /// <summary>
    ///  Apply and Drop act on a stash, not on the working directory.
    /// </summary>
    public bool CanApplyOrDrop => Selected is not null && !IsWorkingDirectorySelected;

    /// <summary>
    ///  Only a message for a new stash can be typed; a stash's own message is shown read-only.
    /// </summary>
    public bool IsMessageReadOnly => !IsWorkingDirectorySelected;

    /// <summary>
    ///  The text of the message box for the selection: empty for the working directory, the stash's message, or upstream's
    ///  note when there is no stash.
    /// </summary>
    public string Message => Items.Count == 1 ? NoStashes :
        Selected is { } stash && !IsWorkingDirectorySelected ? stash.Message : "";

    /// <summary>
    ///  Upstream's <c>EnablePartialStash</c>: selected files can be stashed from the working directory.
    /// </summary>
    public bool CanStashSelected(int selectedFiles) => IsWorkingDirectorySelected && selectedFiles > 0;

    /// <summary>
    ///  Upstream's <c>Initialize</c>: the list read again and the stash to show chosen: the next one after a drop (or the
    ///  one asked for), the newest on the first load when managing stashes, else the working directory.
    /// </summary>
    public void Load(IEnumerable<GitStash> stashes)
    {
        Items = [_workingDirectory, .. stashes];
        if (_lastSelectedStashIndex > 0)
        {
            SelectedIndex = Math.Min(_lastSelectedStashIndex, Items.Count - 1);
            _lastSelectedStashIndex = -1;
        }
        else if (_manageStashes && Items.Count > 1)
        {
            SelectedIndex = 1;
            _manageStashes = false;
        }
        else
        {
            SelectedIndex = 0;
        }
    }

    public void Select(int index)
    {
        if (index >= 0 && index < Items.Count)
        {
            SelectedIndex = index;
        }
    }

    /// <summary>
    ///  Remembers the selection before a drop, so the next load shows the stash that took its place.
    /// </summary>
    public void Dropping() => _lastSelectedStashIndex = SelectedIndex;

    /// <summary>
    ///  Upstream's <c>ChangeSelectedStash</c>: "next" moves up the list towards the newer stashes, as in the grid. False at
    ///  either end.
    /// </summary>
    public bool SelectNeighbour(bool next)
    {
        int index = SelectedIndex + (next ? -1 : 1);
        if (index < 0 || index >= Items.Count)
        {
            return false;
        }

        SelectedIndex = index;
        return true;
    }

    /// <summary>
    ///  Upstream's <c>StashClick</c> message: the typed text trimmed, after a space; empty when nothing was typed.
    /// </summary>
    public static string MessageArgument(string? text) => string.IsNullOrWhiteSpace(text) ? "" : " " + text.Trim();

    /// <summary>
    ///  The files of <paramref name="stash"/> as upstream's <c>InitializeSoft</c> and <c>LoadGitItemStatuses</c> list them:
    ///  for the working directory the staged changes and the others as two groups, for a stash its changes against the
    ///  commit it was made on and the untracked files it kept.
    /// </summary>
    public IReadOnlyList<StashFile> Files(GitModule module, GitStash stash)
    {
        if (stash == _workingDirectory)
        {
            return
            [
                .. module.GetAllChangedFiles().Select(GitRepositoryService.ToFileChange)
                    .OrderBy(change => !change.Staged)
                    .Select(change => new StashFile(change, change.Staged ? IndexGroup : WorkspaceGroup, Commit: null)),
            ];
        }

        // Upstream marks the untracked files read from the stash's third parent with an unset staged state.
        return
        [
            .. module.GetStashDiffFiles(stash.Name).Select(status => new StashFile(
                GitRepositoryService.ToFileChange(status),
                "", status.Staged == StagedStatus.Unset ? $"{stash.Name}^3" : stash.Name)),
        ];
    }
}

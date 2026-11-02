using System.Text;
using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Repository;
using GitUI;
using GitUI.CommandsDialogs;
using GitUI.UserControls;
using GitUIPluginInterfaces;

namespace GitExtensions.Xplat.Core.Diff;

/// <summary>
///  A file selected in a list of changed files: git's status letter, its path, the path it had before a rename or copy,
///  and whether git tracks it.
/// </summary>
public sealed record DiffMenuFile(
    string Status,
    string Path,
    string? OldPath,
    bool IsTracked,
    DiffMenuRevisions? Revisions = null);

/// <summary>
///  The two revisions one file is shown between when a list shows its files between different ones, as upstream's
///  FormStash does for the working directory (the index against HEAD, the working directory against the index).
/// </summary>
public sealed record DiffMenuRevisions(ObjectId? First, ObjectId Second, IReadOnlyList<ObjectId> SecondParents);

/// <summary>
///  The files selected in the browse window's Diff tab and the two revisions they are shown between: <see cref="First"/>
///  (A, the parent; <see langword="null"/> before the first commit) and <see cref="Second"/> (B, the selected commit, or
///  the working directory's or the index's artificial id).
/// </summary>
public sealed record DiffMenuSelection(
    ObjectId? First,
    ObjectId Second,
    IReadOnlyList<ObjectId> SecondParents,
    IReadOnlyList<DiffMenuFile> Files);

/// <summary>
///  Which items of upstream's file list menu (<c>FileStatusList.UpdateStatusOfMenuItems</c>,
///  <c>InitResetFileToToolStripMenuItem</c> and <c>OpenWithDifftool_DropDownOpening</c>) the Diff tab shows for a selection,
///  decided by upstream's <c>RevisionDiffController</c> and <c>FileStatusListContextMenuController</c>. A text that is
///  <see langword="null"/> hides its item.
/// </summary>
public sealed record DiffFileMenuState(
    bool Stage,
    bool Unstage,
    bool ResetFile,
    string? ResetToSecond,
    string? ResetToFirst,
    bool OpenWithDifftool,
    string? DiffSecondCaption,
    string? DiffFirstCaption,
    bool DiffFirstToSelected,
    bool DiffSelectedToLocal,
    bool DiffFirstToLocal,
    bool ShowToLocal,
    bool FileSeparator,
    bool OpenWorkingFile,
    bool OpenRevisionFile,
    bool OpenRevisionFileEnabled,
    bool EditWorkingFile,
    bool SaveAs,
    bool DeleteFile,
    string DeleteFileText,
    bool CopyPaths,
    bool ShowInFolder,
    bool ShowInFolderEnabled,
    bool ShowInFileTree,
    bool FilterFileInGrid,
    bool FileHistory,
    bool Blame,
    bool IgnoreSeparator,
    bool AddToIgnore,
    bool StopTracking);

/// <summary>
///  Upstream's file list item menu (<c>FileStatusList.ContextMenu.cs</c>) for the browse window's Diff tab: the state of
///  its items for a selection, and the actions that need git (reset to a revision, difftool, a revision as a file).
/// </summary>
public static class DiffFileMenu
{
    // Upstream FileStatusList's texts.
    public const string FirstRevisionText = "First: A ";
    public const string SelectedRevisionText = "Second: B ";
    public const string MultipleDescription = "<multiple>";
    public const string ResetSelectedChangesText = "Are you sure you want to reset all selected files to {0}?";

    /// <summary>
    ///  The item states for <paramref name="selection"/>.
    /// </summary>
    /// <param name="workingDir">The repository's working directory, which the paths are relative to.</param>
    /// <param name="describe">Upstream's <c>DescribeRevision</c>: how the menu names a revision.</param>
    /// <param name="isBareRepository">Whether the repository has no working directory.</param>
    public static DiffFileMenuState For(DiffMenuSelection selection, string workingDir, Func<ObjectId, string> describe,
        bool isBareRepository = false)
    {
        FullPathResolver resolver = new(() => workingDir);
        FileStatusItem[] items = Items(selection);
        ContextMenuSelectionInfo info = SelectionInfo(items, isBareRepository, resolver);
        RevisionDiffController rules = new(() => null!, resolver);
        FileStatusListContextMenuController diffTools = new();
        ContextMenuDiffToolInfo diffToolInfo = DiffToolInfo(selection, items, resolver);

        // Upstream's InitResetFileToToolStripMenuItem: the first selected file's revisions are shown; the selected
        // revision unless artificial, the first one unless artificial, or the index when only working directory files are
        // selected.
        ObjectId second = items.SecondIds().FirstOrDefault();
        ObjectId first = items.FirstIds().FirstOrDefault();
        bool canResetToSecond = CanResetToSecond(second);
        bool canResetToFirst = CanResetToFirst(first, items);

        bool isSubmodule = info.SelectedGitItemCount == 1 && info.IsAnySubmodule;
        bool isSingleFile = info.SelectedGitItemCount == 1 && !isSubmodule;
        bool canIgnoreFiles = info.IsAnyItemWorkTree && !isSubmodule;
        bool canStopTracking = isSingleFile && info.IsAnyTracked;
        bool canOpenFile = info.SelectedGitItemCount == 1 && info.AllFilesExist;
        bool showToLocal = !diffTools.ShouldHideToLocal(diffToolInfo);
        return new DiffFileMenuState(
            Stage: rules.ShouldShowMenuStage(info),
            Unstage: rules.ShouldShowMenuUnstage(info),
            ResetFile: (canResetToSecond || canResetToFirst) && rules.ShouldShowResetFileMenus(info),
            ResetToSecond: canResetToSecond ? SelectedRevisionText + describe(second) : null,
            ResetToFirst: canResetToFirst ? FirstRevisionText + describe(first) : null,
            OpenWithDifftool: rules.ShouldShowDifftoolMenus(info),
            DiffSecondCaption: items.Length > 0
                ? SelectedRevisionText + DescribeRevisions([.. items.SecondIds()], describe)
                : null,
            DiffFirstCaption: items.Length > 0
                ? FirstRevisionText + DescribeRevisions([.. items.FirstIds()], describe)
                : null,
            DiffFirstToSelected: diffTools.ShouldShowMenuFirstToSelected(diffToolInfo),
            DiffSelectedToLocal: diffTools.ShouldShowMenuSelectedToLocal(diffToolInfo),
            DiffFirstToLocal: diffTools.ShouldShowMenuFirstToLocal(diffToolInfo),
            ShowToLocal: showToLocal,
            FileSeparator: rules.ShouldShowDifftoolMenus(info) || rules.ShouldShowMenuDeleteFile(info)
                           || rules.ShouldShowMenuEditWorkingDirectoryFile(info) || rules.ShouldShowMenuOpenRevision(info),
            OpenWorkingFile: canOpenFile,
            OpenRevisionFile: rules.ShouldShowMenuOpenRevision(info),
            OpenRevisionFileEnabled: rules.ShouldShowMenuShowInFileTree(info),
            EditWorkingFile: rules.ShouldShowMenuEditWorkingDirectoryFile(info),
            SaveAs: rules.ShouldShowMenuSaveAs(info),
            DeleteFile: rules.ShouldShowMenuDeleteFile(info),
            DeleteFileText: ResourceManager.TranslatedStrings.GetDeleteFile(info.SelectedGitItemCount),
            CopyPaths: rules.ShouldShowMenuCopyFileName(info),
            ShowInFolder: rules.ShouldShowMenuShowInFolder(info),
            ShowInFolderEnabled: items.Any(item => resolver.Resolve(item.Item.Name) is { } path
                                                   && (File.Exists(path) || Directory.Exists(Path.GetDirectoryName(path)))),
            ShowInFileTree: rules.ShouldShowMenuShowInFileTree(info),
            FilterFileInGrid: rules.ShouldShowMenuFileHistory(info),
            FileHistory: rules.ShouldShowMenuFileHistory(info),
            Blame: rules.ShouldShowMenuBlame(info),
            IgnoreSeparator: canIgnoreFiles || canStopTracking,
            AddToIgnore: canIgnoreFiles,
            StopTracking: canStopTracking);
    }

    /// <summary>
    ///  Upstream's <c>ResetSelectedItemsWithConfirmation</c> before its dialog: the revision its question names, and whether the selection has
    ///  files that exist in the revision and files that would be new.
    /// </summary>
    public static (string Revision, bool HasExistingFiles, bool HasNewFiles) ResetQuestion(DiffMenuSelection selection,
        bool toFirst, Func<ObjectId, string> describe)
    {
        FileStatusItem[] items = Items(selection);
        bool hasNewFiles = !items.All(item => item.Item.IsChanged);
        bool hasExistingFiles = items.Any(item => !(item.Item.IsUncommittedAdded
                                                    || (item.Item.IsRenamed && item.Item.Staged == StagedStatus.Index)));
        string revision = toFirst
            ? FirstRevisionText + describe(items.FirstIds().FirstOrDefault())
            : SelectedRevisionText + describe(items.SecondIds().FirstOrDefault());
        return (revision, hasExistingFiles, hasNewFiles);
    }

    /// <summary>
    ///  Upstream's <c>ResetSelectedItemsTo</c>: the selected files as they are in the first or the selected revision.
    ///  Returns git's error output, empty when it worked.
    /// </summary>
    public static Task<string> ResetAsync(DiffMenuSelection selection, string workingDir, bool toFirst, bool resetAndDelete)
        => Task.Run(() =>
        {
            // Upstream's ResetSelectedItemsTo: the selected files to each of their revisions that can be reset to.
            FileStatusItem[] items = Items(selection);
            GitModule module = GitModules.Open(workingDir);
            StringBuilder errors = new();
            foreach (ObjectId id in toFirst ? items.FirstIds() : items.SecondIds())
            {
                if (toFirst ? !CanResetToFirst(id, items) : !CanResetToSecond(id))
                {
                    continue;
                }

                GitItemStatus[] resetItems = toFirst
                    ? [.. items.Items()]
                    : [.. items.Items().Select(item => item.InvertStatus())];
                module.ResetChanges(id, resetItems, resetAndDelete, new FullPathResolver(() => workingDir),
                    out StringBuilder output);
                errors.Append(output);
            }

            return errors.ToString();
        });

    /// <summary>
    ///  Upstream's <c>OpenFilesWithDiffTool</c>: each selected file in the difftool, between the revisions
    ///  <paramref name="kind"/> names (upstream's <c>RevisionDiffInfoProvider</c>). Returns the first error, or
    ///  <see langword="null"/>.
    /// </summary>
    public static Task<string?> OpenWithDifftoolAsync(DiffMenuSelection selection, string workingDir, RevisionDiffKind kind,
        string? tool = null)
        => Task.Run(() =>
        {
            GitModule module = GitModules.Open(workingDir);
            foreach (FileStatusItem item in Items(selection))
            {
                GitRevision?[] revisions = [item.SecondRevision, item.FirstRevision];
                if (!RevisionDiffInfoProvider.TryGet(revisions, kind, out string? first, out string? second,
                        out string? error))
                {
                    return error;
                }

                module.OpenWithDifftool(item.Item.Name, item.Item.OldName, first, second, isTracked: item.Item.IsTracked,
                    customTool: tool);
            }

            return (string?)null;
        });

    /// <summary>
    ///  Upstream's <c>SaveSelectedItemToTempFile</c>: the first selected file as it is in the selected revision, saved in
    ///  the temporary folder under its own name. Returns the file's path, or <see langword="null"/> when the revision has no
    ///  such file.
    /// </summary>
    public static Task<string?> SaveToTempFileAsync(DiffMenuSelection selection, string workingDir)
        => Task.Run(async () =>
        {
            if (selection.Files.FirstOrDefault() is not { } file)
            {
                return null;
            }

            GitModule module = GitModules.Open(workingDir);
            ObjectId blob = module.GetFileBlobHash(file.Path, RevisionsOf(selection, file).Second);
            if (blob.IsZero)
            {
                return null;
            }

            string fileName = Path.Combine(Path.GetTempPath(), PathUtil.GetFileName(file.Path)).ToNativePath();
            await module.SaveBlobAsAsync(fileName, blob.ToString(), CancellationToken.None);
            return (string?)fileName;
        });

    /// <summary>
    ///  Where upstream's "Save selected as..." starts: the file's own path for one file, the folder the files share for
    ///  several (upstream's <c>GetLongestCommonPath</c>).
    /// </summary>
    public static string SaveSuggestion(DiffMenuSelection selection, string workingDir)
    {
        IReadOnlyList<DiffMenuFile> files = selection.Files;
        if (files.Count == 1)
        {
            return Path.Combine(workingDir, files[0].Path).ToNativePath();
        }

        string firstFile = files[0].Path;
        for (int length = files.Min(file => file.Path.Length); length > 0; --length)
        {
            string possibleMatch = firstFile[..length];
            if (files.All(file => file.Path.StartsWith(possibleMatch, StringComparison.Ordinal)))
            {
                string common = Path.GetDirectoryName(possibleMatch) ?? "";
                return Path.Combine(workingDir, common).ToNativePath();
            }
        }

        return workingDir;
    }

    /// <summary>
    ///  Upstream's <c>RevisionDiffController.SaveFiles</c>: one file to <paramref name="target"/>, or several into the folder
    ///  <paramref name="target"/>, keeping their folders below the one they share.
    /// </summary>
    public static Task SaveAsync(DiffMenuSelection selection, string workingDir, string target)
        => Task.Run(() =>
        {
            GitModule module = GitModules.Open(workingDir);
            RevisionDiffController controller = new(() => module, new FullPathResolver(() => workingDir));
            controller.SaveFiles([.. Items(selection)], _ => target);
        });

    /// <summary>
    ///  The revisions <paramref name="file"/> is shown between: its own, else the selection's.
    /// </summary>
    public static DiffMenuRevisions RevisionsOf(DiffMenuSelection selection, DiffMenuFile file)
        => file.Revisions ?? new DiffMenuRevisions(selection.First, selection.Second, selection.SecondParents);

    // Upstream's CanResetToSecond and CanResetToFirst.
    private static bool CanResetToSecond(ObjectId id) => !id.IsZeroOrArtificial;

    private static bool CanResetToFirst(ObjectId first, IEnumerable<FileStatusItem> items)
        => CanResetToSecond(first)
           || (first == ObjectId.IndexId && items.SecondIds().All(id => id == ObjectId.WorkTreeId));

    // Upstream's DescribeRevisions: one revision by its description, several as "<multiple>".
    private static string DescribeRevisions(IReadOnlyList<ObjectId> ids, Func<ObjectId, string> describe)
        => ids.Count switch
        {
            1 => describe(ids[0]),
            > 1 => MultipleDescription,
            _ => ""
        };

    // The selection as upstream's FileStatusItems: the files' git status as FileStatusList reads it from git. Files shown
    // between the same revisions share the revisions' objects, as FileStatusList's do (upstream tells revisions apart by
    // reference).
    private static FileStatusItem[] Items(DiffMenuSelection selection)
    {
        Dictionary<(ObjectId? First, ObjectId Second), (GitRevision? First, GitRevision Second)> revisions = [];
        return [.. selection.Files.Select(Item)];

        FileStatusItem Item(DiffMenuFile file)
        {
            DiffMenuRevisions pair = RevisionsOf(selection, file);
            if (!revisions.TryGetValue((pair.First, pair.Second), out (GitRevision? First, GitRevision Second) shared))
            {
                shared = (pair.First is { } id ? new GitRevision(id) : null,
                    new GitRevision(pair.Second) { ParentIds = pair.SecondParents });
                revisions[(pair.First, pair.Second)] = shared;
            }

            StagedStatus staged = pair.Second == ObjectId.WorkTreeId ? StagedStatus.WorkTree
                : pair.Second == ObjectId.IndexId ? StagedStatus.Index
                : StagedStatus.None;
            return new FileStatusItem(shared.First, shared.Second, StatusOf(file, staged));
        }
    }

    private static GitItemStatus StatusOf(DiffMenuFile file, StagedStatus staged)
    {
        GitItemStatus status = new(file.Path)
        {
            OldName = file.OldPath,
            Staged = staged,
            IsTracked = file.IsTracked
        };
        switch (file.Status.FirstOrDefault())
        {
            case 'A' or '?':
                status.IsNew = true;
                break;
            case 'D':
                status.IsDeleted = true;
                break;
            case 'R':
                status.IsRenamed = true;
                break;
            case 'C':
                status.IsCopied = true;
                break;
            case 'U':
                status.IsUnmerged = true;
                status.IsChanged = true;
                break;
            default:
                status.IsChanged = true;
                break;
        }

        return status;
    }

    // Upstream's GetSelectionInfo; line patching ("Cherry pick changes") is not offered in the Diff tab here.
    private static ContextMenuSelectionInfo SelectionInfo(FileStatusItem[] items, bool isBareRepository,
        IFullPathResolver resolver)
    {
        GitRevision? selectedRevision = items.FirstOrDefault()?.SecondRevision;
        bool allFilesExist = items.Length != 0;
        bool allDirectoriesExist = allFilesExist;
        bool allFilesOrUntrackedDirectoriesExist = allFilesExist;
        foreach (FileStatusItem item in items)
        {
            string? path = resolver.Resolve(item.Item.Name);
            bool fileExists = File.Exists(path);
            bool directoryExists = Directory.Exists(path);
            allFilesExist &= fileExists;
            allDirectoriesExist &= directoryExists;
            allFilesOrUntrackedDirectoriesExist &= fileExists || (!item.Item.IsTracked && allDirectoriesExist);
        }

        return new ContextMenuSelectionInfo(
            SelectedRevision: selectedRevision,
            SelectedFolder: null,
            IsDisplayOnlyDiff: false,
            IsStatusOnly: false,
            SelectedGitItemCount: items.Length,
            IsAnyItemIndex: items.Any(item => item.Item.Staged == StagedStatus.Index),
            IsAnyItemWorkTree: items.Any(item => item.Item.Staged == StagedStatus.WorkTree),
            IsBareRepository: isBareRepository,
            AllFilesExist: allFilesExist,
            AllDirectoriesExist: allDirectoriesExist,
            AllFilesOrUntrackedDirectoriesExist: allFilesOrUntrackedDirectoriesExist,
            IsAnyTracked: items.Any(item => item.Item.IsTracked),
            SupportPatches: false,
            IsDeleted: items.Any(item => item.Item.IsDeleted),
            IsAnySubmodule: items.Any(item => item.Item.IsSubmodule));
    }

    // Upstream's GetContextMenuDiffToolInfo.
    private static ContextMenuDiffToolInfo DiffToolInfo(DiffMenuSelection selection, FileStatusItem[] items,
        IFullPathResolver resolver)
    {
        // Some items are not supported if more than one revision is selected.
        List<GitRevision> revisions = [.. items.SecondRevs()];
        GitRevision? selectedRevision = revisions.Count == 1 ? revisions[0] : null;
        List<ObjectId> parentIds = [.. items.FirstIds()];
        GitRevisionTester tester = new(resolver);
        return new ContextMenuDiffToolInfo(
            selectedRevision: selectedRevision,
            selectedItemParentRevs: parentIds,
            allAreNew: items.All(item => item.Item.IsNew),
            allAreDeleted: items.All(item => item.Item.IsDeleted),
            firstIsParent: tester.AllFirstAreParentsToSelected(parentIds, selectedRevision),
            localExists: tester.AnyLocalFileExists(items.Select(item => item.Item)));
    }
}

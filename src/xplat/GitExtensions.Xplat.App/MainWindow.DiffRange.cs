using GitCommands;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Diff;
using GitExtensions.Xplat.Core.Repository;
using GitUIPluginInterfaces;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's Diff tab (<c>RevisionDiffControl</c>) as upstream's <c>FileStatusDiffCalculator</c> fills it (core
///  <see cref="DiffGroups"/>): for several selected commits, and for a merge, the files in groups (each parent, the
///  combined diff, A/B/BASE), each file's diff, diff tool and window between its group's revisions. A commit with one parent
///  and the artificial rows show their own changes, as before.
/// </summary>
public partial class MainWindow
{
    // The groups the Diff tab shows; null while it shows the selected commit's own files.
    private IReadOnlyList<DiffGroup>? _diffGroups;
    private int _diffGroupsVersion;
    private RevisionFileList<CommitFile>? _diffFiles;

    // Upstream's FileStatusList over the Diff tab's files.
    private void WireDiffFileList()
        => _diffFiles = new RevisionFileList<CommitFile>(CommitFilesList, DiffFilesFilterBox, DiffFilesByPathButton,
            DiffFilesByExtensionButton, DiffFilesByStatusButton, DiffFilesNoFilesText, _preferences, file => file.Path,
            file => file.Status, file => file.Group, new FileListTreeButtons(DiffFilesCollapseGroupsButton,
                DiffFilesAsTreeButton,
                [
                    (DiffFilesPathTreeMenuItem, DiffListSortType.FilePath),
                    (DiffFilesPathFlatMenuItem, DiffListSortType.FilePathFlat),
                    (DiffFilesExtensionTreeMenuItem, DiffListSortType.FileExtension),
                    (DiffFilesExtensionFlatMenuItem, DiffListSortType.FileExtensionFlat),
                    (DiffFilesStatusTreeMenuItem, DiffListSortType.FileStatus),
                    (DiffFilesStatusFlatMenuItem, DiffListSortType.FileStatusFlat),
                ],
                DiffFilesDenseTreeMenuItem, DiffFilesGroupNodesInFlatListMenuItem),
            file => file.DiffFile?.DiffStatus ?? DiffBranchStatus.Unknown,
            new DiffStatusButtons(DiffFilesUnequalChangeButton, DiffFilesOnlyBButton, DiffFilesOnlyAButton,
                DiffFilesSameChangeButton, DiffFilesStatusFilterSeparator), oldPath: file => file.DiffFile?.OldPath);

    // Called on each selection change, after the selection is tracked.
    private void ShowSelectionDiff()
    {
        int version = ++_diffGroupsVersion;
        IReadOnlyList<CommitRow> selected = SelectedRevisions;
        string grep = _gitGrep;
        bool showSkipWorktree = DiffFilesShowSkipWorktreeMenuItem.IsChecked;
        bool showUntracked = DiffFilesShowUntrackedMenuItem.IsChecked;
        if (RepositoryPath is not { } path || !(UsesDiffGroups(selected) || (grep.Length > 0 && selected.Count > 0)
                || (showSkipWorktree && selected.Any(row => row.Hash == ArtificialCommits.WorkTreeHash))))
        {
            if (_diffGroups is not null)
            {
                _diffGroups = null;
                _diffFiles?.Files = SelectedCommitFiles();
            }

            return;
        }

        IReadOnlyList<GitRevision> revisions = [.. selected.Select(Revision)];
        Func<ObjectId, string> describe = RevisionDescriber();
        _diffGroups = [];
        _diffFiles?.Files = null;
        Run(async () =>
        {
            IReadOnlyList<DiffGroup> groups = await Task.Run(() =>
            {
                GitModule module = GitModules.Open(path);
                return DiffGroups.Calculate(module, revisions, module.GetCurrentCheckout(), describe, grep: grep,
                    showSkipWorktreeFiles: showSkipWorktree, showUntrackedFiles: showUntracked);
            });
            if (version != _diffGroupsVersion)
            {
                return;
            }

            _diffGroups = groups;
            _diffFiles?.Files =
            [
                .. groups.SelectMany(group => group.Files.Select(file =>
                    new CommitFile(file.Status, file.Path) { Group = group, DiffFile = file }))
            ];
        });
    }

    // Upstream's ViewChangesAsync: range-diff has cubic runtime and can be slow, so the view says what is going on, and
    // selecting something else cancels it.
    private async Task ShowRangeDiffAsync(DiffView view, string path, DiffGroup group, RevisionDiffFile file)
    {
        _rangeDiffCancellation?.Cancel();
        using CancellationTokenSource cancellation = new();
        _rangeDiffCancellation = cancellation;
        string filter = _commits.Filter.PathFilter;
        view.ShowText(file.Path, $"git range-diff {group.First}...{group.Second} -- {filter}");
        try
        {
            IReadOnlyList<DiffLine> lines = await Task.Run(() =>
                DiffGroups.RangeDiffAsync(GitModules.Open(path), group, view.Options, filter, cancellation.Token));
            if (!cancellation.IsCancellationRequested)
            {
                view.ShowDiffLines(file.Path, lines);
            }
        }
        finally
        {
            if (_rangeDiffCancellation == cancellation)
            {
                _rangeDiffCancellation = null;
            }
        }
    }

    // Upstream's calculator decides the groups for several commits and for a merge (a stash included); one commit with a
    // single parent, and an artificial row alone, keep their own list, which is the calculator's one group.
    private static bool UsesDiffGroups(IReadOnlyList<CommitRow> selected)
        => selected.Count > 1
           || (selected is [{ } row] && !ArtificialCommits.IsArtificial(row.Hash) && row.ParentHashes?.Count > 1);

    // Upstream's GitRevision of a grid row: its id and its parents.
    private static GitRevision Revision(CommitRow row)
        => new(ObjectId.Parse(row.Hash))
        {
            ParentIds =
            [
                .. (row.ParentHashes ?? []).Select(hash => ObjectId.TryParse(hash, out ObjectId id) ? id : default)
                .Where(id => !id.IsZero)
            ],
        };

    // A file of a group, with the group it is shown in.
    private static (DiffGroup Group, RevisionDiffFile File)? GroupFile(CommitFile file)
        => file is { Group: { } group, DiffFile: { } diffFile } ? (group, diffFile) : null;

    // A file of the "grep" group shows its matches (upstream's ViewGrepAsync), any other file its diff.
    private async Task ShowGroupFileDiffAsync(DiffView view, string path, DiffGroup group, RevisionDiffFile file)
    {
        if (group.IsRangeDiff)
        {
            await ShowRangeDiffAsync(view, path, group, file);
            return;
        }

        if (file.GrepString is { } grep)
        {
            await view.ShowGrepAsync(file.Path, options => Task.Run(() =>
                GitGrep.FileAsync(GitModules.Open(path), group.Second, file.Path, grep, options)));
            return;
        }

        await view.ShowDiffAsync(path, file.Path, file.Path,
            options => DiffGroups.DiffAsync(GitModules.Open(path), group, file, options), FileRevision.Of(group.Second));
    }

    // Upstream's difftool for a file of a group: between its group's revisions (none for the combined diff).
    private static Task OpenGroupFileInDiffToolAsync(string path, DiffGroup group, RevisionDiffFile file, string? tool)
        => Task.Run(() => GitModules.Open(path).OpenWithDifftool(file.Path, file.OldPath ?? "",
            group.First?.ToString() ?? "", group.Second.ToString(), extraDiffArguments: null, file.IsTracked,
            customTool: tool));
}

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
                DiffFilesDenseTreeMenuItem, DiffFilesGroupNodesInFlatListMenuItem));

    // Called on each selection change, after the selection is tracked.
    private void ShowSelectionDiff()
    {
        int version = ++_diffGroupsVersion;
        IReadOnlyList<CommitRow> selected = SelectedRevisions;
        if (RepositoryPath is not { } path || !UsesDiffGroups(selected))
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
                return DiffGroups.Calculate(module, revisions, module.GetCurrentCheckout(), describe);
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

    private static Task ShowGroupFileDiffAsync(DiffView view, string path, DiffGroup group, RevisionDiffFile file)
        => view.ShowDiffAsync(path, file.Path, file.Path,
            options => DiffGroups.DiffAsync(GitModules.Open(path), group, file, options));

    // Upstream's difftool for a file of a group: between its group's revisions (none for the combined diff).
    private static Task OpenGroupFileInDiffToolAsync(string path, DiffGroup group, RevisionDiffFile file, string? tool)
        => Task.Run(() => GitModules.Open(path).OpenWithDifftool(file.Path, file.OldPath ?? "",
            group.First?.ToString() ?? "", group.Second.ToString(), extraDiffArguments: null, file.IsTracked,
            customTool: tool));
}

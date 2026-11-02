using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Diff;
using GitExtensions.Xplat.Core.Repository;

namespace GitExtensions.Xplat.App;

/// <summary>
///  The Diff tab's file list menu (<see cref="FileStatusListMenu"/>) bound as upstream's <c>RevisionDiffControl</c> binds
///  it: with the File tree tab and the grid's filter.
/// </summary>
public partial class MainWindow
{
    private FileListMenuServices? _fileListMenuServices;

    /// <summary>
    ///  The git actions and windows of this window that file list menus use.
    /// </summary>
    private FileListMenuServices FileListMenuServices
        => _fileListMenuServices ??= new FileListMenuServices(_actions,
            (path, file, revision, showBlame) => NewFileHistoryWindow(path, file, revision, showBlame));

    private void WireDiffFileMenu()
        => CommitFilesContextMenu.Bind(new FileStatusListMenuBinding
        {
            Owner = this,
            RepositoryPath = () => RepositoryPath,
            Selection = DiffMenuSelection,
            DescribeRevision = DescribeRevision,
            Services = () => FileListMenuServices,
            Refresh = RefreshRepository,
            ShowInFileTree = file =>
            {
                DetailTabs.SelectedItem = FileTreeTab;
                SelectInFileTree(file);
            },

            // The File tree tab shows a commit's files, not the working directory's or the index's.
            CanShowInFileTree = selection => !selection.Second.IsArtificial && _diffRange is null,
            FilterFileInGrid = file => ApplyFilter(_commits.Filter with { PathFilter = file }),
            RunScript = script =>
                RunOnRepository(path => Scripts.RunAsync(script, path, SelectedHashes, FileScriptOptions())),
        });

    // The Diff tab's selected files with the revisions they are shown between: the selected range while several commits
    // are selected, otherwise the selected row against its first parent (the index for the working directory).
    private DiffMenuSelection? DiffMenuSelection()
    {
        List<CommitFile> files = [.. CommitFilesList.SelectedItems?.OfType<CommitFile>() ?? []];
        if (_diffRange is { } range)
        {
            return new DiffMenuSelection(range.First, range.Selected, [],
            [
                .. files.Select(file => _rangeFiles.TryGetValue(file.Path, out RevisionDiffFile? found)
                    ? new DiffMenuFile(found.Status, found.Path, found.OldPath, found.IsTracked)
                    : new DiffMenuFile(file.Status, file.Path, null, IsTracked: file.Status != "?"))
            ]);
        }

        if (_commits.Selected is not { } row || !ObjectId.TryParse(row.Hash, out ObjectId second))
        {
            return null;
        }

        List<ObjectId> parents =
        [
            .. (row.ParentHashes ?? []).Select(hash => ObjectId.TryParse(hash, out ObjectId id) ? id : default)
            .Where(id => !id.IsZero)
        ];
        ObjectId? first = second == ObjectId.WorkTreeId ? ObjectId.IndexId
            : parents.Count > 0 ? parents[0]
            : null;
        return new DiffMenuSelection(first, second, parents,
            [.. files.Select(file => new DiffMenuFile(file.Status, file.Path, null, IsTracked: file.Status != "?"))]);
    }

    // Upstream RevisionDiffControl's DescribeRevision: the working directory for none; the artificial rows by their
    // subject; a commit by its short hash and its first branch or tag, or its subject.
    private string DescribeRevision(ObjectId id)
    {
        if (id.IsZero)
        {
            return ResourceManager.TranslatedStrings.Workspace;
        }

        string hash = id.ToString();
        if (_commits.VisibleRows.FirstOrDefault(item => item.Row.Hash == hash)?.Row is not { } row)
        {
            return id.ToShortString();
        }

        string description = id.IsArtificial ? "" : id.ToShortString() + ": ";
        RefLabel? label =
            (row.Labels ?? []).FirstOrDefault(label => label.Kind is RefKind.Branch or RefKind.RemoteBranch)
            ?? (row.Labels ?? []).FirstOrDefault(label => label.Kind == RefKind.Tag);
        return description + (label?.Name ?? row.Subject);
    }
}

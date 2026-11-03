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
            CanShowInFileTree = selection => !selection.Second.IsArtificial && SelectedRevisions.Count == 1,
            FilterFileInGrid = file => ApplyFilter(_commits.Filter with { PathFilter = file }),
            RunScript = script =>
                RunOnRepository(path => Scripts.RunAsync(script, path, SelectedHashes, FileScriptOptions())),
        });

    // The Diff tab's selected files with the revisions they are shown between: each file's diff group (upstream's
    // FileStatusItem) while the tab shows groups, otherwise the selected row against its first parent (the index for the
    // working directory).
    private DiffMenuSelection? DiffMenuSelection()
    {
        List<CommitFile> files = [.. CommitFilesList.SelectedItems?.OfType<CommitFile>() ?? []];
        if (_diffGroups is not null)
        {
            if (files is not [{ Group: { } firstGroup }, ..])
            {
                return null;
            }

            return new DiffMenuSelection(firstGroup.First, firstGroup.Second, ParentsOf(firstGroup.Second),
            [
                .. files.Select(file => file.Group is { } group
                        ? new DiffMenuFile(file.Status, file.Path, file.DiffFile?.OldPath, file.DiffFile?.IsTracked ?? true,
                            new DiffMenuRevisions(group.First, group.Second, ParentsOf(group.Second)))
                        : null)
                    .OfType<DiffMenuFile>()
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

    // The parents of a grid row, as upstream's GitRevision has them.
    private IReadOnlyList<ObjectId> ParentsOf(ObjectId id)
    {
        string hash = id.ToString();
        return
        [
            .. (_commits.VisibleRows.FirstOrDefault(item => item.Row.Hash == hash)?.Row.ParentHashes ?? [])
                .Select(parent => ObjectId.TryParse(parent, out ObjectId parentId) ? parentId : default)
                .Where(parentId => !parentId.IsZero)
        ];
    }

    // Upstream RevisionDiffControl's DescribeRevision: the working directory for none; the artificial rows by their
    // subject; a commit by its short hash and its first branch or tag, or its subject.
    private string DescribeRevision(ObjectId id) => RevisionDescriber()(id);

    // DescribeRevision over the grid's rows as they are now, for a calculation off the UI thread.
    private Func<ObjectId, string> RevisionDescriber()
    {
        Dictionary<string, CommitRow> rows = [];
        foreach (CommitListItem item in _commits.VisibleRows)
        {
            rows.TryAdd(item.Row.Hash, item.Row);
        }

        return id => Describe(id, rows);
    }

    private static string Describe(ObjectId id, IReadOnlyDictionary<string, CommitRow> rows)
    {
        if (id.IsZero)
        {
            return ResourceManager.TranslatedStrings.Workspace;
        }

        if (!rows.TryGetValue(id.ToString(), out CommitRow? row))
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

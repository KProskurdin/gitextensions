using Avalonia.Controls;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Diff;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Ui;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;

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
            (path, file, revision, showBlame) => NewFileHistoryWindow(path, file, revision, showBlame),
            OfferToResolveConflictsAsync);

    private void WireDiffFileMenu()
        => CommitFilesContextMenu.Bind(new FileStatusListMenuBinding
        {
            Owner = this,
            RepositoryPath = () => RepositoryPath,
            Selection = DiffMenuSelection,
            DescribeRevision = DescribeRevision,
            Services = () => FileListMenuServices,
            Refresh = RefreshRepository,
            ShowInFileTree = file => Run(() => ShowInFileTreeAsync(file, blame: false)),
            Blame = BlameDiffFile,
            BlameChecked = () => _diffTabBlameFile is not null,

            // The File tree tab shows a commit's files, not the working directory's or the index's.
            CanShowInFileTree = selection => !selection.Second.IsArtificial && SelectedRevisions.Count == 1,
            FilterFileInGrid = file => ApplyFilter(_commits.Filter with { PathFilter = file }),
            RunScript = script =>
                RunOnRepository(path => Scripts.RunAsync(script, path, SelectedHashes, FileScriptOptions())),
            FocusedPath = () => (FileStatusListMenu.FocusedItem(CommitFilesList) as CommitFile)?.Path,
            FindFile = new FileListFind(
                () => [.. (_diffFiles?.Files ?? []).Select(file => (file.Path, file.DiffFile?.OldPath))],
                file => _diffFiles?.SelectPath(file)),
            Tree = _diffFiles?.MenuTree,
            SelectFirstGroup = () => _diffFiles?.SelectFirstGroupFiles(),
            Viewer = () => CommitDiff,
            GitGrep = _diffGitGrep,
            Submodules = () => DiffFileSubmodules(() => this),
            CurrentCheckout = () => _commits.VisibleRows.FirstOrDefault(item => item.IsCheckedOut)?.Row.Hash,
        });

    // Upstream FileStatusList's submodule items over the left panel's submodule commands: open in a new instance, update,
    // reset after upstream's question (asked once), stash and commit, each in the submodule's repository.
    private FileListSubmodules DiffFileSubmodules(Func<Window> owner)
    {
        return new FileListSubmodules(
            Open: async path =>
            {
                if (FullPath(path) is not { } full)
                {
                    return;
                }

                if (!Directory.Exists(full))
                {
                    await new MessageBoxWindow(
                        string.Format(UpstreamTranslation.ByText(SubmoduleDirectoryDoesNotExistText), full, path),
                        UpstreamTranslation.ByText(CannotOpenSubmoduleCaption), MessageBoxButtons.OK).ShowDialog(owner());
                    return;
                }

                if (Environment.ProcessPath is { } executable)
                {
                    _launcher.Start(executable, [full], full);
                }
            },
            Update: async paths =>
            {
                if (RepositoryPath is { } repository)
                {
                    foreach (string path in paths)
                    {
                        await InSubmoduleRepositoryAsync(repository, super => UpdateSubmodulesAsync(super, path));
                    }
                }
            },
            Reset: async paths =>
            {
                ResetChangesWindow.ResetAction action = await ResetChangesWindow.ShowResetDialogAsync(owner(),
                    hasExistingFiles: true, hasNewFiles: true);
                if (action == ResetChangesWindow.ResetAction.Cancel)
                {
                    return;
                }

                foreach (string full in paths.Select(FullPath).OfType<string>())
                {
                    await InSubmoduleRepositoryAsync(full, submodule => _actions.ResetAllChangesAsync(submodule,
                        clean: action == ResetChangesWindow.ResetAction.ResetAndDelete, onlyWorkTree: false));
                }
            },
            Stash: async paths =>
            {
                foreach (string full in paths.Select(FullPath).OfType<string>())
                {
                    await InSubmoduleRepositoryAsync(full, submodule => _actions.StashAsync(submodule, "",
                        _preferences.IncludeUntrackedFilesInManualStash, keepIndex: false));
                }
            },
            Commit: async paths =>
            {
                foreach (string full in paths.Select(FullPath).OfType<string>())
                {
                    await CommitSubmoduleAsync(full);
                }
            });

        string? FullPath(string path)
            => RepositoryPath is { } repository
                ? Path.Combine(repository, path.Replace('/', Path.DirectorySeparatorChar))
                : null;
    }

    // Upstream RevisionDiffControl's BlameFile: the file's blame in the File tree tab. Where the File tree tab cannot show
    // the file (the working directory, the index, several commits), the file history window's Blame tab.
    private void BlameDiffFile()
    {
        if (RepositoryPath is not { } path
            || DiffMenuSelection() is not { Files: [{ IsTracked: true } file, ..] } selection)
        {
            return;
        }

        if (_preferences.UseDiffViewerForBlame)
        {
            ToggleDiffTabBlame(file.Path);
            return;
        }

        ObjectId second = DiffFileMenu.RevisionsOf(selection, file).Second;
        if (!second.IsArtificial && SelectedRevisions.Count == 1)
        {
            Run(() => ShowInFileTreeAsync(file.Path, blame: true));
        }
        else
        {
            NewFileHistoryWindow(path, file.Path, second.IsArtificial ? null : second.ToString(), showBlame: true)
                .Show(this);
        }
    }

    // The Diff tab's selected files with the revisions they are shown between: each file's diff group (upstream's
    // FileStatusItem) while the tab shows groups, otherwise the selected row against its first parent (the index for the
    // working directory).
    private DiffMenuSelection? DiffMenuSelection()
    {
        // Upstream's SelectedItems: a single selected folder, group or diff group node stands for the files below it.
        IFileListNode? node = FileListNodes.SelectedNode(CommitFilesList);
        List<CommitFile> files =
            [.. (node?.Files ?? CommitFilesList.SelectedItems?.Cast<object>() ?? []).OfType<CommitFile>()];
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
                        {
                            IsSubmodule = file.IsSubmodule || file.DiffFile?.IsSubmodule == true,
                            IsRangeDiff = file.DiffFile?.IsRangeDiff == true
                        }
                        : null)
                    .OfType<DiffMenuFile>()
            ], node?.Folder);
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
            [
                .. files.Select(file => new DiffMenuFile(file.Status, file.Path, null, IsTracked: file.Status != "?")
                {
                    IsSubmodule = file.IsSubmodule
                })
            ],
            node?.Folder);
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

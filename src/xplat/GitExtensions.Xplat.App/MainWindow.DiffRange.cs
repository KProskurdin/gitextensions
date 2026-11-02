using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Diff;
using GitExtensions.Xplat.Core.Repository;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's Diff tab (<c>RevisionDiffControl</c>) for several selected commits: the files changed from the earliest
///  selected commit to the latest (<c>GetFirstAndSelected</c>), each file's diff between them, its diff tool and its own
///  diff window. One selected commit shows its own changes, as before.
/// </summary>
public partial class MainWindow
{
    // The first and the selected commit while several are selected; null while the Diff tab shows one commit.
    private (ObjectId First, ObjectId Selected)? _diffRange;
    private IReadOnlyDictionary<string, RevisionDiffFile> _rangeFiles = new Dictionary<string, RevisionDiffFile>();
    private int _rangeVersion;
    private RevisionFileList<CommitFile>? _diffFiles;

    // Upstream's FileStatusList over the Diff tab's files.
    private void WireDiffFileList()
        => _diffFiles = new RevisionFileList<CommitFile>(CommitFilesList, DiffFilesFilterBox, DiffFilesByPathButton,
            DiffFilesByExtensionButton, DiffFilesByStatusButton, DiffFilesNoFilesText, _preferences, file => file.Path,
            file => file.Status);

    // Called on each selection change, after the selection is tracked.
    private void ShowSelectionDiff()
    {
        int version = ++_rangeVersion;
        (string? first, CommitRow? selected) = GridSelection.FirstAndSelected(SelectedRevisions);
        if (SelectedRevisions.Count < 2 || RepositoryPath is not { } path || first is null || selected is null
            || !ObjectId.TryParse(first, out ObjectId firstId) ||
            !ObjectId.TryParse(selected.Hash, out ObjectId selectedId))
        {
            if (_diffRange is not null)
            {
                _diffRange = null;
                _rangeFiles = new Dictionary<string, RevisionDiffFile>();
                _diffFiles?.Files = _commits.CommitFiles;
            }

            return;
        }

        _diffRange = (firstId, selectedId);
        _rangeFiles = new Dictionary<string, RevisionDiffFile>();
        _diffFiles?.Files = null;
        Run(async () =>
        {
            IReadOnlyList<RevisionDiffFile> files =
                await Task.Run(() => RevisionDiff.Files(GitModules.Open(path), firstId, selectedId));
            if (version != _rangeVersion)
            {
                return;
            }

            _rangeFiles = files.GroupBy(file => file.Path).ToDictionary(group => group.Key, group => group.First());
            _diffFiles?.Files = [.. files.Select(file => new CommitFile(file.Status, file.Path))];
        });
    }

    // The range and the file's diff details, when the Diff tab shows several commits.
    private ((ObjectId First, ObjectId Selected) Range, RevisionDiffFile File)? RangeFile(CommitFile file)
        => _diffRange is { } range && _rangeFiles.TryGetValue(file.Path, out RevisionDiffFile? found)
            ? (range, found)
            : null;

    private static Task ShowRangeFileDiffAsync(DiffView view, string path, (ObjectId First, ObjectId Selected) range,
        RevisionDiffFile file)
        => view.ShowDiffAsync(path, file.Path, file.Path,
            options => RevisionDiff.DiffAsync(GitModules.Open(path), range.First, range.Selected, file, options));

    // Upstream's difftool for a file of the selection: from the first to the selected commit.
    private static Task OpenRangeFileInDiffToolAsync(string path, (ObjectId First, ObjectId Selected) range,
        RevisionDiffFile file, string? tool)
        => Task.Run(() => GitModules.Open(path).OpenWithDifftool(file.Path, file.OldPath ?? "", range.First.ToString(),
            range.Selected.ToString(), extraDiffArguments: null, file.IsTracked, customTool: tool));
}

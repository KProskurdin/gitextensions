using Avalonia.Controls;
using GitCommands;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Diff;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's grid Compare submenu (<c>RevisionGridControl</c>'s compare items and their hotkeys): the selected commit
///  compared to a branch, the current branch, a commit selected as BASE or the working directory, in upstream's
///  <c>FormDiff</c> (<see cref="RevisionDiffWindow"/>); and the selection (upstream's <c>GetFirstAndSelected</c>: a commit
///  and its first parent, or the earliest and the latest selected of several) compared in that window or opened in the
///  directory diff tool.
/// </summary>
public partial class MainWindow
{
    // Upstream's _baseCommitToCompare, with the repository it belongs to.
    private (string Path, CommitRow Row)? _baseCommitToCompare;

    private void WireCompareMenu()
    {
        CompareToBranchMenuItem.Click += (_, _) => CompareToBranch();
        CompareWithCurrentBranchMenuItem.Click += (_, _) => CompareWithCurrentBranch();
        SelectAsBaseMenuItem.Click += (_, _) => SelectAsBaseToCompare();
        CompareToBaseMenuItem.Click += (_, _) => CompareToBase();
        CompareToWorkingDirectoryMenuItem.Click += (_, _) => CompareToWorkingDirectory();
        CompareSelectedCommitsMenuItem.Click += (_, _) => CompareSelectedCommits();

        // Upstream's diffSelectedCommitsMenuItem_Click: the item opens the default tool, its submenu (when there are
        // several tools) a chosen one; a click on a sub-item also reaches the item, hence the source check.
        OpenCommitsWithDiffToolMenuItem.Click += (_, e) =>
        {
            if (e.Source == OpenCommitsWithDiffToolMenuItem)
            {
                DiffSelectedCommitsWithDifftool(tool: null);
            }
        };
    }

    private bool _gridDiffToolsLoaded;

    // Upstream's LoadCustomDifftools (CustomDiffMergeToolProvider): the diff tools under the item when there are several
    // and "Show all available difftools" is on, the default bold, then "Disable this dropdown".
    private Task LoadGridDiffToolsAsync()
    {
        _gridDiffToolsLoaded = true;
        return CustomDiffTools.LoadAsync(
            [(OpenCommitsWithDiffToolMenuItem, tool => DiffSelectedCommitsWithDifftool(tool))], diff: true, _preferences);
    }

    // Upstream's OnGridViewSelectionChanged settings of the compare items, for the selected commit.
    private void ShowCompareMenu(CommitRow row)
    {
        CompareToWorkingDirectoryMenuItem.IsEnabled = row.Hash != ArtificialCommits.WorkTreeHash;
        CompareWithCurrentBranchMenuItem.IsEnabled = _repository.CurrentBranch.Length > 0;
        (string? first, CommitRow? selected) = GridSelection.FirstAndSelected(SelectedRevisions);
        CompareSelectedCommitsMenuItem.IsEnabled = first is not null && selected is not null;
        OpenCommitsWithDiffToolMenuItem.IsEnabled = first is not null && selected is not null;
        CompareToBaseMenuItem.IsEnabled = BaseToCompare() is not null;
        if (!_gridDiffToolsLoaded)
        {
            Run(LoadGridDiffToolsAsync);
        }
    }

    // Upstream's compareSelectedCommitsMenuItem_Click: the first and the selected commit in the diff window, the first
    // shown by its subject when the grid has it.
    private bool CompareSelectedCommits()
    {
        if (RepositoryPath is not { } path)
        {
            return false;
        }

        (string? first, CommitRow? selected) = GridSelection.FirstAndSelected(SelectedRevisions);
        if (selected is null || first is null || !ObjectId.TryParse(first, out ObjectId firstId)
            || !ObjectId.TryParse(selected.Hash, out ObjectId selectedId))
        {
            Run(() => ShowCompareErrorAsync(RevisionDiff.TwoCommitsToCompare));
            return true;
        }

        string firstSubject = _commits.VisibleRows.FirstOrDefault(item => item.Row.Hash == first)?.Row.Subject ?? "";
        ShowRevisionDiff(path, firstId, selectedId, firstSubject, selected.Subject);
        return true;
    }

    // Upstream's DiffSelectedCommitsWithDifftool: the directory diff tool from the first to the selected commit.
    private bool DiffSelectedCommitsWithDifftool(string? tool)
    {
        if (RepositoryPath is not { } path
            || GridSelection.FirstAndSelected(SelectedRevisions) is not (var first, { } selected))
        {
            return false;
        }

        Run(() => Task.Run(() => GitModules.Open(path).OpenWithDifftoolDirDiff(first, selected.Hash, tool)));
        return true;
    }

    private CommitRow? BaseToCompare()
        => _baseCommitToCompare is { } chosen && chosen.Path == RepositoryPath ? chosen.Row : null;

    // Upstream's CompareToBranchToolStripMenuItem_Click: a branch chosen in the compare to branch window as BASE.
    private bool CompareToBranch()
    {
        if (RepositoryPath is not { } path || SelectedCommitId() is not { } head || _commits.Selected is not { } row)
        {
            return false;
        }

        Run(async () =>
        {
            if (await new CompareToBranchWindow(path, head).ShowDialog<string?>(this) is not { } branch)
            {
                return;
            }

            ObjectId baseCommit = await Task.Run(() => GitModules.Open(path).RevParse(branch));
            if (baseCommit.IsZero)
            {
                await ShowCompareErrorAsync(RevisionDiff.NoRevisionFound);
                return;
            }

            ShowRevisionDiff(path, baseCommit, head, branch, row.Subject);
        });
        return true;
    }

    // Upstream's CompareWithCurrentBranchToolStripMenuItem_Click: the selected commit as BASE, the current branch compared.
    private bool CompareWithCurrentBranch()
    {
        if (RepositoryPath is not { } path || SelectedCommitId() is not { } baseCommit || _commits.Selected is not { } row)
        {
            return false;
        }

        string branch = _repository.CurrentBranch;
        Run(async () =>
        {
            ObjectId current = await Task.Run(() => GitModules.Open(path).GetCurrentCheckout());
            if (string.IsNullOrWhiteSpace(branch) || current.IsZero)
            {
                await ShowCompareErrorAsync(RevisionDiff.NoBranchSelected);
                return;
            }

            ShowRevisionDiff(path, baseCommit, current, row.Subject, branch);
        });
        return true;
    }

    // Upstream's selectAsBaseToolStripMenuItem_Click.
    private bool SelectAsBaseToCompare()
    {
        if (RepositoryPath is not { } path || _commits.Selected is not { } row)
        {
            return false;
        }

        _baseCommitToCompare = (path, row);
        CompareToBaseMenuItem.IsEnabled = true;
        return true;
    }

    // Upstream's compareToBaseToolStripMenuItem_Click: the commit selected as BASE, the selected one compared.
    private bool CompareToBase()
    {
        if (RepositoryPath is not { } path || SelectedCommitId() is not { } head || _commits.Selected is not { } row)
        {
            return false;
        }

        if (BaseToCompare() is not { } baseRow || !ObjectId.TryParse(baseRow.Hash, out ObjectId baseCommit))
        {
            Run(() => ShowCompareErrorAsync(RevisionDiff.BaseNotSelected));
            return true;
        }

        ShowRevisionDiff(path, baseCommit, head, baseRow.Subject, row.Subject);
        return true;
    }

    // Upstream's compareToWorkingDirectoryMenuItem_Click: the selected commit as BASE, the working directory compared.
    private bool CompareToWorkingDirectory()
    {
        if (RepositoryPath is not { } path || SelectedCommitId() is not { } baseCommit || _commits.Selected is not { } row)
        {
            return false;
        }

        if (baseCommit == ObjectId.WorkTreeId)
        {
            Run(() => ShowCompareErrorAsync(RevisionDiff.WorkTreeToItself));
            return true;
        }

        ShowRevisionDiff(path, baseCommit, ObjectId.WorkTreeId, row.Subject,
            UpstreamTranslation.ByText(RevisionDiff.WorkingDirectory));
        return true;
    }

    private ObjectId? SelectedCommitId()
        => _commits.Selected is { } row && ObjectId.TryParse(row.Hash, out ObjectId id) ? id : null;

    // Upstream's ShowFormDiff: not modal, in the taskbar.
    private void ShowRevisionDiff(string path, ObjectId baseCommit, ObjectId headCommit, string baseDisplay,
        string headDisplay)
        => new RevisionDiffWindow(path, baseCommit, headCommit, baseDisplay, headDisplay, _preferences)
        {
            ShowInTaskbar = true,
            MenuServices = FileListMenuServices,
        }.Show(this);

    private Task ShowCompareErrorAsync(string text)
        => new MessageBoxWindow(UpstreamTranslation.ByText(text), UpstreamTranslation.ByText(Core.Operations.ManageWorktreeDialog.Error),
            System.Windows.Forms.MessageBoxButtons.OK).ShowDialog(this);
}

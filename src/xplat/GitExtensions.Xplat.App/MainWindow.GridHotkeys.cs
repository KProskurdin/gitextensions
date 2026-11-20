using Avalonia.Controls;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Presentation;

namespace GitExtensions.Xplat.App;

// Upstream RevisionGridControl's hotkeys that act on the View menu's choices, the selection and the selected commit's refs.
public partial class MainWindow
{
    // Upstream's ToggleRevisionGraphColumn, ToggleShowAuthorDate and the other toggles: the View menu's item.
    private bool ToggleGridView(GridViewToggle toggle)
    {
        GridViewMenu.ToggleCommand.Execute(toggle);
        return true;
    }

    // Upstream's SelectNextForkPointAsDiffBase: the fork point below the earliest selected commit, selected first, then
    // the other selected commits again (the earliest itself when it was alone), so the fork point is the diff's base.
    private bool SelectNextForkPointAsDiffBase()
    {
        IReadOnlyList<CommitRow> selected = SelectedRevisions;
        if (selected.Count == 0 || _commits.NextForkPoint(selected[^1]) is not { } forkPoint
                                || _commits.IndexOfHash(forkPoint) is not { } forkIndex
                                || CommitList.SelectedItems is not { } items)
        {
            return false;
        }

        items.Clear();
        items.Add(_commits.VisibleRows[forkIndex]);
        foreach (CommitRow row in selected.Take(Math.Max(1, selected.Count - 1)).Reverse())
        {
            if (_commits.IndexOfHash(row.Hash) is { } index && !items.Contains(_commits.VisibleRows[index]))
            {
                items.Add(_commits.VisibleRows[index]);
            }
        }

        CommitList.ScrollIntoView(forkIndex);
        return true;
    }

    // Upstream's DeleteRef and RenameRef: one branch or tag is acted on at once; for several, upstream's
    // FormQuickGitRefSelector lets the user choose, here a menu at the selected commit.
    private bool QuickRefAction(bool delete)
    {
        if (_commits.Selected is not { } row)
        {
            return false;
        }

        HashSet<string> localBranches =
            [.. _repository.Branches.Where(branch => !branch.IsRemote).Select(branch => branch.Name)];
        GridRefMenus menus = GridMenus.For(row, _repository.CurrentBranch,
            new HashSet<string>(_repository.Tags.Where(localBranches.Contains)), keep: null);
        IReadOnlyList<MenuEntry> choices = Toolbar.QuickRefAction(menus, delete);
        if (choices.Count == 0)
        {
            return true;
        }

        MenuFlyout chooser = new();
        MenuEntries.Fill(chooser.Items, choices, this);
        Control target = CommitList.ContainerFromIndex(CommitList.SelectedIndex) ?? CommitList;
        chooser.ShowAt(target);
        return true;
    }
}

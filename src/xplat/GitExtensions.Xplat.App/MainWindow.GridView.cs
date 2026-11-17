using Avalonia.Controls;
using GitCommands;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Settings;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's View menu for the revision grid (<c>RevisionGridMenuCommands.GetViewMenuCommands</c>): the branch choices,
///  the grid labels, the commit body, the columns and the sort, and the grid's row colors (<c>RevisionDataGridView</c>): the
///  alternating rows and the tint of the selected commit's author's commits (<c>AuthorRevisionHighlighting</c>).
/// </summary>
public partial class MainWindow
{
    // The author whose commits the grid tints: the selected commit's, or the user's while none is selected.
    private string _highlightedAuthorEmail = "";

    // Upstream's View menu, whose state and choices are the view model's (MVVM.md); what a choice changes in the window.
    private GridViewMenuViewModel NewGridViewMenu() => new(_preferences, _commits, new GridViewMenuServices
    {
        ShowBranches = ShowBranches,
        ApplyFilter = ApplyFilter,
        EditFilter = EditRevisionFilterAsync,
        RefreshRepository = RefreshRepository,
        ShowGridView = ShowGridView,
        ShowAvatarColumn = ShowAvatarSettings,
        ShowBuildStatusColumn = ShowBuildStatusColumn,
        HighlightSelectedBranch = HighlightSelectedBranch,
        ShowError = ex => ShowError(ex.Message),
    });

    private void WireGridView()
    {
        if (ShowAllBranchesMenuItem.Parent is MenuItem view)
        {
            view.SubmenuOpened += (_, _) => GridViewMenu.Refresh();
        }

        CommitList.ContainerPrepared += (_, e) => ShowAuthoredHighlight(e.Container);
        CommitList.SelectionChanged += (_, _) =>
        {
            // Upstream's ProcessRevisionSelectionChange: several selected commits leave the highlight as it is.
            if (CommitList.SelectedItems is not { Count: > 1 })
            {
                HighlightAuthor(LatestSelectedItem?.Row.AuthorEmail);
            }
        };
        _repository.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(_repository.UserEmail) && LatestSelectedItem is null)
            {
                HighlightAuthor(null);
            }
        };

        ShowGridView();
    }

    // The grid as the View menu and upstream's appearance settings say: the columns, the labels' fill, the alternating rows
    // and the rows' labels and body.
    private void ShowGridView()
    {
        GridColumns columns = _preferences.GridColumns;
        Resources["ShowGraphColumn"] = columns.Graph;
        Resources["ShowAuthorNameColumn"] = columns.AuthorName;
        Resources["ShowDateColumn"] = columns.Date;
        RevisionGridColumns.SetWidths(this, _preferences.RelativeDate);
        Resources["ShowObjectIdColumn"] = columns.ObjectId;
        Resources["ShowNotesColumn"] = _preferences.GridViewOptions.NotesInSeparateColumn;
        Resources["FillRefLabels"] = _preferences.FillRefLabels;
        CommitList.Classes.Set("alternate", _preferences.RevisionGraphDrawAlternateBackColor);
        _commits.ViewOptions = _preferences.GridViewOptions;
        _commits.DrawNonRelativesGray = _preferences.RevisionGraphDrawNonRelativesGray;
        _commits.DrawNonRelativesTextGray = _preferences.RevisionGraphDrawNonRelativesTextGray;
        foreach (Control container in CommitList.GetRealizedContainers())
        {
            ShowAuthoredHighlight(container);
        }
    }

    // Upstream's ProcessRevisionSelectionChange: the selected commit's author, or the user's own commits without a selection.
    private void HighlightAuthor(string? email)
    {
        string author = email ?? _repository.UserEmail;
        if (string.Equals(author, _highlightedAuthorEmail, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _highlightedAuthorEmail = author;
        foreach (Control container in CommitList.GetRealizedContainers())
        {
            ShowAuthoredHighlight(container);
        }
    }

    // Upstream's IsHighlighted: a commit (not an artificial row) by the highlighted author, when HighlightAuthoredRevisions is on.
    private void ShowAuthoredHighlight(Control container)
        => container.Classes.Set("authored", _preferences.HighlightAuthoredRevisions
                                             && container.DataContext is CommitListItem { IsArtificial: false } item
                                             && !string.IsNullOrWhiteSpace(item.Row.AuthorEmail)
                                             && string.Equals(item.Row.AuthorEmail, _highlightedAuthorEmail,
                                                 StringComparison.OrdinalIgnoreCase));

    // Upstream's ViewSelectedRevisions, which a double-click on the grid runs: a real commit opens in upstream's
    // FormCommitDiff; the artificial rows open nothing. Not ported: with no selection, upstream compares revisions.
    private void ViewSelectedRevision()
    {
        if (RepositoryPath is { } path && _commits.Selected is { } row && !ArtificialCommits.IsArtificial(row.Hash))
        {
            new CommitDiffWindow(path, row.Hash).Show(this);
        }
    }
}

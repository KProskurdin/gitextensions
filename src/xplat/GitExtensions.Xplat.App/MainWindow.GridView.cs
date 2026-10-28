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

    private void WireGridView()
    {
        ShowAllBranchesMenuItem.Click += (_, _) => ShowBranches(currentOnly: false, filtered: false);
        ShowCurrentBranchOnlyMenuItem.Click += (_, _) => ShowBranches(currentOnly: true, filtered: false);
        ShowFilteredBranchesMenuItem.Click += (_, _) => ShowBranches(currentOnly: false, filtered: true);
        ShowReflogReferencesMenuItem.Click += (_, _) =>
            ApplyFilter(_commits.Filter with { ShowReflog = !_commits.Filter.ShowReflog });
        AdvancedFilterViewMenuItem.Click += (_, _) => Run(EditRevisionFilterAsync);

        ShowRemoteBranchesMenuItem.Click += (_, _) => SetGridViewOptions(_preferences.GridViewOptions with
        {
            ShowRemoteBranches = !_preferences.GridViewOptions.ShowRemoteBranches,
        });
        ShowTagsMenuItem.Click += (_, _) => SetGridViewOptions(_preferences.GridViewOptions with
        {
            ShowTags = !_preferences.GridViewOptions.ShowTags,
        });
        ShowCommitBodyMenuItem.Click += (_, _) => SetGridViewOptions(_preferences.GridViewOptions with
        {
            ShowCommitBody = !_preferences.GridViewOptions.ShowCommitBody,
        });

        ShowGraphColumnMenuItem.Click += (_, _) => SetGridColumns(_preferences.GridColumns with
        {
            Graph = !_preferences.GridColumns.Graph,
        });
        ShowAvatarColumnMenuItem.Click += (_, _) =>
        {
            _preferences.ShowAuthorAvatarColumn = !_preferences.ShowAuthorAvatarColumn;
            ShowAvatarSettings();
        };
        ShowAuthorNameColumnMenuItem.Click += (_, _) => SetGridColumns(_preferences.GridColumns with
        {
            AuthorName = !_preferences.GridColumns.AuthorName,
        });
        ShowDateColumnMenuItem.Click += (_, _) => SetGridColumns(_preferences.GridColumns with
        {
            Date = !_preferences.GridColumns.Date,
        });
        ShowObjectIdColumnMenuItem.Click += (_, _) => SetGridColumns(_preferences.GridColumns with
        {
            ObjectId = !_preferences.GridColumns.ObjectId,
        });

        // Upstream's ToggleAuthorDateSort and ToggleTopoOrder: each turns its order on, or back to git's default.
        AuthorDateSortMenuItem.Click += (_, _) => SetRevisionSortOrder(RevisionSortOrder.AuthorDate);
        TopoOrderMenuItem.Click += (_, _) => SetRevisionSortOrder(RevisionSortOrder.Topology);

        if (ShowAllBranchesMenuItem.Parent is MenuItem view)
        {
            view.SubmenuOpened += (_, _) => ShowViewMenuChecks();
        }

        CommitList.ContainerPrepared += (_, e) => ShowAuthoredHighlight(e.Container);
        CommitList.SelectionChanged += (_, _) => HighlightAuthor((CommitList.SelectedItem as CommitListItem)?.Row.AuthorEmail);
        _repository.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(_repository.UserEmail) && CommitList.SelectedItem is null)
            {
                HighlightAuthor(null);
            }
        };

        ShowGridView();
        ShowViewMenuChecks();
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
        Resources["FillRefLabels"] = _preferences.FillRefLabels;
        CommitList.Classes.Set("alternate", _preferences.RevisionGraphDrawAlternateBackColor);
        _commits.ViewOptions = _preferences.GridViewOptions;
    }

    private void ShowViewMenuChecks()
    {
        RevisionFilter filter = _commits.Filter;
        ShowAllBranchesMenuItem.IsChecked = !filter.CurrentBranchOnly && !filter.ShowFilteredBranches;
        ShowCurrentBranchOnlyMenuItem.IsChecked = filter.CurrentBranchOnly;
        ShowFilteredBranchesMenuItem.IsChecked = !filter.CurrentBranchOnly && filter.ShowFilteredBranches;
        ShowReflogReferencesMenuItem.IsChecked = filter.ShowReflog;
        GridViewOptions options = _preferences.GridViewOptions;
        ShowRemoteBranchesMenuItem.IsChecked = options.ShowRemoteBranches;
        ShowTagsMenuItem.IsChecked = options.ShowTags;
        ShowCommitBodyMenuItem.IsChecked = options.ShowCommitBody;
        GridColumns columns = _preferences.GridColumns;
        ShowGraphColumnMenuItem.IsChecked = columns.Graph;
        ShowAvatarColumnMenuItem.IsChecked = _preferences.ShowAuthorAvatarColumn;
        ShowAuthorNameColumnMenuItem.IsChecked = columns.AuthorName;
        ShowDateColumnMenuItem.IsChecked = columns.Date;
        ShowObjectIdColumnMenuItem.IsChecked = columns.ObjectId;
        AuthorDateSortMenuItem.IsChecked = _preferences.RevisionSortOrder == RevisionSortOrder.AuthorDate;
        TopoOrderMenuItem.IsChecked = _preferences.RevisionSortOrder == RevisionSortOrder.Topology;
    }

    private void SetGridViewOptions(GridViewOptions options)
    {
        _preferences.GridViewOptions = options;
        ShowGridView();
        ShowViewMenuChecks();
    }

    private void SetGridColumns(GridColumns columns)
    {
        _preferences.GridColumns = columns;
        ShowGridView();
        ShowViewMenuChecks();
    }

    private void SetRevisionSortOrder(RevisionSortOrder order)
    {
        _preferences.RevisionSortOrder = _preferences.RevisionSortOrder == order ? RevisionSortOrder.GitDefault : order;
        ShowViewMenuChecks();
        RefreshRepository();
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
}

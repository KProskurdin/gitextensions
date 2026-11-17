using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitCommands;
using GitExtensions.Xplat.Core.Settings;

namespace GitExtensions.Xplat.Core.CommitHistory;

/// <summary>
///  The View menu's choices of branches (upstream's <c>ShowAllBranches</c>, <c>ShowCurrentBranchOnly</c>,
///  <c>ShowFilteredBranches</c>).
/// </summary>
public enum BranchesShown
{
    All,
    CurrentOnly,
    Filtered,
}

/// <summary>
///  The View menu's on and off choices, in the menu's order.
/// </summary>
public enum GridViewToggle
{
    Reflog,
    DrawNonRelativesGray,
    ArtificialCommits,
    Stashes,
    GitNotes,
    SessionCheckpoints,
    RemoteBranches,
    Tags,
    SuperprojectTags,
    SuperprojectRemoteBranches,
    SuperprojectBranches,
    BuildStatusIcon,
    BuildStatusText,
    CommitBody,
    AuthorDate,
    RelativeDate,
    GraphColumn,
    NotesColumn,
    AvatarColumn,
    AuthorNameColumn,
    DateColumn,
    ObjectIdColumn,
    AuthorDateSort,
    TopoOrder,
}

/// <summary>
///  What <see cref="GridViewMenuViewModel"/> asks of the browse window: how a choice is shown.
/// </summary>
public sealed record GridViewMenuServices
{
    /// <summary>
    ///  Upstream's branch choice, through the filter toolbar: current branch only, or the filtered branches.
    /// </summary>
    public required Action<bool, bool> ShowBranches { get; init; }

    public required Action<RevisionFilter> ApplyFilter { get; init; }

    /// <summary>
    ///  Upstream's "Advanced filter..." window.
    /// </summary>
    public required Func<Task> EditFilter { get; init; }

    /// <summary>
    ///  Reads the repository again, for a choice the history is read with.
    /// </summary>
    public required Action RefreshRepository { get; init; }

    /// <summary>
    ///  Shows the grid's columns and labels as the preferences say.
    /// </summary>
    public required Action ShowGridView { get; init; }

    public required Action ShowAvatarColumn { get; init; }

    public required Action ShowBuildStatusColumn { get; init; }

    /// <summary>
    ///  Upstream's "Highlight selected branch (until refresh)".
    /// </summary>
    public Action HighlightSelectedBranch { get; init; } = () => { };

    public Action<Exception> ShowError { get; init; } = _ => { };
}

/// <summary>
///  Upstream's View menu for the revision grid (<c>RevisionGridMenuCommands.GetViewMenuCommands</c>): its check marks, from
///  the preferences and the grid's filter, and its choices, which store the preference and show it as upstream does (the
///  grid's columns and labels at once, the choices the history is read with by reading it again).
/// </summary>
public sealed partial class GridViewMenuViewModel(
    IAppPreferences preferences,
    CommitListViewModel commits,
    GridViewMenuServices services) : ObservableObject
{
    public bool ShowAllBranches => !commits.Filter.CurrentBranchOnly && !commits.Filter.ShowFilteredBranches;

    public bool ShowCurrentBranchOnly => commits.Filter.CurrentBranchOnly;

    public bool ShowFilteredBranches => !commits.Filter.CurrentBranchOnly && commits.Filter.ShowFilteredBranches;

    public bool ShowReflog => commits.Filter.ShowReflog;

    public bool DrawNonRelativesGray => preferences.RevisionGraphDrawNonRelativesGray;

    public bool ShowArtificialCommits => preferences.RevisionGraphShowArtificialCommits;

    public bool ShowStashes => preferences.ShowStashes;

    public bool ShowGitNotes => preferences.GridViewOptions.ShowGitNotes;

    public bool ShowSessionCheckpoints => preferences.ShowSessionRefs;

    public bool ShowRemoteBranches => preferences.GridViewOptions.ShowRemoteBranches;

    public bool ShowTags => preferences.GridViewOptions.ShowTags;

    public bool ShowSuperprojectTags => preferences.ShowSuperprojectTags;

    public bool ShowSuperprojectRemoteBranches => preferences.ShowSuperprojectRemoteBranches;

    public bool ShowSuperprojectBranches => preferences.ShowSuperprojectBranches;

    public bool ShowBuildStatusIcon => preferences.ShowBuildStatusIconColumn;

    public bool ShowBuildStatusText => preferences.ShowBuildStatusTextColumn;

    public bool ShowCommitBody => preferences.GridViewOptions.ShowCommitBody;

    public bool ShowAuthorDate => preferences.ShowAuthorDate;

    public bool ShowRelativeDate => preferences.RelativeDate;

    public bool ShowGraphColumn => preferences.GridColumns.Graph;

    public bool ShowNotesColumn => preferences.GridViewOptions.NotesInSeparateColumn;

    public bool ShowAvatarColumn => preferences.ShowAuthorAvatarColumn;

    public bool ShowAuthorNameColumn => preferences.GridColumns.AuthorName;

    public bool ShowDateColumn => preferences.GridColumns.Date;

    public bool ShowObjectIdColumn => preferences.GridColumns.ObjectId;

    public bool SortByAuthorDate => preferences.RevisionSortOrder == RevisionSortOrder.AuthorDate;

    public bool TopoOrder => preferences.RevisionSortOrder == RevisionSortOrder.Topology;

    /// <summary>
    ///  Shows the check marks again, for a choice made elsewhere (the filter toolbar, the settings window).
    /// </summary>
    public void Refresh() => OnPropertyChanged(string.Empty);

    [RelayCommand]
    private void HighlightSelectedBranch() => services.HighlightSelectedBranch();

    [RelayCommand]
    private void ShowBranches(BranchesShown shown)
    {
        services.ShowBranches(shown == BranchesShown.CurrentOnly, shown == BranchesShown.Filtered);
        Refresh();
    }

    [RelayCommand]
    private async Task EditFilterAsync()
    {
        try
        {
            await services.EditFilter();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            services.ShowError(ex);
        }

        Refresh();
    }

    [RelayCommand]
    private void Toggle(GridViewToggle toggle)
    {
        IAppPreferences p = preferences;
        GridViewOptions options = p.GridViewOptions;
        GridColumns columns = p.GridColumns;
        switch (toggle)
        {
            case GridViewToggle.Reflog:
                services.ApplyFilter(commits.Filter with { ShowReflog = !commits.Filter.ShowReflog });
                break;
            case GridViewToggle.DrawNonRelativesGray:
                p.RevisionGraphDrawNonRelativesGray = !p.RevisionGraphDrawNonRelativesGray;
                commits.DrawNonRelativesGray = p.RevisionGraphDrawNonRelativesGray;
                break;

            // Upstream's toggles of what the history is read with read the grid again (PerformRefreshRevisions).
            case GridViewToggle.ArtificialCommits:
                p.RevisionGraphShowArtificialCommits = !p.RevisionGraphShowArtificialCommits;
                services.RefreshRepository();
                break;
            case GridViewToggle.Stashes:
                p.ShowStashes = !p.ShowStashes;
                services.RefreshRepository();
                break;
            case GridViewToggle.SessionCheckpoints:
                p.ShowSessionRefs = !p.ShowSessionRefs;
                services.RefreshRepository();
                break;
            case GridViewToggle.SuperprojectTags:
                p.ShowSuperprojectTags = !p.ShowSuperprojectTags;
                services.RefreshRepository();
                break;
            case GridViewToggle.SuperprojectRemoteBranches:
                p.ShowSuperprojectRemoteBranches = !p.ShowSuperprojectRemoteBranches;
                services.RefreshRepository();
                break;
            case GridViewToggle.SuperprojectBranches:
                p.ShowSuperprojectBranches = !p.ShowSuperprojectBranches;
                services.RefreshRepository();
                break;
            case GridViewToggle.AuthorDate:
                p.ShowAuthorDate = !p.ShowAuthorDate;
                services.RefreshRepository();
                break;

            // The date column's width follows the relative date.
            case GridViewToggle.RelativeDate:
                p.RelativeDate = !p.RelativeDate;
                services.ShowGridView();
                services.RefreshRepository();
                break;

            // Upstream's ToggleShowGitNotes and ToggleShowGitNotesColumn: the notes come with the history.
            case GridViewToggle.GitNotes:
                SetOptions(options with { ShowGitNotes = !options.ShowGitNotes });
                services.RefreshRepository();
                break;
            case GridViewToggle.NotesColumn:
                SetOptions(options with { NotesInSeparateColumn = !options.NotesInSeparateColumn });
                services.RefreshRepository();
                break;
            case GridViewToggle.RemoteBranches:
                SetOptions(options with { ShowRemoteBranches = !options.ShowRemoteBranches });
                break;
            case GridViewToggle.Tags:
                SetOptions(options with { ShowTags = !options.ShowTags });
                break;
            case GridViewToggle.CommitBody:
                SetOptions(options with { ShowCommitBody = !options.ShowCommitBody });
                break;
            case GridViewToggle.BuildStatusIcon:
                p.ShowBuildStatusIconColumn = !p.ShowBuildStatusIconColumn;
                services.ShowBuildStatusColumn();
                break;
            case GridViewToggle.BuildStatusText:
                p.ShowBuildStatusTextColumn = !p.ShowBuildStatusTextColumn;
                services.ShowBuildStatusColumn();
                break;
            case GridViewToggle.GraphColumn:
                SetColumns(columns with { Graph = !columns.Graph });
                break;
            case GridViewToggle.AuthorNameColumn:
                SetColumns(columns with { AuthorName = !columns.AuthorName });
                break;
            case GridViewToggle.DateColumn:
                SetColumns(columns with { Date = !columns.Date });
                break;
            case GridViewToggle.ObjectIdColumn:
                SetColumns(columns with { ObjectId = !columns.ObjectId });
                break;
            case GridViewToggle.AvatarColumn:
                p.ShowAuthorAvatarColumn = !p.ShowAuthorAvatarColumn;
                services.ShowAvatarColumn();
                break;

            // Upstream's ToggleAuthorDateSort and ToggleTopoOrder: each turns its order on, or back to git's default.
            case GridViewToggle.AuthorDateSort:
                SetSortOrder(RevisionSortOrder.AuthorDate);
                break;
            case GridViewToggle.TopoOrder:
                SetSortOrder(RevisionSortOrder.Topology);
                break;
        }

        Refresh();
    }

    private void SetOptions(GridViewOptions options)
    {
        preferences.GridViewOptions = options;
        services.ShowGridView();
    }

    private void SetColumns(GridColumns columns)
    {
        preferences.GridColumns = columns;
        services.ShowGridView();
    }

    private void SetSortOrder(RevisionSortOrder order)
    {
        preferences.RevisionSortOrder = preferences.RevisionSortOrder == order ? RevisionSortOrder.GitDefault : order;
        services.RefreshRepository();
    }
}

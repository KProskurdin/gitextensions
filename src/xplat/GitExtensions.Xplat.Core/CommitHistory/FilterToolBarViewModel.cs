using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Scripts;
using GitExtensions.Xplat.Core.Settings;

namespace GitExtensions.Xplat.Core.CommitHistory;

/// <summary>
///  The check items of the filter toolbar's two type menus: the kinds of text the "Filter:" box filters by, and the kinds
///  of refs the "Branches:" box offers.
/// </summary>
public enum FilterToolBarCheck
{
    Message,
    Committer,
    Author,
    DiffContent,
    LocalBranches,
    RemoteBranches,
    Tags,
}

/// <summary>
///  What the filter toolbar asks of the window around it.
/// </summary>
public sealed record FilterToolBarServices
{
    /// <summary>
    ///  Upstream's translation of an English text.
    /// </summary>
    public Func<string, string> Translate { get; init; } = text => text;

    /// <summary>
    ///  Upstream's "&lt;No results found&gt;" item of the "Branches:" box, translated; read when the box is used, as reading
    ///  the translation while the window opens races with the settings read in the background.
    /// </summary>
    public Func<string> NoResultsText { get; init; } = () => DefaultNoResultsText;

    /// <summary>
    ///  Shows a warning (text, caption) with an OK button and completes when it is closed.
    /// </summary>
    public Func<string, string, Task> ShowWarningAsync { get; init; } = (_, _) => Task.CompletedTask;

    /// <summary>
    ///  Shows an error.
    /// </summary>
    public Action<string> ShowError { get; init; } = _ => { };

    /// <summary>
    ///  The commit a revision names in a repository (repository, revision), or null; git by default.
    /// </summary>
    public Func<string, string, Task<string?>> ResolveCommitAsync { get; init; } = RepositoryScriptContext.ResolveCommitAsync;

    // Upstream's TranslatedStrings._noResultsFound.
    public const string DefaultNoResultsText = "<No results found>";
}

/// <summary>
///  Upstream's filter toolstrip (<c>FilterToolBar</c>) for a grid (<see cref="IFilterToolBarGrid"/>): the advanced filter
///  with its resets, the reflog and first parent toggles, the branch choice with the "Branches:" box and the kinds of refs
///  it offers, and the "Filter:" box with the texts it remembers and the kinds of text it filters by. The view keeps the
///  keys, the focus, the opening of its menus and the boxes' drop-down widths.
/// </summary>
public sealed partial class FilterToolBarViewModel : ObservableObject
{
    // Upstream's FilterToolBar: how many texts the "Filter:" box remembers, and the presets it offers after them.
    public const int MaxRevisionFilterItems = 30;

    // Upstream's TranslatedStrings for a branch filter word that is not a revision.
    public const string IgnoringReferenceText = "\"{0}\" is not a Git revision and will be ignored.";
    public const string NonexistingGitRevisionText = "Git revision does not exist";

    // Upstream's images of the advanced filter button and of the branch choice.
    public const string FilterOnImage = "FunnelExclamation";
    public const string FilterOffImage = "FunnelPencil";
    public const string AllBranchesImage = "BranchLocal";
    public const string FilteredBranchesImage = "BranchFilter";

    private static readonly string[] _revisionFilterPresets =
    [
        @"--invert-grep --grep=""EXCLUDE_COMMIT_MESSAGE_REGEX_PATTERN""",
        @"--perl-regexp --author=""^(?!.*EXCLUDE_AUTHOR_REGEX_PATTERN)""",
        @"--exclude=refs/remotes/EXCLUDE_REMOTE_REGEX_PATTERN",
    ];

    private readonly IFilterToolBarGrid _grid;
    private readonly IAppPreferences _preferences;
    private readonly FilterToolBarServices _services;

    public FilterToolBarViewModel(IFilterToolBarGrid grid, IAppPreferences preferences, FilterToolBarServices? services = null)
    {
        _grid = grid;
        _preferences = preferences;
        _services = services ?? new FilterToolBarServices();
        ShowRevisionFilterItems();
        ShowFilterState();
    }

    /// <summary>
    ///  The advanced filter button asks for its resets menu: a filter is on (upstream's
    ///  <c>tsbtnAdvancedFilter_ButtonClick</c>).
    /// </summary>
    public event EventHandler? ResetsMenuRequested;

    /// <summary>
    ///  Upstream's "&lt;No results found&gt;" item of the "Branches:" box.
    /// </summary>
    public string NoResultsText => _services.NoResultsText();

    // The kinds of text the "Filter:" box filters by (upstream's checks start on the commit message).
    [ObservableProperty]
    public partial bool FilterByMessage { get; set; } = true;

    [ObservableProperty]
    public partial bool FilterByCommitter { get; set; }

    [ObservableProperty]
    public partial bool FilterByAuthor { get; set; }

    [ObservableProperty]
    public partial bool FilterByDiffContent { get; set; }

    // The kinds of refs the "Branches:" box offers (upstream's start on local branches).
    [ObservableProperty]
    public partial bool OfferLocalBranches { get; set; } = true;

    [ObservableProperty]
    public partial bool OfferRemoteBranches { get; set; }

    [ObservableProperty]
    public partial bool OfferTags { get; set; }

    /// <summary>
    ///  The "Filter:" box's text.
    /// </summary>
    [ObservableProperty]
    public partial string? RevisionFilterText { get; set; } = "";

    /// <summary>
    ///  The texts the "Filter:" box remembers, then upstream's presets.
    /// </summary>
    [ObservableProperty]
    public partial IReadOnlyList<string> RevisionFilterItems { get; set; } = [];

    /// <summary>
    ///  The "Branches:" box's text.
    /// </summary>
    [ObservableProperty]
    public partial string? BranchFilterText { get; set; } = "";

    /// <summary>
    ///  The refs the "Branches:" box offers, then its "no results" item.
    /// </summary>
    [ObservableProperty]
    public partial IReadOnlyList<string> BranchFilterItems { get; set; } = [];

    /// <summary>
    ///  The refs the "Branches:" box offers, without the "no results" item.
    /// </summary>
    public IReadOnlyList<string> BranchFilterRefs { get; private set; } = [];

    [ObservableProperty]
    public partial bool ShowReflog { get; set; }

    [ObservableProperty]
    public partial bool FirstParent { get; set; }

    /// <summary>
    ///  The branches the grid shows, which the branch button shows as its chosen item.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BranchesImage))]
    public partial BranchesShown ShownBranches { get; set; }

    public string BranchesImage => ShownBranches == BranchesShown.All ? AllBranchesImage : FilteredBranchesImage;

    [ObservableProperty]
    public partial string AdvancedFilterImage { get; set; } = FilterOffImage;

    /// <summary>
    ///  The filter's summary, the advanced filter button's tooltip; null without a filter.
    /// </summary>
    [ObservableProperty]
    public partial string? AdvancedFilterTip { get; set; }

    [ObservableProperty]
    public partial bool CanResetPathFilter { get; set; }

    [ObservableProperty]
    public partial bool CanResetRevisionFilters { get; set; }

    /// <summary>
    ///  Upstream's <c>BindBranches</c>: a ref whose name contains the text, whatever its case, and the "no results" item only
    ///  when there is none.
    /// </summary>
    public static bool IsBranchFilterItemShown(string? search, string? item, IReadOnlyList<string> branches, string noResults)
    {
        string text = search ?? "";
        return item == noResults
            ? !branches.Any(branch => branch.Contains(text, StringComparison.InvariantCultureIgnoreCase))
            : item?.Contains(text, StringComparison.InvariantCultureIgnoreCase) == true;
    }

    /// <summary>
    ///  Upstream's <c>revisionGridFilter_FilterChanged</c>: the toolbar shows the filter the grid applies.
    /// </summary>
    public void ShowFilterState()
    {
        RevisionFilter filter = _grid.Filter;
        FirstParent = filter.FirstParent;
        ShowReflog = filter.ShowReflog;

        // Upstream's InitBranchSelectionFilter and SelectShowBranchesFilterOption.
        ShownBranches = filter.CurrentBranchOnly ? BranchesShown.CurrentOnly
            : filter.ShowFilteredBranches ? BranchesShown.Filtered
            : BranchesShown.All;
        if (filter.ShowFilteredBranches && !filter.CurrentBranchOnly)
        {
            BranchFilterText = filter.BranchFilter;
        }

        // The box shows the first text filter, and the kinds filtering by that text are checked; without a text filter
        // the box is empty and the kinds stay.
        string text = new[] { filter.Message, filter.Committer, filter.Author, filter.DiffContent }
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "";
        if (text.Length > 0)
        {
            FilterByMessage = filter.Message == text;
            FilterByCommitter = filter.Committer == text;
            FilterByAuthor = filter.Author == text;
            FilterByDiffContent = filter.DiffContent == text;
            RememberRevisionFilter(text.Trim());
        }

        RevisionFilterText = text;

        string summary = filter.Summary(_services.Translate);
        AdvancedFilterTip = summary.Length > 0 ? summary : null;
        AdvancedFilterImage = filter.HasFilter ? FilterOnImage : FilterOffImage;
        CanResetPathFilter = filter.PathFilter.Length > 0;
        CanResetRevisionFilters = filter.HasFilter;
    }

    /// <summary>
    ///  Upstream's <c>SetRevisionFilter</c>: the text in the "Filter:" box, applied (nothing when both are empty).
    /// </summary>
    public void SetRevisionFilter(string? filter)
    {
        if (string.IsNullOrEmpty(RevisionFilterText) && string.IsNullOrEmpty(filter))
        {
            return;
        }

        RevisionFilterText = filter;
        ApplyTextFilter();
    }

    // Upstream's UpdateBranchFilterItems when the box gets the focus.
    [RelayCommand]
    private Task LoadBranchFilterItemsAsync() => GuardAsync(ReadBranchFilterItemsAsync);

    // Upstream's UpdateBranchFilterItems: the refs of the checked kinds; with none checked, every branch and tag.
    private async Task ReadBranchFilterItemsAsync()
    {
        (IReadOnlyList<BranchInfo> branches, IReadOnlyList<string> tags) = await _grid.RefsAsync();
        bool all = !OfferLocalBranches && !OfferRemoteBranches && !OfferTags;
        BranchFilterRefs = branches
            .Where(branch => all || (branch.IsRemote ? OfferRemoteBranches : OfferLocalBranches))
            .Select(branch => branch.Name)
            .Concat(all || OfferTags ? tags : [])
            .ToList();
        BranchFilterItems = [.. BranchFilterRefs, NoResultsText];
    }

    /// <summary>
    ///  Upstream's <c>ShowAllBranches</c>, <c>ShowCurrentBranchOnly</c> and <c>ShowFilteredBranches</c>; the current branch
    ///  choice is upstream's <c>ShowCurrentBranchOnly</c> setting.
    /// </summary>
    [RelayCommand]
    public void ShowBranches(BranchesShown shown)
    {
        bool currentOnly = shown == BranchesShown.CurrentOnly;
        bool filtered = shown == BranchesShown.Filtered;
        _preferences.ShowCurrentBranchOnly = currentOnly;
        ApplyFilter(_grid.Filter with
        {
            CurrentBranchOnly = currentOnly,
            ShowFilteredBranches = filtered,
            BranchFilter = filtered ? BranchFilterText?.Trim() ?? "" : _grid.Filter.BranchFilter,
        });
    }

    // Upstream's tsbtnAdvancedFilter_ButtonClick: with a filter on, the button offers the resets first.
    [RelayCommand]
    private void AdvancedFilter()
    {
        if (_grid.Filter.HasFilter)
        {
            ResetsMenuRequested?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            Run(_grid.ShowRevisionFilterDialogAsync);
        }
    }

    [RelayCommand]
    private void EditAdvancedFilter() => Run(_grid.ShowRevisionFilterDialogAsync);

    [RelayCommand]
    private void ResetPathFilter() => ApplyFilter(_grid.Filter with { PathFilter = "" });

    [RelayCommand]
    private void ResetRevisionFilters() => ApplyFilter(_grid.Filter.ResetAll());

    [RelayCommand]
    private void ToggleReflog() => ApplyFilter(_grid.Filter with { ShowReflog = !_grid.Filter.ShowReflog });

    [RelayCommand]
    private void ToggleFirstParent() => ApplyFilter(_grid.Filter with { FirstParent = !_grid.Filter.FirstParent });

    // A changed kind of text applies the text again (upstream's revisionFilterBox_CheckedChanged); a changed kind of ref
    // reads the box's refs again.
    [RelayCommand]
    private void ToggleCheck(FilterToolBarCheck check)
    {
        switch (check)
        {
            case FilterToolBarCheck.Message:
                FilterByMessage = !FilterByMessage;
                break;
            case FilterToolBarCheck.Committer:
                FilterByCommitter = !FilterByCommitter;
                break;
            case FilterToolBarCheck.Author:
                FilterByAuthor = !FilterByAuthor;
                break;
            case FilterToolBarCheck.DiffContent:
                FilterByDiffContent = !FilterByDiffContent;
                break;
            case FilterToolBarCheck.LocalBranches:
                OfferLocalBranches = !OfferLocalBranches;
                break;
            case FilterToolBarCheck.RemoteBranches:
                OfferRemoteBranches = !OfferRemoteBranches;
                break;
            default:
                OfferTags = !OfferTags;
                break;
        }

        if (check >= FilterToolBarCheck.LocalBranches)
        {
            Run(ReadBranchFilterItemsAsync);
        }
        else if (!string.IsNullOrWhiteSpace(RevisionFilterText))
        {
            ApplyTextFilter();
        }
    }

    // Upstream's ApplyRevisionFilter: the text filters by each checked kind; an unchecked kind's filter is cleared.
    [RelayCommand]
    private void ApplyTextFilter()
    {
        string text = RevisionFilterText?.Trim() ?? "";
        ApplyFilter(_grid.Filter with
        {
            Message = FilterByMessage ? text : "",
            Committer = FilterByCommitter ? text : "",
            Author = FilterByAuthor ? text : "",
            DiffContent = FilterByDiffContent ? text : "",
        });
    }

    // Upstream's ApplyCustomBranchFilter: words that are no revision are reported and left out; a filter shows the
    // filtered branches, an empty one all of them.
    [RelayCommand]
    private Task ApplyBranchFilterAsync() => GuardAsync(async () =>
    {
        if (_grid.RepositoryPath is not { } path)
        {
            return;
        }

        // Upstream's ApplyCustomBranchFilter takes its "no results" item for no filter.
        if (BranchFilterText == NoResultsText)
        {
            BranchFilterText = "";
        }

        (IReadOnlyList<BranchInfo> branches, IReadOnlyList<string> tags) = await _grid.RefsAsync();
        HashSet<string> refs = [.. branches.Select(branch => branch.Name), .. tags];
        List<string> words = [];
        foreach (string word in (BranchFilterText ?? "").Split((char[]?)null,
                     StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            bool accepted = word.StartsWith("--", StringComparison.Ordinal) || refs.Contains(word)
                || word.Contains("..", StringComparison.Ordinal) || RevisionFilter.IsBranchPattern(word);
            if (!accepted && await _services.ResolveCommitAsync(path, word.TrimStart('^')) is null)
            {
                await _services.ShowWarningAsync(string.Format(_services.Translate(IgnoringReferenceText), word),
                    _services.Translate(NonexistingGitRevisionText));
                continue;
            }

            words.Add(word);
        }

        string filter = string.Join(' ', words);
        _preferences.ShowCurrentBranchOnly = false;
        await _grid.ApplyFilterAsync(_grid.Filter with
        {
            CurrentBranchOnly = false, ShowFilteredBranches = filter.Length > 0, BranchFilter = filter,
        });
    });

    // Upstream adds an applied text to the top of the box's list and to its settings, once.
    private void RememberRevisionFilter(string text)
    {
        List<string> remembered = [.. _preferences.RevisionFilterDropdowns];
        if (remembered.FirstOrDefault() == text)
        {
            return;
        }

        remembered.Remove(text);
        remembered.Insert(0, text);
        _preferences.RevisionFilterDropdowns = [.. remembered.Take(MaxRevisionFilterItems)];
        ShowRevisionFilterItems();
    }

    private void ShowRevisionFilterItems()
        => RevisionFilterItems = _preferences.RevisionFilterDropdowns.Union(_revisionFilterPresets).ToList();

    private void ApplyFilter(RevisionFilter filter) => Run(() => _grid.ApplyFilterAsync(filter));

    private void Run(Func<Task> action) => _ = GuardAsync(action);

    // An exception that escapes an asynchronous command ends the app; it shows over the window instead, as the toolbar did.
    private async Task GuardAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (OperationCanceledException)
        {
            // Nothing to report.
        }
        catch (Exception ex)
        {
            _services.ShowError(ex.Message);
        }
    }
}

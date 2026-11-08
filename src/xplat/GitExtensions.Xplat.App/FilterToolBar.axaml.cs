using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Scripts;
using GitExtensions.Xplat.Core.Settings;
using GitExtensions.Xplat.Ui;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>IRevisionGridFilter</c>: the revision grid a <see cref="FilterToolBar"/> filters.
/// </summary>
public interface IFilterToolBarGrid
{
    /// <summary>
    ///  The repository the grid shows, or null.
    /// </summary>
    string? RepositoryPath { get; }

    /// <summary>
    ///  The filter the grid applies.
    /// </summary>
    RevisionFilter Filter { get; }

    /// <summary>
    ///  Applies a filter (upstream's <c>SetAndApply...Filter</c>), keeping the options upstream stores.
    /// </summary>
    Task ApplyFilterAsync(RevisionFilter filter);

    /// <summary>
    ///  Upstream's <c>ShowRevisionFilterDialog</c>: the advanced filter window.
    /// </summary>
    Task ShowRevisionFilterDialogAsync();

    /// <summary>
    ///  The branches and tags the "Branches:" box offers.
    /// </summary>
    Task<(IReadOnlyList<BranchInfo> Branches, IReadOnlyList<string> Tags)> RefsAsync();
}

/// <summary>
///  Upstream's filter toolstrip (<c>FilterToolBar</c>): the advanced filter with its resets, the reflog and first parent
///  toggles, the branch choice with the "Branches:" box, and the "Filter:" box with the kinds of text it filters by. The
///  browse window and the file history show it, each for its own grid (<see cref="Bind"/>).
/// </summary>
public partial class FilterToolBar : UserControl
{
    // Upstream's FilterToolBar: how many texts the "Filter:" box remembers, and the presets it offers after them.
    private const int MaxRevisionFilterItems = 30;

    // Upstream's TranslatedStrings for a branch filter word that is not a revision.
    private const string IgnoringReferenceText = "\"{0}\" is not a Git revision and will be ignored.";
    private const string NonexistingGitRevisionText = "Git revision does not exist";

    private static readonly string[] _revisionFilterPresets =
    [
        @"--invert-grep --grep=""EXCLUDE_COMMIT_MESSAGE_REGEX_PATTERN""",
        @"--perl-regexp --author=""^(?!.*EXCLUDE_AUTHOR_REGEX_PATTERN)""",
        @"--exclude=refs/remotes/EXCLUDE_REMOTE_REGEX_PATTERN",
    ];

    private IFilterToolBarGrid? _grid;
    private IAppPreferences _preferences = null!;

    // The kinds of text the "Filter:" box filters by (upstream's checks start on the commit message), and the kinds of
    // refs the "Branches:" box offers (upstream's start on local branches).
    private bool _filterByMessage = true;
    private bool _filterByCommitter;
    private bool _filterByAuthor;
    private bool _filterByDiffContent;
    private bool _offerLocalBranches = true;
    private bool _offerRemoteBranches;
    private bool _offerTags;

    // The refs the branch filter box offers; its items are these and upstream's "no results" item.
    private IReadOnlyList<string> _branchFilterItems = [];

    private static string BranchFilterNoResults => UpstreamTranslation.Text("TranslatedStrings", "_noResultsFound", "<No results found>");

    public FilterToolBar()
    {
        InitializeComponent();
    }

    private IFilterToolBarGrid Grid
        => _grid ?? throw new InvalidOperationException($"{nameof(Bind)} is not called.");

    /// <summary>
    ///  Upstream's <c>Bind</c>: the grid the toolbar filters; call <see cref="ShowFilterState"/> when its filter changes.
    /// </summary>
    public void Bind(IFilterToolBarGrid grid, IAppPreferences preferences)
    {
        _grid = grid;
        _preferences = preferences;

        // Upstream's tsbtnAdvancedFilter_ButtonClick: with a filter on, the button offers the resets first.
        AdvancedFilterButton.Click += (_, _) =>
        {
            if (Grid.Filter.HasFilter)
            {
                AdvancedFilterButton.Flyout!.ShowAt(AdvancedFilterButton);
            }
            else
            {
                Run(Grid.ShowRevisionFilterDialogAsync);
            }
        };
        ResetPathFilterMenuItem.Click += (_, _) => ApplyFilter(Grid.Filter with { PathFilter = "" });
        ResetRevisionFiltersMenuItem.Click += (_, _) => ApplyFilter(Grid.Filter.ResetAll());
        AdvancedFilterMenuItem.Click += (_, _) => Run(Grid.ShowRevisionFilterDialogAsync);
        ShowReflogButton.Click += (_, _) => ApplyFilter(Grid.Filter with { ShowReflog = !Grid.Filter.ShowReflog });
        ShowFirstParentButton.Click += (_, _) =>
            ApplyFilter(Grid.Filter with { FirstParent = !Grid.Filter.FirstParent });

        ShowBranchesButton.Click += (_, _) => ShowBranchesButton.Flyout!.ShowAt(ShowBranchesButton);
        ShowBranchesAllMenuItem.Click += (_, _) => ShowBranches(currentOnly: false, filtered: false);
        ShowBranchesCurrentMenuItem.Click += (_, _) => ShowBranches(currentOnly: true, filtered: false);
        ShowBranchesFilteredMenuItem.Click += (_, _) => ShowBranches(currentOnly: false, filtered: true);

        BranchFilterBox.ItemFilter = (search, item) =>
            IsBranchFilterItemShown(search, item as string, _branchFilterItems, BranchFilterNoResults);
        BranchFilterBox.GotFocus += (_, _) => Run(FillBranchFilterItemsAsync);

        // Upstream's SetShortcutKeys, as the Settings may have changed them.
        ((MenuFlyout)AdvancedFilterButton.Flyout!).Opening += (_, _) =>
        {
            ResetPathFilterMenuItem.InputGesture = Hotkeys.Grid.GestureFor(GridCommand.ResetRevisionPathFilter);
            ResetRevisionFiltersMenuItem.InputGesture = Hotkeys.Grid.GestureFor(GridCommand.ResetRevisionFilter);
            AdvancedFilterMenuItem.InputGesture = Hotkeys.Grid.GestureFor(GridCommand.RevisionFilter);
        };
        BranchFilterBox.AddHandler(KeyUpEvent, (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                Run(ApplyBranchFilterAsync);
            }
        }, RoutingStrategies.Bubble, handledEventsToo: true);
        BranchLocalMenuItem.Click += (_, _) => _offerLocalBranches = !_offerLocalBranches;
        BranchRemoteMenuItem.Click += (_, _) => _offerRemoteBranches = !_offerRemoteBranches;
        BranchTagMenuItem.Click += (_, _) => _offerTags = !_offerTags;
        foreach (MenuItem item in (MenuItem[])[BranchLocalMenuItem, BranchRemoteMenuItem, BranchTagMenuItem])
        {
            item.Click += (_, _) =>
            {
                ShowFilterTypeChecks();
                Run(FillBranchFilterItemsAsync);
            };
        }

        // Enter applies the text; the box's own Enter would only open its list.
        RevisionFilterBox.AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                ApplyTextFilter();
            }
        }, RoutingStrategies.Tunnel);
        CommitFilterMenuItem.Click += (_, _) => _filterByMessage = !_filterByMessage;
        CommitterFilterMenuItem.Click += (_, _) => _filterByCommitter = !_filterByCommitter;
        AuthorFilterMenuItem.Click += (_, _) => _filterByAuthor = !_filterByAuthor;
        DiffContainsFilterMenuItem.Click += (_, _) => _filterByDiffContent = !_filterByDiffContent;
        MenuItem[] filterKinds =
            [CommitFilterMenuItem, CommitterFilterMenuItem, AuthorFilterMenuItem, DiffContainsFilterMenuItem];
        foreach (MenuItem item in filterKinds)
        {
            // Upstream's revisionFilterBox_CheckedChanged: a changed kind applies the text again.
            item.Click += (_, _) =>
            {
                ShowFilterTypeChecks();
                if (!string.IsNullOrWhiteSpace(RevisionFilterBox.Text))
                {
                    ApplyTextFilter();
                }
            };
        }

        ShowRevisionFilterItems();
        ShowFilterTypeChecks();
        ShowFilterState();
    }

    /// <summary>
    ///  Upstream's <c>revisionGridFilter_FilterChanged</c>: the toolbar shows the filter the grid applies.
    /// </summary>
    public void ShowFilterState()
    {
        RevisionFilter filter = Grid.Filter;
        ShowFirstParentButton.IsChecked = filter.FirstParent;
        ShowReflogButton.IsChecked = filter.ShowReflog;

        // Upstream's InitBranchSelectionFilter and SelectShowBranchesFilterOption.
        MenuItem branches = filter.CurrentBranchOnly ? ShowBranchesCurrentMenuItem
            : filter.ShowFilteredBranches ? ShowBranchesFilteredMenuItem
            : ShowBranchesAllMenuItem;
        ShowBranchesButton.Image = branches == ShowBranchesAllMenuItem ? "BranchLocal" : "BranchFilter";
        ShowBranchesButton.Text = ((string)branches.Header!).Replace("_", "", StringComparison.Ordinal);
        ToolTip.SetTip(ShowBranchesButton, ToolTip.GetTip(branches));
        if (filter.ShowFilteredBranches && !filter.CurrentBranchOnly)
        {
            BranchFilterBox.Text = filter.BranchFilter;
        }

        // The box shows the first text filter, and the kinds filtering by that text are checked; without a text filter
        // the box is empty and the kinds stay.
        (string Value, Action<bool> Check)[] texts =
        [
            (filter.Message, check => _filterByMessage = check),
            (filter.Committer, check => _filterByCommitter = check),
            (filter.Author, check => _filterByAuthor = check),
            (filter.DiffContent, check => _filterByDiffContent = check),
        ];
        string text = texts.Select(item => item.Value).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "";
        if (text.Length > 0)
        {
            foreach ((string value, Action<bool> check) in texts)
            {
                check(value == text);
            }

            RememberRevisionFilter(text.Trim());
        }

        RevisionFilterBox.Text = text;
        ShowFilterTypeChecks();

        string summary = filter.Summary(UpstreamTranslation.ByText);
        ToolTip.SetTip(AdvancedFilterButton, summary.Length > 0 ? summary : null);
        AdvancedFilterButton.Image = filter.HasFilter ? "FunnelExclamation" : "FunnelPencil";
        ResetPathFilterMenuItem.IsEnabled = filter.PathFilter.Length > 0;
        ResetRevisionFiltersMenuItem.IsEnabled = filter.HasFilter;
    }

    /// <summary>
    ///  Upstream's <c>ShowAllBranches</c>, <c>ShowCurrentBranchOnly</c> and <c>ShowFilteredBranches</c>; the current branch
    ///  choice is upstream's <c>ShowCurrentBranchOnly</c> setting.
    /// </summary>
    public void ShowBranches(bool currentOnly, bool filtered)
    {
        _preferences.ShowCurrentBranchOnly = currentOnly;
        ApplyFilter(Grid.Filter with
        {
            CurrentBranchOnly = currentOnly,
            ShowFilteredBranches = filtered,
            BranchFilter = filtered ? BranchFilterBox.Text?.Trim() ?? "" : Grid.Filter.BranchFilter,
        });
    }

    /// <summary>
    ///  Upstream's <c>SetFocus</c>: the "Filter:" box, or from there the "Branches:" box.
    /// </summary>
    public void FocusFilter()
    {
        if (RevisionFilterBox.IsKeyboardFocusWithin)
        {
            BranchFilterBox.Focus();
        }
        else
        {
            RevisionFilterBox.Focus();
        }
    }

    /// <summary>
    ///  Upstream's <c>SetRevisionFilter</c>: the text in the "Filter:" box, applied (nothing when both are empty).
    /// </summary>
    public void SetRevisionFilter(string? filter)
    {
        if (string.IsNullOrEmpty(RevisionFilterBox.Text) && string.IsNullOrEmpty(filter))
        {
            return;
        }

        RevisionFilterBox.Text = filter;
        ApplyTextFilter();
    }

    private void ShowFilterTypeChecks()
    {
        CommitFilterMenuItem.IsChecked = _filterByMessage;
        CommitterFilterMenuItem.IsChecked = _filterByCommitter;
        AuthorFilterMenuItem.IsChecked = _filterByAuthor;
        DiffContainsFilterMenuItem.IsChecked = _filterByDiffContent;
        BranchLocalMenuItem.IsChecked = _offerLocalBranches;
        BranchRemoteMenuItem.IsChecked = _offerRemoteBranches;
        BranchTagMenuItem.IsChecked = _offerTags;
    }

    // Upstream's ApplyRevisionFilter: the text filters by each checked kind; an unchecked kind's filter is cleared.
    private void ApplyTextFilter()
    {
        string text = RevisionFilterBox.Text?.Trim() ?? "";
        ApplyFilter(Grid.Filter with
        {
            Message = _filterByMessage ? text : "",
            Committer = _filterByCommitter ? text : "",
            Author = _filterByAuthor ? text : "",
            DiffContent = _filterByDiffContent ? text : "",
        });
    }

    // Upstream's ApplyCustomBranchFilter: words that are no revision are reported and left out; a filter shows the
    // filtered branches, an empty one all of them.
    private async Task ApplyBranchFilterAsync()
    {
        if (Grid.RepositoryPath is not { } path)
        {
            return;
        }

        // Upstream's ApplyCustomBranchFilter takes its "no results" item for no filter.
        if (BranchFilterBox.Text == BranchFilterNoResults)
        {
            BranchFilterBox.Text = "";
        }

        (IReadOnlyList<BranchInfo> branches, IReadOnlyList<string> tags) = await Grid.RefsAsync();
        HashSet<string> refs = [.. branches.Select(branch => branch.Name), .. tags];
        List<string> words = [];
        foreach (string word in (BranchFilterBox.Text ?? "").Split((char[]?)null,
                     StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            bool accepted = word.StartsWith("--", StringComparison.Ordinal) || refs.Contains(word)
                                                                            || word.Contains("..",
                                                                                StringComparison.Ordinal) ||
                                                                            RevisionFilter.IsBranchPattern(word);
            if (!accepted && await RepositoryScriptContext.ResolveCommitAsync(path, word.TrimStart('^')) is null)
            {
                if (TopLevel.GetTopLevel(this) is Window owner)
                {
                    await new MessageBoxWindow(string.Format(UpstreamTranslation.ByText(IgnoringReferenceText), word),
                        UpstreamTranslation.ByText(NonexistingGitRevisionText), MessageBoxButtons.OK).ShowDialog(owner);
                }

                continue;
            }

            words.Add(word);
        }

        string filter = string.Join(' ', words);
        _preferences.ShowCurrentBranchOnly = false;
        await Grid.ApplyFilterAsync(Grid.Filter with
        {
            CurrentBranchOnly = false, ShowFilteredBranches = filter.Length > 0, BranchFilter = filter,
        });
    }

    // Upstream's UpdateBranchFilterItems: the refs of the checked kinds; with none checked, every branch and tag.
    private async Task FillBranchFilterItemsAsync()
    {
        (IReadOnlyList<BranchInfo> branches, IReadOnlyList<string> tags) = await Grid.RefsAsync();
        bool all = !_offerLocalBranches && !_offerRemoteBranches && !_offerTags;
        _branchFilterItems = branches
            .Where(branch => all || (branch.IsRemote ? _offerRemoteBranches : _offerLocalBranches))
            .Select(branch => branch.Name)
            .Concat(all || _offerTags ? tags : [])
            .ToList();
        BranchFilterBox.ItemsSource = _branchFilterItems.Append(BranchFilterNoResults).ToList();
    }

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
        => RevisionFilterBox.ItemsSource = _preferences.RevisionFilterDropdowns.Union(_revisionFilterPresets).ToList();

    private void ApplyFilter(RevisionFilter filter) => Run(() => Grid.ApplyFilterAsync(filter));

    private void Run(Func<Task> action)
        => UiActions.Run(action, ex =>
        {
            if (TopLevel.GetTopLevel(this) is Window owner)
            {
                _ = new ErrorWindow(ex.Message).ShowDialog(owner);
            }
        });
}

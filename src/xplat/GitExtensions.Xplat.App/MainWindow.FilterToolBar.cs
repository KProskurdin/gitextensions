using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Scripts;
using GitExtensions.Xplat.Ui;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's filter toolstrip (<c>FilterToolBar</c>): the advanced filter with its resets, the reflog and first parent
///  toggles, the branch choice with the "Branches:" box, and the "Filter:" box with the kinds of text it filters by.
/// </summary>
public partial class MainWindow
{
    // Upstream's FilterToolBar: how many texts the "Filter:" box remembers, and the presets it offers after them.
    private const int MaxRevisionFilterItems = 30;

    private static readonly string[] _revisionFilterPresets =
    [
        @"--invert-grep --grep=""EXCLUDE_COMMIT_MESSAGE_REGEX_PATTERN""",
        @"--perl-regexp --author=""^(?!.*EXCLUDE_AUTHOR_REGEX_PATTERN)""",
        @"--exclude=refs/remotes/EXCLUDE_REMOTE_REGEX_PATTERN",
    ];

    // Upstream's TranslatedStrings for a branch filter word that is not a revision.
    private const string IgnoringReferenceText = "\"{0}\" is not a Git revision and will be ignored.";
    private const string NonexistingGitRevisionText = "Git revision does not exist";

    // The kinds of text the "Filter:" box filters by (upstream's checks start on the commit message), and the kinds of
    // refs the "Branches:" box offers (upstream's start on local branches).
    private bool _filterByMessage = true;
    private bool _filterByCommitter;
    private bool _filterByAuthor;
    private bool _filterByDiffContent;
    private bool _offerLocalBranches = true;
    private bool _offerRemoteBranches;
    private bool _offerTags;

    private void WireFilterToolStrip()
    {
        // Upstream's tsbtnAdvancedFilter_ButtonClick: with a filter on, the button offers the resets first.
        AdvancedFilterButton.Click += (_, _) =>
        {
            if (_commits.Filter.HasFilter)
            {
                AdvancedFilterButton.Flyout!.ShowAt(AdvancedFilterButton);
            }
            else
            {
                Run(EditRevisionFilterAsync);
            }
        };
        ResetPathFilterMenuItem.Click += (_, _) => ApplyFilter(_commits.Filter with { PathFilter = "" });
        ResetRevisionFiltersMenuItem.Click += (_, _) => ApplyFilter(_commits.Filter.ResetAll());
        AdvancedFilterMenuItem.Click += (_, _) => Run(EditRevisionFilterAsync);
        ShowReflogButton.Click +=
            (_, _) => ApplyFilter(_commits.Filter with { ShowReflog = !_commits.Filter.ShowReflog });
        ShowFirstParentButton.Click += (_, _) =>
            ApplyFilter(_commits.Filter with { FirstParent = !_commits.Filter.FirstParent });

        ShowBranchesButton.Click += (_, _) => ShowBranchesButton.Flyout!.ShowAt(ShowBranchesButton);
        ShowBranchesAllMenuItem.Click += (_, _) => ShowBranches(currentOnly: false, filtered: false);
        ShowBranchesCurrentMenuItem.Click += (_, _) => ShowBranches(currentOnly: true, filtered: false);
        ShowBranchesFilteredMenuItem.Click += (_, _) => ShowBranches(currentOnly: false, filtered: true);

        BranchFilterBox.GotFocus += (_, _) => FillBranchFilterItems();
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
                FillBranchFilterItems();
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

    // Upstream's revisionGridFilter_FilterChanged: the toolbar shows the filter the grid applies.
    private void ShowFilterState()
    {
        RevisionFilter filter = _commits.Filter;
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
        ApplyFilter(_commits.Filter with
        {
            Message = _filterByMessage ? text : "",
            Committer = _filterByCommitter ? text : "",
            Author = _filterByAuthor ? text : "",
            DiffContent = _filterByDiffContent ? text : "",
        });
    }

    // Upstream's ShowAllBranches, ShowCurrentBranchOnly and ShowFilteredBranches; the current branch choice is upstream's
    // ShowCurrentBranchOnly setting.
    private void ShowBranches(bool currentOnly, bool filtered)
    {
        _preferences.ShowCurrentBranchOnly = currentOnly;
        ApplyFilter(_commits.Filter with
        {
            CurrentBranchOnly = currentOnly,
            ShowFilteredBranches = filtered,
            BranchFilter = filtered ? BranchFilterBox.Text?.Trim() ?? "" : _commits.Filter.BranchFilter,
        });
    }

    // Upstream's ApplyCustomBranchFilter: words that are no revision are reported and left out; a filter shows the
    // filtered branches, an empty one all of them.
    private async Task ApplyBranchFilterAsync()
    {
        if (RepositoryPath is not { } path)
        {
            return;
        }

        HashSet<string> refs = [.. _repository.Branches.Select(branch => branch.Name), .. _repository.Tags];
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
                await new MessageBoxWindow(string.Format(UpstreamTranslation.ByText(IgnoringReferenceText), word),
                    UpstreamTranslation.ByText(NonexistingGitRevisionText), MessageBoxButtons.OK).ShowDialog(this);
                continue;
            }

            words.Add(word);
        }

        string filter = string.Join(' ', words);
        _preferences.ShowCurrentBranchOnly = false;
        await ApplyRevisionFilterAsync(_commits.Filter with
        {
            CurrentBranchOnly = false, ShowFilteredBranches = filter.Length > 0, BranchFilter = filter,
        });
    }

    // Upstream's UpdateBranchFilterItems: the refs of the checked kinds; with none checked, every branch and tag.
    private void FillBranchFilterItems()
    {
        bool all = !_offerLocalBranches && !_offerRemoteBranches && !_offerTags;
        BranchFilterBox.ItemsSource = _repository.Branches
            .Where(branch => all || (branch.IsRemote ? _offerRemoteBranches : _offerLocalBranches))
            .Select(branch => branch.Name)
            .Concat(all || _offerTags ? _repository.Tags : [])
            .ToList();
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

    private void ApplyFilter(RevisionFilter filter) => Run(() => ApplyRevisionFilterAsync(filter));
}

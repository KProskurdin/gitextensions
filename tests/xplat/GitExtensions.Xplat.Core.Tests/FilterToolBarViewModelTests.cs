using AwesomeAssertions;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

// Upstream's FilterToolBar: what the toolbar shows for the grid's filter, and the filters its commands apply.
internal sealed class FilterToolBarViewModelTests
{
    private FakeGrid _grid = null!;
    private InMemoryAppPreferences _preferences = null!;
    private List<(string Text, string Caption)> _warnings = null!;

    [SetUp]
    public void SetUp()
    {
        _grid = new FakeGrid();
        _preferences = new InMemoryAppPreferences { RevisionFilterDropdowns = [] };
        _warnings = [];
    }

    [Test]
    public void ApplyTextFilter_should_filter_by_the_checked_kinds_and_remember_the_text()
    {
        FilterToolBarViewModel toolBar = New();
        toolBar.RevisionFilterText = " fix ";

        toolBar.ApplyTextFilterCommand.Execute(null);

        _grid.Filter.Message.Should().Be("fix");
        _grid.Filter.Author.Should().BeEmpty();
        _preferences.RevisionFilterDropdowns.Should().Equal("fix");
        toolBar.RevisionFilterItems[0].Should().Be("fix");
        toolBar.AdvancedFilterImage.Should().Be(FilterToolBarViewModel.FilterOnImage);
        toolBar.AdvancedFilterTip.Should().NotBeNull();
        toolBar.CanResetRevisionFilters.Should().BeTrue();
    }

    [Test]
    public void ToggleCheck_should_apply_the_text_again_by_the_kinds_now_checked()
    {
        FilterToolBarViewModel toolBar = New();
        toolBar.RevisionFilterText = "ann";

        toolBar.ToggleCheckCommand.Execute(FilterToolBarCheck.Author);
        toolBar.ToggleCheckCommand.Execute(FilterToolBarCheck.Message);

        (_grid.Filter.Author, _grid.Filter.Message).Should().Be(("ann", ""));
        (toolBar.FilterByAuthor, toolBar.FilterByMessage).Should().Be((true, false));
    }

    [Test]
    public void ShowFilterState_should_show_the_grids_filter()
    {
        _grid.Filter = new RevisionFilter(Committer: "bob", ShowFilteredBranches: true, BranchFilter: "main", FirstParent: true,
            PathFilter: "a.txt");
        FilterToolBarViewModel toolBar = New();

        toolBar.RevisionFilterText.Should().Be("bob");
        (toolBar.FilterByMessage, toolBar.FilterByCommitter).Should().Be((false, true));
        toolBar.ShownBranches.Should().Be(BranchesShown.Filtered);
        toolBar.BranchesImage.Should().Be(FilterToolBarViewModel.FilteredBranchesImage);
        toolBar.BranchFilterText.Should().Be("main");
        toolBar.FirstParent.Should().BeTrue();
        toolBar.ShowReflog.Should().BeFalse();
        toolBar.CanResetPathFilter.Should().BeTrue();
    }

    [Test]
    public void ShowBranches_should_store_the_choice_and_take_the_boxs_text_for_filtered_branches()
    {
        FilterToolBarViewModel toolBar = New();
        toolBar.BranchFilterText = " feature ";

        toolBar.ShowBranchesCommand.Execute(BranchesShown.Filtered);

        (_grid.Filter.ShowFilteredBranches, _grid.Filter.BranchFilter).Should().Be((true, "feature"));
        _preferences.ShowCurrentBranchOnly.Should().BeFalse();

        toolBar.ShowBranchesCommand.Execute(BranchesShown.CurrentOnly);

        _grid.Filter.CurrentBranchOnly.Should().BeTrue();
        _preferences.ShowCurrentBranchOnly.Should().BeTrue();
        toolBar.ShownBranches.Should().Be(BranchesShown.CurrentOnly);
    }

    [Test]
    public async Task ApplyBranchFilter_should_leave_out_and_report_words_that_are_no_revision()
    {
        FilterToolBarViewModel toolBar = New();
        toolBar.BranchFilterText = "main no-such-ref feature/* a..b";

        await toolBar.ApplyBranchFilterCommand.ExecuteAsync(null);

        _grid.Filter.BranchFilter.Should().Be("main feature/* a..b");
        _grid.Filter.ShowFilteredBranches.Should().BeTrue();
        _warnings.Should().Equal(("\"no-such-ref\" is not a Git revision and will be ignored.", "Git revision does not exist"));
    }

    [Test]
    public async Task LoadBranchFilterItems_should_offer_the_checked_kinds_of_refs_or_all_when_none_is()
    {
        FilterToolBarViewModel toolBar = New();

        await toolBar.LoadBranchFilterItemsCommand.ExecuteAsync(null);
        toolBar.BranchFilterItems.Should().Equal("main", FilterToolBarServices.DefaultNoResultsText);

        toolBar.ToggleCheckCommand.Execute(FilterToolBarCheck.LocalBranches);
        await toolBar.LoadBranchFilterItemsCommand.ExecuteAsync(null);
        toolBar.BranchFilterRefs.Should().Equal("main", "origin/main", "v1");

        toolBar.ToggleCheckCommand.Execute(FilterToolBarCheck.Tags);
        await toolBar.LoadBranchFilterItemsCommand.ExecuteAsync(null);
        toolBar.BranchFilterRefs.Should().Equal("v1");
    }

    [Test]
    public void AdvancedFilter_should_offer_the_resets_while_a_filter_is_on_else_open_the_window()
    {
        FilterToolBarViewModel toolBar = New();
        int resetsMenus = 0;
        toolBar.ResetsMenuRequested += (_, _) => resetsMenus++;

        toolBar.AdvancedFilterCommand.Execute(null);
        _grid.DialogsShown.Should().Be(1);

        _grid.Filter = new RevisionFilter(Message: "x");
        toolBar.AdvancedFilterCommand.Execute(null);
        resetsMenus.Should().Be(1);

        toolBar.ResetRevisionFiltersCommand.Execute(null);
        _grid.Filter.Message.Should().BeEmpty();
        toolBar.AdvancedFilterImage.Should().Be(FilterToolBarViewModel.FilterOffImage);
    }

    [TestCase("ma", "main", true)]
    [TestCase("MA", "main", true)]
    [TestCase("x", "main", false)]
    [TestCase("x", "<none>", true)]
    [TestCase("ma", "<none>", false)]
    public void IsBranchFilterItemShown_should_show_the_no_results_item_only_without_a_match(string search, string item,
        bool shown)
        => FilterToolBarViewModel.IsBranchFilterItemShown(search, item, ["main"], "<none>").Should().Be(shown);

    private FilterToolBarViewModel New()
    {
        FilterToolBarViewModel? toolBar = null;
        _grid.FilterApplied = () => toolBar?.ShowFilterState();
        toolBar = new FilterToolBarViewModel(_grid, _preferences, new FilterToolBarServices
        {
            ShowWarningAsync = (text, caption) =>
            {
                _warnings.Add((text, caption));
                return Task.CompletedTask;
            },
            ShowError = message => Assert.Fail(message),
            ResolveCommitAsync = (_, revision) => Task.FromResult(revision == "no-such-ref" ? null : (string?)"abc"),
        });
        return toolBar;
    }

    // The grid, which applies a filter at once and shows the toolbar its filter again, as the windows do.
    private sealed class FakeGrid : IFilterToolBarGrid
    {
        public string? RepositoryPath => "/repo";

        public RevisionFilter Filter { get; set; } = new();

        public Action? FilterApplied { get; set; }

        public int DialogsShown { get; private set; }

        public Task ApplyFilterAsync(RevisionFilter filter)
        {
            Filter = filter;
            FilterApplied?.Invoke();
            return Task.CompletedTask;
        }

        public Task ShowRevisionFilterDialogAsync()
        {
            DialogsShown++;
            return Task.CompletedTask;
        }

        public Task<(IReadOnlyList<BranchInfo> Branches, IReadOnlyList<string> Tags)> RefsAsync()
            => Task.FromResult<(IReadOnlyList<BranchInfo>, IReadOnlyList<string>)>(
                ([new BranchInfo("main", IsRemote: false, IsCurrent: true), new BranchInfo("origin/main", IsRemote: true, IsCurrent: false)],
                    ["v1"]));
    }
}

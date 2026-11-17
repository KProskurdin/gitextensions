using AwesomeAssertions;
using GitCommands;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Settings;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class GridViewMenuViewModelTests
{
    private readonly List<string> _steps = [];
    private InMemoryAppPreferences _preferences = null!;
    private CommitListViewModel _commits = null!;

    [SetUp]
    public void SetUp()
    {
        _steps.Clear();
        _preferences = new InMemoryAppPreferences();
        _commits = new CommitListViewModel(new FakeCommitHistory());
    }

    [Test]
    public async Task The_checks_should_follow_the_grids_filter_and_the_preferences()
    {
        _preferences.GridViewOptions = new GridViewOptions(ShowTags: false, ShowGitNotes: true);
        _preferences.GridColumns = new GridColumns(Date: false);
        _preferences.RevisionSortOrder = RevisionSortOrder.Topology;
        GridViewMenuViewModel menu = New();

        await _commits.ApplyFilterAsync(new RevisionFilter(ShowFilteredBranches: true, ShowReflog: true));

        (menu.ShowAllBranches, menu.ShowCurrentBranchOnly, menu.ShowFilteredBranches, menu.ShowReflog).Should()
            .Be((false, false, true, true));
        (menu.ShowTags, menu.ShowRemoteBranches, menu.ShowGitNotes).Should().Be((false, true, true));
        (menu.ShowGraphColumn, menu.ShowDateColumn).Should().Be((true, false));
        (menu.TopoOrder, menu.SortByAuthorDate).Should().Be((true, false));
    }

    [Test]
    public void A_label_or_column_choice_should_be_stored_and_shown_without_reading_the_history_again()
    {
        GridViewMenuViewModel menu = New();
        List<string?> changed = [];
        menu.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        menu.ToggleCommand.Execute(GridViewToggle.Tags);
        menu.ToggleCommand.Execute(GridViewToggle.ObjectIdColumn);

        _preferences.GridViewOptions.ShowTags.Should().BeFalse();
        _preferences.GridColumns.ObjectId.Should().BeFalse();
        (menu.ShowTags, menu.ShowObjectIdColumn).Should().Be((false, false));
        _steps.Should().Equal("grid", "grid");
        changed.Should().Equal(string.Empty, string.Empty);
    }

    [Test]
    public void A_choice_the_history_is_read_with_should_read_it_again()
    {
        GridViewMenuViewModel menu = New();
        bool stashes = _preferences.ShowStashes;

        menu.ToggleCommand.Execute(GridViewToggle.Stashes);
        menu.ToggleCommand.Execute(GridViewToggle.GitNotes);

        _preferences.ShowStashes.Should().Be(!stashes);
        _preferences.GridViewOptions.ShowGitNotes.Should().BeTrue();
        _steps.Should().Equal("refresh", "grid", "refresh");
    }

    [Test]
    public void A_sort_choice_should_turn_its_order_on_or_back_to_gits_default()
    {
        GridViewMenuViewModel menu = New();

        menu.ToggleCommand.Execute(GridViewToggle.AuthorDateSort);
        _preferences.RevisionSortOrder.Should().Be(RevisionSortOrder.AuthorDate);
        menu.ToggleCommand.Execute(GridViewToggle.TopoOrder);
        _preferences.RevisionSortOrder.Should().Be(RevisionSortOrder.Topology);
        menu.ToggleCommand.Execute(GridViewToggle.TopoOrder);

        _preferences.RevisionSortOrder.Should().Be(RevisionSortOrder.GitDefault);
        _steps.Should().Equal("refresh", "refresh", "refresh");
    }

    [Test]
    public void The_branch_choices_and_the_reflog_should_go_through_the_filter()
    {
        GridViewMenuViewModel menu = New();

        menu.ShowBranchesCommand.Execute(BranchesShown.CurrentOnly);
        menu.ShowBranchesCommand.Execute(BranchesShown.Filtered);
        menu.ToggleCommand.Execute(GridViewToggle.Reflog);

        _steps.Should().Equal("branches current=True filtered=False", "branches current=False filtered=True",
            "filter reflog=True");
    }

    [Test]
    public void Draw_non_relatives_gray_should_change_the_graph_at_once()
    {
        _preferences.RevisionGraphDrawNonRelativesGray = false;
        GridViewMenuViewModel menu = New();

        menu.ToggleCommand.Execute(GridViewToggle.DrawNonRelativesGray);

        (_preferences.RevisionGraphDrawNonRelativesGray, _commits.DrawNonRelativesGray, menu.DrawNonRelativesGray)
            .Should().Be((true, true, true));
        _steps.Should().BeEmpty();
    }

    [Test]
    public void The_relative_date_should_size_the_date_column_and_read_the_dates_again()
    {
        GridViewMenuViewModel menu = New();
        bool relative = _preferences.RelativeDate;

        menu.ToggleCommand.Execute(GridViewToggle.RelativeDate);

        _preferences.RelativeDate.Should().Be(!relative);
        _steps.Should().Equal("grid", "refresh");
    }

    private GridViewMenuViewModel New()
        => new(_preferences, _commits, new GridViewMenuServices
        {
            ShowBranches = (current, filtered) => _steps.Add($"branches current={current} filtered={filtered}"),
            ApplyFilter = filter => _steps.Add($"filter reflog={filter.ShowReflog}"),
            EditFilter = () => Task.CompletedTask,
            RefreshRepository = () => _steps.Add("refresh"),
            ShowGridView = () => _steps.Add("grid"),
            ShowAvatarColumn = () => _steps.Add("avatar"),
            ShowBuildStatusColumn = () => _steps.Add("build"),
        });
}

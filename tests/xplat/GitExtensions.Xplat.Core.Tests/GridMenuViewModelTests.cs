using AwesomeAssertions;
using GitExtensions.Xplat.Core.CommitHistory;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class GridMenuViewModelTests
{
    private const string Hash = "1234567812345678123456781234567812345678";

    private static readonly GridRefMenus _noRefs = new([], [], [], RebaseOnTopOf: null, [], [], [], [], IsHeadOfCurrentBranch: false);

    [Test]
    public void A_commit_should_offer_its_actions_and_hide_the_working_directorys()
    {
        GridMenuViewModel viewModel = new(supportsAmend: () => true);
        List<string?> changed = [];
        viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        viewModel.Refresh(Row(Hash), _noRefs, editorAvailable: true, hasBuildReport: false, hasPullRequest: false);

        changed.Should().Equal(string.Empty);
        viewModel.IsCommitItemVisible.Should().BeTrue();
        viewModel.IsArtificialItemVisible.Should().BeFalse();
        viewModel.IsStashItemVisible.Should().BeFalse();
        viewModel.IsAmendVisible.Should().BeTrue();
        viewModel.IsCheckoutBranchVisible.Should().BeFalse("no branch points at the commit");
        viewModel.CanRebaseOn.Should().BeFalse();
        viewModel.IsBuildReportVisible.Should().BeFalse();
    }

    [Test]
    public void The_working_directory_row_should_offer_only_its_reset_and_commit()
    {
        GridMenuViewModel viewModel = new();

        viewModel.Refresh(Row(ArtificialCommits.WorkTreeHash), _noRefs with
        {
            Checkout = [new GridMenuRef("main", IsRemote: false)],
        }, editorAvailable: true, hasBuildReport: false, hasPullRequest: false);

        viewModel.IsArtificial.Should().BeTrue();
        viewModel.IsArtificialItemVisible.Should().BeTrue();
        viewModel.IsCommitItemVisible.Should().BeFalse();
        viewModel.IsCheckoutBranchVisible.Should().BeFalse("upstream offers no commit actions on an artificial row");
        viewModel.IsAmendVisible.Should().BeFalse();
    }

    [Test]
    public void The_ref_submenus_should_show_when_they_have_refs_and_separators_alone_do_not_count()
    {
        GridMenuViewModel viewModel = new();

        viewModel.Refresh(Row(Hash), _noRefs with
        {
            Checkout = [null, new GridMenuRef("feature", IsRemote: false)],
            Push = [new GridMenuRef("feature", IsRemote: false)],
            Delete = [null],
            RebaseOnTopOf = "feature",
            IsHeadOfCurrentBranch = true,
        }, editorAvailable: false, hasBuildReport: true, hasPullRequest: true);

        viewModel.IsCheckoutBranchVisible.Should().BeTrue();
        viewModel.IsPushBranchVisible.Should().BeTrue();
        viewModel.IsMergeBranchVisible.Should().BeFalse();
        viewModel.IsDeleteBranchVisible.Should().BeTrue("upstream shows delete, disabled, on the current branch's head");
        viewModel.CanDeleteBranch.Should().BeFalse();
        viewModel.CanRebaseOn.Should().BeTrue();
        viewModel.CanRebaseInteractive.Should().BeFalse("an interactive rebase needs an editor");
        viewModel.IsBuildReportVisible.Should().BeTrue();
        viewModel.IsPullRequestVisible.Should().BeTrue();
    }

    [Test]
    public void A_stash_row_should_offer_the_stash_items_and_amend_only_where_git_can()
    {
        GridMenuViewModel viewModel = new(supportsAmend: () => false);

        viewModel.Refresh(Row(Hash) with { ReflogSelector = "refs/stash@{0}" }, _noRefs, editorAvailable: true,
            hasBuildReport: false, hasPullRequest: false);

        viewModel.IsStashItemVisible.Should().BeTrue();
        viewModel.IsAmendVisible.Should().BeFalse();
    }

    private static CommitRow Row(string hash) => new(hash, hash[..8], "subject", "Ann", "2026-11-15");
}

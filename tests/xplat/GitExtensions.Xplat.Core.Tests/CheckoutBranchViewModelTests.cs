using AwesomeAssertions;
using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Operations;
using NSubstitute;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class CheckoutBranchViewModelTests
{
    [Test]
    public void CheckoutBranchViewModel_should_show_the_local_branch_of_a_remote_one_and_follow_the_kind()
    {
        CheckoutBranchViewModel viewModel = NewViewModel(new FakeDialogHost(), "origin/feature", remote: true, out _);

        viewModel.IsRemote.Should().BeTrue();
        viewModel.LocalBranchNameText.Should().Be("'feature'");
        viewModel.ResetText.Should().Be(FakeDialogHost.TranslatedMark + "R_eset local branch with the name:");
        viewModel.UsesCustomName.Should().BeFalse();

        viewModel.Dialog.NewBranchMode = CheckoutNewBranchMode.Create;
        viewModel.UsesCustomName.Should().BeTrue();
        viewModel.IsRemote = false;

        viewModel.Dialog.Branches.Should().Contain("main");
        viewModel.Dialog.Branch.Should().BeEmpty("the chosen branch is cleared, as upstream's PopulateBranches");
    }

    [Test]
    public async Task CheckoutBranchViewModel_should_report_an_unknown_branch_and_check_out_a_known_one()
    {
        FakeDialogHost host = new();
        CheckoutBranchViewModel viewModel = NewViewModel(host, "nope", remote: false, out List<int> checkouts);
        viewModel.Dialog.Branches.Should().Contain("nope", "upstream adds the given branch to the list");
        viewModel.Dialog.SetRemote(true);
        viewModel.Dialog.SetRemote(false);
        viewModel.Dialog.Branch = "nope";

        await viewModel.CheckoutCommand.ExecuteAsync(null);
        viewModel.Dialog.Branch = "main";
        await viewModel.CheckoutCommand.ExecuteAsync(null);

        host.Messages.Should().Equal(FakeDialogHost.TranslatedMark + CheckoutBranchDialog.InvalidBranchName);
        checkouts.Should().Equal(1);
        host.Result.Should().Be(true);
    }

    [Test]
    public void CheckoutBranchViewModel_should_drop_set_as_default_for_a_reset()
    {
        CheckoutBranchViewModel viewModel = NewViewModel(new FakeDialogHost(), "main", remote: false, out _);
        viewModel.Dialog.SetAsDefault = true;

        viewModel.Dialog.ChangesMode = LocalChangesAction.Reset;

        viewModel.Dialog.SetAsDefault.Should().BeFalse();
        viewModel.Dialog.CanSetAsDefault.Should().BeFalse();
    }

    private static CheckoutBranchViewModel NewViewModel(FakeDialogHost host, string branch, bool remote,
        out List<int> checkouts)
    {
        IGitModule module = Substitute.For<IGitModule>();
        module.CheckBranchFormat(Arg.Any<string>()).Returns(true);
        CheckoutBranchSource source = new(
            ["main", "feature", "origin_feature"],
            ["origin/feature", "origin/new"],
            ["origin"],
            (remoteName, remoteBranch) => remoteBranch[(remoteName.Length + 1)..],
            IsDirty: true);
        List<int> done = [];
        checkouts = done;
        return new CheckoutBranchViewModel(host,
            new CheckoutBranchDialog(source, LocalChangesAction.DontChange, createLocalBranchForRemote: false, branch,
                remote),
            module,
            () =>
            {
                done.Add(1);
                return Task.FromResult(true);
            });
    }
}

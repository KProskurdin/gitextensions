using System.Text;
using AwesomeAssertions;
using GitCommands.Git;
using GitExtensions.Xplat.Core.BuildServer;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Settings;
using GitUIPluginInterfaces.BuildServerIntegration;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class SmallDialogViewModels4Tests
{
    [Test]
    public void AvailableEncodingsViewModel_should_move_encodings_and_store_them_only_on_OK()
    {
        Dictionary<string, Encoding> settings = new() { [Encoding.UTF8.WebName] = Encoding.UTF8 };
        int saves = 0;
        FakeDialogHost host = new();
        AvailableEncodingsViewModel viewModel = new(host, settings, () => saves++);
        Encoding added = viewModel.Available[0];
        viewModel.ToLeftCommand.CanExecute(null).Should().BeFalse("nothing is selected");

        viewModel.SelectedAvailable = added;
        viewModel.ToLeftCommand.Execute(null);
        viewModel.SelectedIncluded = Encoding.UTF8;

        viewModel.ToRightCommand.CanExecute(null).Should().BeFalse("the default encodings stay");
        viewModel.Included.Should().Equal(Encoding.UTF8, added);
        viewModel.Available.Should().NotContain(added);
        settings.Should().HaveCount(1, "only OK stores the list");

        viewModel.OkCommand.Execute(null);

        settings.Keys.Should().Equal(Encoding.UTF8.WebName, added.WebName);
        saves.Should().Be(1);
        host.Result.Should().Be(true);
    }

    [Test]
    public void BuildServerCredentialsViewModel_should_enable_the_boxes_of_the_access_type_and_keep_every_box()
    {
        FakeDialogHost host = new();
        BuildServerCredentialsViewModel viewModel = new(host, "https://ci.example.com",
            new BuildServerCredentials { BuildServerCredentialsType = BuildServerCredentialsType.Guest, Username = "ann" });

        viewModel.Header.Should().Be(FakeDialogHost.TranslatedMark +
                                     "Please enter the credentials for the build server at https://ci.example.com.");
        viewModel.UsesUserName.Should().BeFalse();
        viewModel.CredentialsType = BuildServerCredentialsType.BearerToken;
        viewModel.UsesBearerToken.Should().BeTrue();
        viewModel.BearerToken = "token";
        viewModel.Credentials.Should().BeNull("nothing is answered before OK");

        viewModel.OkCommand.Execute(null);

        viewModel.Credentials!.BuildServerCredentialsType.Should().Be(BuildServerCredentialsType.BearerToken);
        viewModel.Credentials.BearerToken.Should().Be("token");
        viewModel.Credentials.Username.Should().Be("ann");
        host.IsClosed.Should().BeTrue();
    }

    [Test]
    public async Task RenameBranchViewModel_should_normalise_the_name_and_close_as_cancel_for_the_same_name()
    {
        List<string> commands = [];
        InMemoryAppPreferences preferences = new() { AutoNormaliseBranchName = true, AutoNormaliseSymbol = "_" };
        FakeDialogHost sameHost = new();
        RenameBranchViewModel same = new(sameHost, "feature", preferences, Run);
        FakeDialogHost renamedHost = new();
        RenameBranchViewModel renamed = new(renamedHost, "feature", preferences, Run) { BranchName = "new name" };

        await same.RenameCommand.ExecuteAsync(null);
        await renamed.RenameCommand.ExecuteAsync(null);

        sameHost.Result.Should().Be(false);
        renamed.BranchName.Should().Be("new_name");
        commands.Should().Equal(Commands.RenameBranch("feature", "new_name").ToString());
        renamedHost.Result.Should().Be(true);
        return;

        Task<bool> Run(string arguments)
        {
            commands.Add(arguments);
            return Task.FromResult(true);
        }
    }

    [Test]
    public async Task RenameBranchViewModel_should_stay_open_when_git_fails()
    {
        FakeDialogHost host = new();
        RenameBranchViewModel viewModel = new(host, "feature", new InMemoryAppPreferences(), _ => Task.FromResult(false))
        {
            BranchName = "other",
        };

        await viewModel.RenameCommand.ExecuteAsync(null);

        host.IsClosed.Should().BeFalse();
    }

    [Test]
    public async Task BisectViewModel_should_offer_the_marks_once_the_bisect_started()
    {
        FakeDialogHost host = new();
        BisectViewModel viewModel = new(host, isBisecting: false, () => Task.FromResult(true));
        viewModel.MarkCommand.CanExecute(BisectStep.Bad).Should().BeFalse();

        await viewModel.StartCommand.ExecuteAsync(null);

        viewModel.StartCommand.CanExecute(null).Should().BeFalse();
        viewModel.MarkCommand.CanExecute(BisectStep.Bad).Should().BeTrue();
        viewModel.MarkCommand.Execute(BisectStep.Skip);
        host.Result.Should().Be(BisectStep.Skip);
        BisectViewModel.OptionFor(BisectStep.Skip).Should().Be(GitBisectOption.Skip);
        BisectViewModel.OptionFor(BisectStep.Stop).Should().BeNull();
    }
}

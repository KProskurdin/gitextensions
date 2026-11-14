using AwesomeAssertions;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using NSubstitute;
using NUnit.Framework;
using UserRepository = GitCommands.UserRepositoryHistory.Repository;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class DeleteBranchAndAddSubmoduleViewModelTests
{
    private readonly List<IReadOnlyList<string>> _deleted = [];
    private readonly List<string> _commands = [];

    [SetUp]
    public void SetUp()
    {
        _deleted.Clear();
        _commands.Clear();
    }

    [Test]
    public async Task DeleteBranchViewModel_should_refuse_the_current_branch()
    {
        FakeDialogHost host = new();
        DeleteBranchViewModel viewModel = NewDeleteBranch(host);
        viewModel.BranchesText = "merged main";

        await viewModel.DeleteCommand.ExecuteAsync(null);

        host.Messages.Should().Equal(
            string.Format(FakeDialogHost.TranslatedMark + DeleteBranchDialog.CannotDeleteCurrentBranch, "main"));
        _deleted.Should().BeEmpty();
        host.IsClosed.Should().BeFalse();
    }

    [Test]
    public async Task DeleteBranchViewModel_should_leave_out_unknown_names_and_close_after_the_delete()
    {
        FakeDialogHost host = new();
        DeleteBranchViewModel viewModel = NewDeleteBranch(host);
        viewModel.BranchesText = "merged nosuch";

        await viewModel.DeleteCommand.ExecuteAsync(null);

        host.Messages.Should().Equal(
            string.Format(FakeDialogHost.TranslatedMark + DeleteBranchDialog.BranchNotSelectable, "nosuch"));
        _deleted.Should().ContainSingle().Which.Should().Equal("merged");
        host.Result.Should().Be(true);
    }

    [Test]
    public async Task DeleteBranchViewModel_should_name_the_branches_checked_in_the_multiple_branches_window()
    {
        IReadOnlyList<string>? offered = null;
        DeleteBranchViewModel viewModel = new(new FakeDialogHost(), ["main", "merged", "other"], "main", ["merged"],
            null, selected =>
            {
                offered = selected;
                return Task.FromResult<IReadOnlyList<string>>(["merged", "other"]);
            });

        await viewModel.SelectMultipleBranchesCommand.ExecuteAsync(null);

        offered.Should().Equal("merged");
        viewModel.BranchesText.Should().Be("merged other");
    }

    [Test]
    public async Task AddSubmoduleViewModel_should_offer_the_history_and_propose_the_local_path()
    {
        IRemoteUrlHistory history = Substitute.For<IRemoteUrlHistory>();
        IList<UserRepository> remotes = [new UserRepository("https://example.com/team/app.git")];
        history.LoadAsync().Returns(remotes);
        AddSubmoduleViewModel viewModel = NewAddSubmodule(new FakeDialogHost(), history);

        await viewModel.LoadAsync();
        viewModel.RemotePath = "https://example.com/team/tools.git";

        viewModel.RemoteHistory.Should().Equal("https://example.com/team/app.git");
        viewModel.LocalPath.Should().Be("tools");
    }

    [Test]
    public async Task AddSubmoduleViewModel_should_need_both_paths_and_close_once_git_ran()
    {
        FakeDialogHost host = new();
        AddSubmoduleViewModel viewModel = NewAddSubmodule(host, Substitute.For<IRemoteUrlHistory>());

        await viewModel.AddCommand.ExecuteAsync(null);
        host.Messages.Should().Equal(FakeDialogHost.TranslatedMark + SubmodulesDialog.RemoteAndLocalPathRequired);
        _commands.Should().BeEmpty();

        viewModel.RemotePath = "https://example.com/team/tools.git";
        viewModel.Branch = "main";
        viewModel.Force = true;
        await viewModel.AddCommand.ExecuteAsync(null);

        _commands.Should().Equal(
            SubmodulesDialog.AddArguments("https://example.com/team/tools.git", "tools", "main", force: true)
                .ToString());
        host.IsClosed.Should().BeTrue();
    }

    [Test]
    public async Task AddSubmoduleViewModel_should_take_the_picked_folder()
    {
        AddSubmoduleViewModel viewModel = NewAddSubmodule(new FakeDialogHost(), Substitute.For<IRemoteUrlHistory>());
        viewModel.PickFolder = _ => Task.FromResult<string?>(Path.Combine("repos", "lib"));

        await viewModel.BrowseCommand.ExecuteAsync(null);

        viewModel.RemotePath.Should().Be(Path.Combine("repos", "lib"));
        viewModel.LocalPath.Should().Be("lib");
    }

    private DeleteBranchViewModel NewDeleteBranch(FakeDialogHost host)
        => new(host, ["main", "merged", "other"], "main", [], selected =>
        {
            _deleted.Add(selected);
            return Task.FromResult(true);
        }, null);

    private AddSubmoduleViewModel NewAddSubmodule(FakeDialogHost host, IRemoteUrlHistory history)
        => new(host, Substitute.For<IGitModule>(), history, arguments =>
        {
            _commands.Add(arguments);
            return Task.FromResult(true);
        });
}

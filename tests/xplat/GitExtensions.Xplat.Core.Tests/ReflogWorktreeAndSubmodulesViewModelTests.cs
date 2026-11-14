using AwesomeAssertions;
using GitCommands;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;
using NSubstitute;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class ReflogWorktreeAndSubmodulesViewModelTests
{
    private const string RepositoryPath = "/work/repo";

    private static readonly ObjectId _first = ObjectId.Parse("1111111111111111111111111111111111111111");

    [Test]
    public async Task ReflogViewModel_should_list_the_references_and_ask_before_a_soft_reset_with_local_changes()
    {
        IGitModule module = Substitute.For<IGitModule>();
        module.IsDirtyDir().Returns(true);
        module.GetSelectedBranch().Returns("main");
        List<string> read = [];
        List<(ObjectId Id, ResetMode Mode)> resets = [];
        FakeDialogHost host = new() { Answer = true };
        ReflogViewModel viewModel = new(host, RepositoryPath, module, _ => Task.CompletedTask,
            (id, mode) =>
            {
                resets.Add((id, mode));
                return Task.CompletedTask;
            },
            (_, reference) =>
            {
                read.Add(reference);
                return Task.FromResult<IReadOnlyList<ReflogEntry>>([new ReflogEntry(_first, reference + "@{0}", "commit")]);
            });

        await viewModel.LoadAsync();
        viewModel.ShowCurrentBranchCommand.Execute(null);
        await viewModel.EntriesLoaded;
        viewModel.SelectedEntry = viewModel.Entries[0];
        await viewModel.ResetCurrentBranchCommand.ExecuteAsync(null);

        viewModel.IsDirty.Should().BeTrue();
        viewModel.CurrentBranchText.Should().Be("current branch (main)");
        read.Should().Equal("HEAD", "main");
        viewModel.Entries.Single().Ref.Should().Be("main@{0}");
        host.Questions.Should().ContainSingle();
        resets.Should().Equal((_first, ResetMode.Soft));
    }

    [Test]
    public async Task ReflogViewModel_should_not_reset_on_a_detached_HEAD()
    {
        IGitModule module = Substitute.For<IGitModule>();
        module.GetSelectedBranch().Returns(GitCommands.Git.DetachedHeadParser.DetachedBranch);
        ReflogViewModel viewModel = new(new FakeDialogHost(), RepositoryPath, module, _ => Task.CompletedTask,
            (_, _) => Task.CompletedTask,
            (_, reference) => Task.FromResult<IReadOnlyList<ReflogEntry>>([new ReflogEntry(_first, reference, "commit")]));

        await viewModel.LoadAsync();
        viewModel.SelectedEntry = viewModel.Entries[0];

        viewModel.IsOnBranch.Should().BeFalse();
        viewModel.ResetCurrentBranchCommand.CanExecute(null).Should().BeFalse();
        viewModel.CreateBranchCommand.CanExecute(null).Should().BeTrue();
    }

    [Test]
    public async Task CreateWorktreeViewModel_should_propose_a_directory_and_create_only_with_a_branch_name()
    {
        IGitModule module = Substitute.For<IGitModule>();
        module.GetRefs(RefsFilter.Heads).Returns([]);
        module.GetSelectedBranch().Returns("main");
        CreateWorktreeViewModel viewModel = new(new FakeDialogHost(), RepositoryPath, RepositoryPath, module,
            new RepositoryOperationsViewModel(new FakeGitOperations()),
            new InMemoryAppPreferences { AutoNormaliseBranchName = true, AutoNormaliseSymbol = "_" });

        await viewModel.LoadAsync();
        viewModel.CanCheckoutExistingBranch.Should().BeFalse("no other branch exists");
        viewModel.CreateNewBranch.Should().BeTrue();
        viewModel.CreateCommand.CanExecute(null).Should().BeFalse("a new branch needs a name");

        viewModel.NewBranchName = "wt branch";
        viewModel.NormaliseNewBranchName();

        viewModel.NewBranchName.Should().Be("wt_branch");
        viewModel.Directory.Should().EndWith("wt_branch");
        viewModel.CreateCommand.CanExecute(null).Should().BeTrue();
    }

    [Test]
    public async Task SubmodulesViewModel_should_select_the_first_and_remove_only_after_the_question()
    {
        IGitModule module = Substitute.For<IGitModule>();
        GitSubmoduleInfo lib = new("lib", "lib", "https://example.com/lib.git", _first, "main", isInitialized: true,
            isUpToDate: true);
        module.GetSubmodulesInfo().Returns([lib]);
        module.WorkingDir.Returns(RepositoryPath);
        List<string> pulled = [];
        FakeDialogHost host = new() { Answer = false };
        SubmodulesViewModel viewModel = new(host, module, _ => Task.FromResult(true),
            path =>
            {
                pulled.Add(path);
                return Task.CompletedTask;
            },
            () => Task.CompletedTask);

        await viewModel.ReloadAsync();
        await viewModel.RemoveCommand.ExecuteAsync(null);
        await viewModel.PullCommand.ExecuteAsync(null);

        viewModel.Selected.Should().BeSameAs(lib);
        host.Questions.Should().ContainSingle();
        pulled.Should().Equal(Path.Combine(RepositoryPath, "lib"));
    }
}

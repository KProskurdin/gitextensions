using System.ComponentModel;
using AwesomeAssertions;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Settings;
using GitUIPluginInterfaces;
using NSubstitute;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class CreateBranchViewModelTests
{
    private const string RepositoryPath = "/work/repo";

    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);
    private static readonly ObjectId _head = ObjectId.Parse("1234567812345678123456781234567812345678");
    private static readonly ObjectId _other = ObjectId.Parse("abcdefabcdefabcdefabcdefabcdefabcdefabcd");

    private FakeDialogHost _host = null!;
    private FakeGitOperations _git = null!;
    private IGitModule _module = null!;

    [SetUp]
    public void Setup()
    {
        _host = new FakeDialogHost();
        _git = new FakeGitOperations();
        _module = Substitute.For<IGitModule>();
        _module.WorkingDir.Returns(RepositoryPath);
        _module.GetCurrentCheckout().Returns(_head);
        GitRevision head = Revision(_head, "second", Branch("main"));
        GitRevision other = Revision(_other, "first");
        _module.GetRevision(_head, Arg.Any<bool>(), Arg.Any<bool>()).Returns(head);
        _module.GetRevision(_other, Arg.Any<bool>(), Arg.Any<bool>()).Returns(other);
        _module.CheckBranchFormat(Arg.Any<string>()).Returns(true);
    }

    [Test]
    public async Task LoadAsync_should_propose_the_commits_branch_and_show_its_summary()
    {
        CreateBranchViewModel viewModel = NewViewModel();

        await viewModel.LoadAsync();

        viewModel.Dialog.BranchName.Should().Be("main");
        viewModel.Dialog.Revision.Should().Be(_head);
        viewModel.Summary!.Subject.Should().Be("second");
        viewModel.Summary.Branches.Should().Be("main");
        viewModel.CanPickRevision.Should().BeTrue();
        viewModel.OrphanOnlyText.Should().BeNull();
        viewModel.Loaded.IsCompleted.Should().BeTrue();
    }

    [Test]
    public async Task LoadAsync_without_commits_should_allow_an_orphan_only_with_upstreams_translated_label()
    {
        _module.GetCurrentCheckout().Returns(default(ObjectId));
        CreateBranchViewModel viewModel = NewViewModel(checkoutAfterCreation: true);

        await viewModel.LoadAsync();

        viewModel.Dialog.IsOrphanOnly.Should().BeTrue();
        viewModel.Dialog.CreateOrphan.Should().BeTrue();
        viewModel.CanPickRevision.Should().BeFalse();
        viewModel.Summary.Should().BeNull();
        viewModel.OrphanOnlyText.Should().Be(FakeDialogHost.TranslatedMark + CreateBranchDialog.CreatingOrphanBranch);
    }

    [Test]
    public async Task LoadAsync_should_show_an_error_and_still_complete_when_git_fails()
    {
        _module.GetCurrentCheckout().Returns(_ => throw new InvalidOperationException("no git"));
        CreateBranchViewModel viewModel = NewViewModel();

        await viewModel.LoadAsync();

        _host.Errors.Should().ContainSingle().Which.Message.Should().Be("no git");
        viewModel.Loaded.IsCompleted.Should().BeTrue();
    }

    [Test]
    public async Task Choosing_an_orphan_should_check_out_and_turn_the_commit_picker_off()
    {
        CreateBranchViewModel viewModel = NewViewModel(checkoutAfterCreation: false);
        await viewModel.LoadAsync();
        List<string?> changed = [];
        viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        viewModel.Dialog.CreateOrphan = true;

        viewModel.Dialog.CheckoutAfterCreate.Should().BeTrue();
        viewModel.Dialog.CanClearOrphan.Should().BeTrue();
        viewModel.CanPickRevision.Should().BeFalse();
        changed.Should().Contain(nameof(CreateBranchViewModel.CanPickRevision));
    }

    [Test]
    public async Task Picking_another_commit_should_show_its_summary()
    {
        CreateBranchViewModel viewModel = NewViewModel();
        await viewModel.LoadAsync();

        viewModel.Dialog.Revision = _other;

        await WaitUntilAsync(() => viewModel.Summary?.Subject == "first");
    }

    [Test]
    public async Task NormaliseName_should_fix_the_name_when_auto_normalise_is_on()
    {
        InMemoryAppPreferences preferences = new() { AutoNormaliseBranchName = true, AutoNormaliseSymbol = "_" };
        CreateBranchViewModel viewModel = NewViewModel(preferences: preferences);
        await viewModel.LoadAsync();
        viewModel.Dialog.BranchName = "my feature";

        viewModel.NormaliseName();

        viewModel.Dialog.BranchName.Should().Be("my_feature");
    }

    [Test]
    public async Task Create_with_a_name_git_refuses_should_show_upstreams_message_and_keep_the_window_open()
    {
        _module.CheckBranchFormat("bad..name").Returns(false);

        // Normalising would fix the name before git sees it.
        CreateBranchViewModel viewModel = NewViewModel(preferences: new InMemoryAppPreferences { AutoNormaliseBranchName = false });
        await viewModel.LoadAsync();
        viewModel.Dialog.BranchName = "bad..name";

        await viewModel.CreateCommand.ExecuteAsync(null);

        _host.Messages.Should().Equal(
            string.Format(FakeDialogHost.TranslatedMark + CreateBranchDialog.BranchNameIsNotValid, "bad..name"));
        _host.IsClosed.Should().BeFalse();
        _git.Count.Should().Be(0);
    }

    [Test]
    public async Task Create_should_run_upstreams_commands_and_close_with_true()
    {
        CreateBranchViewModel viewModel = NewViewModel();
        await viewModel.LoadAsync();
        viewModel.Dialog.BranchName = "feature";
        viewModel.Dialog.Revision = _other;

        Task create = viewModel.CreateCommand.ExecuteAsync(null);
        await WaitUntilAsync(() => _git.Count == 1);
        _git.NameAt(0).Should().Be("RunCommands");
        _git.ArgumentsAt(0).Should().Contain(RepositoryPath).And.Contain("feature");
        _git.Complete(0);
        await create;

        _host.Result.Should().Be(true);
        viewModel.CheckedOutOtherCommit.Should().BeTrue("the branch was checked out at another commit than HEAD");
    }

    [Test]
    public void Cancel_should_close_with_false()
    {
        CreateBranchViewModel viewModel = NewViewModel();

        viewModel.CancelCommand.Execute(null);

        _host.Result.Should().Be(false);
    }

    [Test]
    public async Task Replacing_the_dialog_on_load_should_raise_its_bound_properties()
    {
        CreateBranchViewModel viewModel = NewViewModel();
        List<string?> changed = [];
        ((INotifyPropertyChanged)viewModel).PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        await viewModel.LoadAsync();

        changed.Should().Contain([
            nameof(CreateBranchViewModel.Dialog), nameof(CreateBranchViewModel.CanPickRevision),
            nameof(CreateBranchViewModel.OrphanOnlyText), nameof(CreateBranchViewModel.Summary)
        ]);
    }

    private CreateBranchViewModel NewViewModel(bool checkoutAfterCreation = true, IAppPreferences? preferences = null)
        => new(_host, _module, new RepositoryOperationsViewModel(_git), preferences ?? new InMemoryAppPreferences(),
            checkoutAfterCreation: checkoutAfterCreation);

    private static GitRevision Revision(ObjectId id, string subject, params IGitRef[] refs)
        => new(id) { Subject = subject, Author = "Test", Refs = refs };

    private static IGitRef Branch(string name)
    {
        IGitRef branch = Substitute.For<IGitRef>();
        branch.IsHead.Returns(true);
        branch.LocalName.Returns(name);
        return branch;
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow + _timeout;
        while (!condition())
        {
            DateTime.UtcNow.Should().BeBefore(deadline, "the view model finishes its work");
            await Task.Delay(10);
        }
    }
}

using AwesomeAssertions;
using GitCommands;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Settings;
using GitUIPluginInterfaces;
using NSubstitute;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

// The fast-forward check runs git (GitExecutable), so the window's tests cover it; these cover the rest without git.
internal sealed class ResetAnotherBranchViewModelTests
{
    private static readonly ObjectId _target = ObjectId.Parse("1234567812345678123456781234567812345678");
    private static readonly ObjectId _elsewhere = ObjectId.Parse("abcdefabcdefabcdefabcdefabcdefabcdefabcd");

    private FakeDialogHost _host = null!;
    private IGitModule _module = null!;
    private InMemoryAppPreferences _preferences = null!;
    private List<string> _ran = null!;
    private List<string> _checkedOut = null!;

    [SetUp]
    public void Setup()
    {
        _host = new FakeDialogHost();
        _module = Substitute.For<IGitModule>();
        _module.RevParse("HEAD~1").Returns(_target);
        GitRevision revision = new(_target) { Subject = "first", Author = "Test" };
        _module.GetRevision(_target, Arg.Any<bool>(), Arg.Any<bool>()).Returns(revision);
        _module.GetSelectedBranch(Arg.Any<bool>()).Returns("main");
        IGitRef main = Head("main", _elsewhere);
        IGitRef feature = Head("feature", _elsewhere);
        _module.GetRefs(RefsFilter.Heads).Returns([main, feature]);
        _preferences = new InMemoryAppPreferences { CheckoutOtherBranchAfterReset = false };
        _ran = [];
        _checkedOut = [];
    }

    [Test]
    public async Task LoadAsync_should_list_the_branches_but_the_current_one()
    {
        ResetAnotherBranchViewModel viewModel = NewViewModel();

        await viewModel.LoadAsync();

        viewModel.BranchNames.Should().Equal("refs/heads/feature");
        viewModel.Summary!.Subject.Should().Be("first");
        viewModel.CanReset.Should().BeFalse("no branch is chosen");
    }

    [Test]
    public async Task A_name_that_is_not_listed_should_be_marked_and_refused()
    {
        ResetAnotherBranchViewModel viewModel = NewViewModel();
        await viewModel.LoadAsync();

        viewModel.BranchText = "nothing";
        viewModel.Force = true;

        viewModel.IsBranchUnlisted.Should().BeTrue();
        viewModel.CanReset.Should().BeFalse();
        viewModel.ResetCommand.CanExecute(null).Should().BeFalse();
    }

    [Test]
    public async Task A_forced_reset_should_move_the_branch_check_it_out_when_asked_and_close_with_true()
    {
        ResetAnotherBranchViewModel viewModel = NewViewModel();
        await viewModel.LoadAsync();
        viewModel.Force = true;
        viewModel.BranchText = "refs/heads/feature";
        viewModel.CheckoutAfterReset = true;
        viewModel.ResetCommand.CanExecute(null).Should().BeTrue();

        await viewModel.ResetCommand.ExecuteAsync(null);

        _ran.Should().ContainSingle().Which.Should().Contain("update-ref").And.Contain("refs/heads/feature");
        _checkedOut.Should().Equal("refs/heads/feature");
        _preferences.CheckoutOtherBranchAfterReset.Should().BeTrue("the choice is kept in upstream's setting");
        _host.Result.Should().Be(true);
    }

    private ResetAnotherBranchViewModel NewViewModel()
        => new(_host, _module, "HEAD~1", _preferences,
            arguments =>
            {
                _ran.Add(arguments.ToString());
                return Task.FromResult(true);
            },
            branch =>
            {
                _checkedOut.Add(branch);
                return Task.CompletedTask;
            });

    private static IGitRef Head(string name, ObjectId at)
    {
        IGitRef head = Substitute.For<IGitRef>();
        head.IsHead.Returns(true);
        head.Name.Returns("refs/heads/" + name);
        head.LocalName.Returns(name);
        head.CompleteName.Returns("refs/heads/" + name);
        head.ObjectId.Returns(at);
        return head;
    }
}

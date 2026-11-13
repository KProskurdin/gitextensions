using AwesomeAssertions;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Settings;
using GitUIPluginInterfaces;
using NSubstitute;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class ResetCurrentBranchViewModelTests
{
    private const string RepositoryPath = "/work/repo";

    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);
    private static readonly ObjectId _head = ObjectId.Parse("1234567812345678123456781234567812345678");
    private static readonly ObjectId _target = ObjectId.Parse("abcdefabcdefabcdefabcdefabcdefabcdefabcd");

    private FakeDialogHost _host = null!;
    private FakeGitOperations _git = null!;
    private IGitModule _module = null!;
    private List<string> _submoduleUpdates = null!;

    [SetUp]
    public void Setup()
    {
        _host = new FakeDialogHost();
        _git = new FakeGitOperations();
        _module = Substitute.For<IGitModule>();
        _module.WorkingDir.Returns(RepositoryPath);
        _module.GetSelectedBranch(Arg.Any<bool>()).Returns("main");
        _module.GetCurrentCheckout().Returns(_head);
        _module.RevParse("HEAD~1").Returns(_target);
        GitRevision revision = new(_target) { Subject = "first", Author = "Test" };
        _module.GetRevision(_target, Arg.Any<bool>(), Arg.Any<bool>()).Returns(revision);
        _submoduleUpdates = [];
    }

    [Test]
    public async Task LoadAsync_should_name_the_branch_and_show_the_commit()
    {
        ResetCurrentBranchViewModel viewModel = NewViewModel(ResetMode.Mixed);

        await viewModel.LoadAsync();

        viewModel.BranchInfo.Should().Be(FakeDialogHost.TranslatedMark + "Reset branch 'main' to revision:");
        viewModel.Summary!.Subject.Should().Be("first");
        viewModel.Dialog.Mode.Should().Be(ResetMode.Mixed);
    }

    [Test]
    public async Task A_hard_reset_answered_no_should_change_nothing()
    {
        _host.Answer = false;
        ResetCurrentBranchViewModel viewModel = NewViewModel(ResetMode.Soft);
        await viewModel.LoadAsync();
        viewModel.Dialog.Mode = ResetMode.Hard;

        await viewModel.ResetCommand.ExecuteAsync(null);

        _host.Questions.Should().Equal(FakeDialogHost.TranslatedMark + ResetCurrentBranchDialog.ResetHardWarning);
        _git.Count.Should().Be(0);
        _host.IsClosed.Should().BeFalse();
    }

    [Test]
    public async Task A_hard_reset_answered_yes_should_reset_update_the_submodules_and_close_with_true()
    {
        ResetCurrentBranchViewModel viewModel = NewViewModel(ResetMode.Hard);
        await viewModel.LoadAsync();

        Task reset = viewModel.ResetCommand.ExecuteAsync(null);
        await WaitUntilAsync(() => _git.Count == 1);
        _git.ArgumentsAt(0).Should().Contain(_target.ToString());
        _git.Complete(0);
        await reset;

        _submoduleUpdates.Should().Equal(RepositoryPath);
        _host.Result.Should().Be(true);
    }

    [Test]
    public async Task A_soft_reset_should_not_ask()
    {
        ResetCurrentBranchViewModel viewModel = NewViewModel(ResetMode.Soft);
        await viewModel.LoadAsync();

        Task reset = viewModel.ResetCommand.ExecuteAsync(null);
        await WaitUntilAsync(() => _git.Count == 1);
        _git.Complete(0);
        await reset;

        _host.Questions.Should().BeEmpty();
        _submoduleUpdates.Should().BeEmpty("only a hard reset or the checkout setting updates them");
    }

    private ResetCurrentBranchViewModel NewViewModel(ResetMode mode)
        => new(_host, _module, new RepositoryOperationsViewModel(_git), new InMemoryAppPreferences(), "HEAD~1", mode,
            path =>
            {
                _submoduleUpdates.Add(path);
                return Task.FromResult(true);
            });

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

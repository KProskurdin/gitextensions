using AwesomeAssertions;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Operations;
using NSubstitute;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class CheckoutRevisionViewModelTests
{
    private const string RepositoryPath = "/work/repo";

    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);
    private static readonly ObjectId _head = ObjectId.Parse("1234567812345678123456781234567812345678");
    private static readonly ObjectId _target = ObjectId.Parse("abcdefabcdefabcdefabcdefabcdefabcdefabcd");

    private FakeDialogHost _host = null!;
    private FakeGitOperations _git = null!;
    private IGitModule _module = null!;
    private List<string> _scripted = null!;
    private List<string> _submoduleUpdates = null!;

    [SetUp]
    public void Setup()
    {
        _host = new FakeDialogHost();
        _git = new FakeGitOperations();
        _module = Substitute.For<IGitModule>();
        _module.WorkingDir.Returns(RepositoryPath);
        _module.GetCurrentCheckout().Returns(_head);
        _module.RevParse("v1").Returns(_target);
        _scripted = [];
        _submoduleUpdates = [];
    }

    [Test]
    public async Task LoadAsync_should_propose_the_given_commit()
    {
        CheckoutRevisionViewModel viewModel = NewViewModel("v1");

        await viewModel.LoadAsync();

        viewModel.Dialog.Revision.Should().Be(_target);
        viewModel.Loaded.IsCompleted.Should().BeTrue();
    }

    [Test]
    public async Task LoadAsync_should_leave_no_commit_for_one_git_does_not_know()
    {
        CheckoutRevisionViewModel viewModel = NewViewModel("unknown");

        await viewModel.LoadAsync();

        viewModel.Dialog.Revision.Should().BeNull();
    }

    [Test]
    public async Task Checkout_without_a_commit_should_show_upstreams_message_and_keep_the_window_open()
    {
        CheckoutRevisionViewModel viewModel = NewViewModel(null);
        await viewModel.LoadAsync();

        await viewModel.CheckoutCommand.ExecuteAsync(null);

        _host.Messages.Should().Equal(FakeDialogHost.TranslatedMark + CheckoutRevisionDialog.NoRevisionSelected);
        _host.IsClosed.Should().BeFalse();
        _scripted.Should().BeEmpty();
    }

    [Test]
    public async Task Checkout_should_run_between_the_scripts_update_the_submodules_and_close_with_true()
    {
        CheckoutRevisionViewModel viewModel = NewViewModel("v1");
        await viewModel.LoadAsync();
        viewModel.Dialog.Force = true;

        Task checkout = viewModel.CheckoutCommand.ExecuteAsync(null);
        await WaitUntilAsync(() => _git.Count == 1);
        _git.ArgumentsAt(0).Should().Contain(_target.ToShortString()).And.Contain("--force");
        _git.Complete(0);
        await checkout;

        _scripted.Should().Equal(RepositoryPath);
        _submoduleUpdates.Should().Equal(RepositoryPath);
        _host.Result.Should().Be(true);
    }

    [Test]
    public async Task Checkout_of_the_current_commit_should_not_update_the_submodules()
    {
        _module.RevParse("HEAD").Returns(_head);
        CheckoutRevisionViewModel viewModel = NewViewModel("HEAD");
        await viewModel.LoadAsync();

        Task checkout = viewModel.CheckoutCommand.ExecuteAsync(null);
        await WaitUntilAsync(() => _git.Count == 1);
        _git.Complete(0);
        await checkout;

        _submoduleUpdates.Should().BeEmpty();
        _host.Result.Should().Be(true);
    }

    private CheckoutRevisionViewModel NewViewModel(string? revision)
        => new(_host, _module, new RepositoryOperationsViewModel(_git), revision,
            (path, checkout) =>
            {
                _scripted.Add(path);
                return checkout();
            },
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

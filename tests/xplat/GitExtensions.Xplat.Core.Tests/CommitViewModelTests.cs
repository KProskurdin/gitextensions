using AwesomeAssertions;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;
using GitUI.ScriptsEngine;
using NSubstitute;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

// The file lists, the diff and the message's menus are the window's; these cover the message, amend and the commit.
internal sealed class CommitViewModelTests
{
    private const string RepositoryPath = "repo";

    private readonly List<Confirmation> _confirmations = [];
    private readonly List<ScriptEvent> _scripts = [];
    private FakeGitOperations _operations = null!;
    private ICommitMessageStore _messages = null!;
    private IRepositoryService _service = null!;
    private InMemoryAppPreferences _preferences = null!;
    private bool _beforeCommitPasses;

    [SetUp]
    public void SetUp()
    {
        _confirmations.Clear();
        _scripts.Clear();
        _operations = new FakeGitOperations();
        _messages = Substitute.For<ICommitMessageStore>();
        _service = Substitute.For<IRepositoryService>();
        _preferences = new InMemoryAppPreferences();
        _beforeCommitPasses = true;
    }

    [Test]
    public async Task LoadAsync_should_start_from_the_draft()
    {
        Task<string> draft = Task.FromResult("draft\n");
        _messages.LoadAsync(RepositoryPath).Returns(draft);
        CommitViewModel viewModel = New(new FakeDialogHost());

        await viewModel.LoadAsync();

        viewModel.Message.Should().Be("draft");
        viewModel.Amend.Should().BeFalse();
    }

    [Test]
    public void Amend_should_show_the_head_message_and_unchecking_it_should_put_back_the_typed_one()
    {
        Task<string> head = Task.FromResult("head message");
        _service.GetHeadMessageAsync(RepositoryPath).Returns(head);
        CommitViewModel viewModel = New(new FakeDialogHost());
        viewModel.Message = "typed";

        viewModel.Amend = true;

        viewModel.Message.Should().Be("head message");
        viewModel.CommitAndPushContent.Should().Be(CommitViewModel.CommitAndPushText);

        viewModel.Amend = false;

        viewModel.Message.Should().Be("typed");
    }

    [Test]
    public async Task Commit_should_commit_the_message_reset_the_draft_and_close()
    {
        FakeDialogHost host = new();
        CommitViewModel viewModel = New(host);
        viewModel.Message = "subject";
        viewModel.SignOff = true;

        Task commit = viewModel.CommitCommand.ExecuteAsync(null);
        _operations.Complete(0);
        await commit;

        _confirmations.Should().Equal(Confirmation.CommitWithoutBranch);
        _operations.NameAt(0).Should().Be("Commit");
        _operations.ArgumentsAt(0).Should().StartWith($"{RepositoryPath} subject")
            .And.Contain("amend=False signOff=True");
        _scripts.Should().Equal(ScriptEvent.BeforeCommit, ScriptEvent.AfterCommit);
        await _messages.Received(1).ResetAsync(RepositoryPath);
        viewModel.Message.Should().BeEmpty();
        host.IsClosed.Should().BeTrue();
    }

    [Test]
    public async Task A_before_commit_script_should_stop_the_commit()
    {
        _beforeCommitPasses = false;
        FakeDialogHost host = new();
        CommitViewModel viewModel = New(host);
        viewModel.Message = "subject";

        await viewModel.CommitCommand.ExecuteAsync(null);

        _operations.Count.Should().Be(0);
        viewModel.Message.Should().Be("subject");
        host.IsClosed.Should().BeFalse();
    }

    [Test]
    public void A_fixup_message_should_be_read_only_until_modified()
    {
        CommitViewModel viewModel = New(new FakeDialogHost());

        viewModel.StartSpecialCommit(CommitKind.Fixup, "subject", "subject\n\nbody");

        viewModel.Message.Should().StartWith("fixup! subject");
        (viewModel.CanChangeMessage, viewModel.IsModifyMessageVisible, viewModel.KeepsDraft).Should()
            .Be((false, true, false));

        viewModel.ModifyMessageCommand.Execute(null);

        (viewModel.CanChangeMessage, viewModel.IsModifyMessageVisible).Should().Be((true, false));
    }

    [Test]
    public void A_specific_GPG_key_should_show_its_box_and_the_key_on_the_button()
    {
        CommitViewModel viewModel = New(new FakeDialogHost());

        viewModel.GpgSignIndex = CommitViewModel.GpgSpecificKey;

        viewModel.IsGpgKeyVisible.Should().BeTrue();
        viewModel.CommitIcon.Should().Be("Key");
    }

    private CommitViewModel New(FakeDialogHost host)
        => new(host, RepositoryPath, new RepositoryViewModel(new FakeRepositoryService()),
            new RepositoryOperationsViewModel(_operations), _service, _preferences, () => Task.CompletedTask, _messages,
            new CommitViewModelServices
            {
                ConfirmSuppressible = (confirmation, _, _, _) =>
                {
                    _confirmations.Add(confirmation);
                    return Task.FromResult(true);
                },
                AskValidation = (_, _) => Task.FromResult(true),
                RunScripts = scriptEvent =>
                {
                    _scripts.Add(scriptEvent);
                    return Task.FromResult(scriptEvent != ScriptEvent.BeforeCommit || _beforeCommitPasses);
                },
                Push = _ => Task.FromResult(true),
                AskResetChanges = (_, _) => Task.FromResult(ResetChangesAction.Cancel),
                CreateBranch = () => Task.CompletedTask,
                ShowError = (_, _) => { },
                ResolveCommit = _ => Task.FromResult<string?>("parent"),
            });
}

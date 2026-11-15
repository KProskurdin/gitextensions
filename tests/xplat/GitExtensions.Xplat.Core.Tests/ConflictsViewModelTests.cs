using AwesomeAssertions;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Platform;
using GitExtensions.Xplat.Core.Settings;
using NSubstitute;
using NUnit.Framework;
using TaskDialogButton = System.Windows.Forms.TaskDialogButton;
using TaskDialogPage = System.Windows.Forms.TaskDialogPage;

namespace GitExtensions.Xplat.Core.Tests;

// The merge tool's run, staging and deleting go through git (GitExecutable), so the window's tests cover them; these cover
// the rest with a substituted module.
internal sealed class ConflictsViewModelTests
{
    private const string Mark = FakeDialogHost.TranslatedMark;

    private static readonly ObjectId _id = ObjectId.Parse("0123456789abcdef0123456789abcdef01234567");

    private readonly List<Confirmation> _confirmations = [];
    private readonly List<TaskDialogPage> _pages = [];
    private int _commitsOffered;
    private FakeGitOperations _operations = null!;

    [SetUp]
    public void SetUp()
    {
        _confirmations.Clear();
        _pages.Clear();
        _commitsOffered = 0;
        _operations = new FakeGitOperations();
    }

    [Test]
    public async Task LoadAsync_should_list_the_files_and_describe_the_first_one()
    {
        IGitModule module = Module(conflicts: [Conflict("a.txt"), Conflict("b.txt")]);
        ConflictsViewModel viewModel = New(new FakeDialogHost(), module, tools: ["kdiff3", "meld"]);

        await viewModel.LoadAsync();

        viewModel.Rows.Select(row => row.ToString()).Should().Equal("a.txt", "b.txt");
        viewModel.SelectedIndex.Should().Be(0);
        viewModel.SelectedRows.Should().Equal(viewModel.Rows[0]);
        viewModel.Description.Should().Be(string.Format(Mark + ConflictsDialog.FileChangeLocallyAndRemotely, "ours", "theirs"));
        (viewModel.LocalName, viewModel.BaseName, viewModel.RemoteName).Should().Be(("a.txt", "a.txt", "a.txt"));
        viewModel.LocalLabel.Should().Be("[t]Local/current (ours)");
        viewModel.ChooseLocalText.Should().Be(Mark + ConflictsDialog.ChooseLocalMergeText);
        viewModel.OpenInText.Should().Be(Mark + "Open in fake");
        viewModel.CanOpenMergeTool.Should().BeTrue();
        viewModel.MergeTools.Should().Equal("kdiff3", "meld");
        viewModel.Loaded.IsCompleted.Should().BeTrue();
    }

    [Test]
    public async Task LoadAsync_should_swap_the_sides_during_a_rebase()
    {
        IGitModule module = Module(conflicts: [Conflict("a.txt")]);
        module.InTheMiddleOfRebase().Returns(true);
        ConflictsViewModel viewModel = New(new FakeDialogHost(), module);

        await viewModel.LoadAsync();

        viewModel.ChooseLocalText.Should().Be(Mark + ConflictsDialog.ChooseLocalRebaseText);
        viewModel.ChooseRemoteText.Should().Be(Mark + ConflictsDialog.ChooseRemoteRebaseText);
        viewModel.MergeTools.Should().BeEmpty("one tool is no choice");
    }

    [Test]
    public async Task Selecting_several_files_or_a_file_without_a_side_should_disable_what_needs_it()
    {
        IGitModule module = Module(conflicts: [Conflict("a.txt", remote: false), Conflict("b.txt")]);
        ConflictsViewModel viewModel = New(new FakeDialogHost(), module);
        await viewModel.LoadAsync();

        (viewModel.HasOneFile, viewModel.CanUseLocal, viewModel.CanUseRemote, viewModel.CanUseBase).Should()
            .Be((true, true, false, true));
        viewModel.RemoteName.Should().Be(Mark + ConflictsDialog.Deleted);

        viewModel.SelectedRows = viewModel.Rows;

        (viewModel.HasOneFile, viewModel.CanOpenMergeTool, viewModel.CanUseLocal).Should().Be((false, false, false));
        (viewModel.LocalName, viewModel.BaseName, viewModel.RemoteName).Should().Be(("", "", ""));
    }

    [Test]
    public async Task Choosing_a_side_should_resolve_the_file_offer_to_commit_and_close()
    {
        IGitModule module = Module(conflicts: [Conflict("a.txt")]);
        module.GetConflictsAsync(Arg.Any<string?>()).Returns(
            Task.FromResult<List<ConflictData>>([Conflict("a.txt")]), Task.FromResult<List<ConflictData>>([]));
        module.InTheMiddleOfConflictedMerge(Arg.Any<bool>()).Returns(true, true, false);
        module.HandleConflictSelectSide("a.txt", ConflictsDialog.Remote).Returns(true);
        FakeDialogHost host = new();
        ConflictsViewModel viewModel = New(host, module);
        await viewModel.LoadAsync();

        await viewModel.ChooseCommand.ExecuteAsync(ConflictsDialog.Remote);

        module.Received(1).HandleConflictSelectSide("a.txt", ConflictsDialog.Remote);
        _confirmations.Should().Equal(Confirmation.CommitAfterConflictsResolved);
        _commitsOffered.Should().Be(1);
        host.IsClosed.Should().BeTrue();
        viewModel.IsProgressVisible.Should().BeFalse();
    }

    [Test]
    public async Task A_side_that_cannot_be_chosen_should_say_so()
    {
        IGitModule module = Module(conflicts: [Conflict("a.txt")]);
        FakeDialogHost host = new();
        ConflictsViewModel viewModel = New(host, module);
        await viewModel.LoadAsync();

        await viewModel.ChooseCommand.ExecuteAsync(ConflictsDialog.Local);

        host.Messages.Should().Equal(Mark + ConflictsDialog.ChooseLocalFileFailed);
        host.IsClosed.Should().BeFalse();
    }

    [Test]
    public async Task Merging_a_file_deleted_remotely_should_ask_which_side_to_keep()
    {
        IGitModule module = Module(conflicts: [Conflict("a.txt", remote: false)]);
        ConflictsViewModel viewModel = New(new FakeDialogHost(), module);
        await viewModel.LoadAsync();

        await viewModel.MergeCommand.ExecuteAsync(null);

        _pages.Should().ContainSingle();
        _pages[0].Caption.Should().Be(Mark + ConflictsDialog.SolveMergeConflictCaption);
        _pages[0].Text.Should().Be(string.Format(Mark + ConflictsDialog.FileModifiedLocallyAndDeletedRemotelyLong, "a.txt",
            "ours", "theirs"));
        _pages[0].Buttons.Select(button => button.Text).Should().Equal(TaskDialogButton.Cancel.Text,
            $"{Mark}{ConflictsDialog.KeepModifiedButtonText} (ours)", $"{Mark}{ConflictsDialog.DeleteFileButtonText} (theirs)",
            Mark + ConflictsDialog.KeepBaseButtonText);
        module.DidNotReceive().HandleConflictSelectSide(Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Reset_should_do_nothing_unless_both_questions_are_answered_yes()
    {
        IGitModule module = Module(conflicts: [Conflict("a.txt")]);
        FakeDialogHost host = new() { Answer = false };
        ConflictsViewModel viewModel = New(host, module);
        await viewModel.LoadAsync();

        await viewModel.ResetCommand.ExecuteAsync(null);

        host.Questions.Should().Equal(Mark + ConflictsDialog.AbortCurrentOperation);
        _confirmations.Should().BeEmpty();
        _operations.Count.Should().Be(0);
        host.IsClosed.Should().BeFalse();
    }

    [Test]
    public async Task A_missing_merge_tool_should_be_reported_when_loading()
    {
        IGitModule module = Module(conflicts: [Conflict("a.txt")]);
        FakeDialogHost host = new();
        ConflictsViewModel viewModel = New(host, module, tool: new MergeTool(null, null, null, "no tool"));

        await viewModel.LoadAsync();

        host.Messages.Should().Equal("no tool");
        viewModel.CanOpenMergeTool.Should().BeFalse();
        viewModel.Rows.Should().ContainSingle();
    }

    private static IGitModule Module(List<ConflictData> conflicts)
    {
        IGitModule module = Substitute.For<IGitModule>();
        Task<List<ConflictData>> read = Task.FromResult(conflicts);
        module.GetConflictsAsync(Arg.Any<string?>()).Returns(read);
        module.InTheMiddleOfConflictedMerge(Arg.Any<bool>()).Returns(true);
        return module;
    }

    private static ConflictData Conflict(string fileName, bool remote = true)
        => new(new ConflictedFileData(_id, fileName), new ConflictedFileData(_id, fileName),
            remote ? new ConflictedFileData(_id, fileName) : default);

    private ConflictsViewModel New(FakeDialogHost host, IGitModule module, IReadOnlyList<string>? tools = null,
        MergeTool? tool = null)
        => new(host, module, Path.Combine(Path.GetTempPath(), "no-such-repository-" + Guid.NewGuid().ToString("N")),
            new RepositoryOperationsViewModel(_operations), new ConflictsViewModelServices
            {
                RefreshRepository = () => Task.CompletedTask,
                ConfirmSuppressible = (confirmation, _, _) =>
                {
                    _confirmations.Add(confirmation);
                    return Task.FromResult(true);
                },
                ShowTaskDialog = page =>
                {
                    _pages.Add(page);
                    return Task.FromResult<TaskDialogButton?>(null);
                },
                MergeSubmodule = _ => Task.FromResult(false),
                PickSaveFile = (_, _) => Task.FromResult<string?>(null),
                LaunchFile = _ => Task.CompletedTask,
                FileManager = Substitute.For<IFileManager>(),
                ShowFileHistory = _ => { },
                ShowCommit = () => _commitsOffered++,
                AvailableMergeTools = () => Task.FromResult(tools ?? ["fake"]),
                ReadMergeTool = () => tool ?? new MergeTool("fake", "\"$MERGED\"", "/bin/fake", null),
            });
}

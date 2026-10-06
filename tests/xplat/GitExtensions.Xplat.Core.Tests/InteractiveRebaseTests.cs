using AwesomeAssertions;
using GitExtensions.Xplat.Core.Operations;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class InteractiveRebaseTests
{
    private const string RepositoryPath = "/work/one";
    private const string Editor = "'/apps/GitExtensions' fileeditor";

    private FakeGitOperations _git = null!;
    private RepositoryOperationsViewModel _viewModel = null!;

    [SetUp]
    public void Setup()
    {
        _git = new FakeGitOperations();
        _viewModel = new RepositoryOperationsViewModel(_git);
    }

    [Test]
    public void Build_should_quote_the_app_and_append_the_upstream_verb()
    {
        string app = Path.Combine(Path.GetTempPath(), "Git Extensions", "GitExtensions");

        string command = GitEditorCommand.Build(app, entryAssemblyPath: app + ".dll");

        command.Should().Be($"'{app.Replace('\\', '/')}' fileeditor");
    }

    [Test]
    public void Build_should_pass_the_entry_assembly_when_the_app_runs_under_the_dotnet_host()
    {
        string host = Path.Combine(Path.GetTempPath(), "dotnet", OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet");
        string assembly = Path.Combine(Path.GetTempPath(), "app", "GitExtensions.dll");

        string command = GitEditorCommand.Build(host, assembly);

        command.Should().Be($"'{host.Replace('\\', '/')}' '{assembly.Replace('\\', '/')}' fileeditor");
    }

    [Test]
    public void Build_should_escape_a_single_quote_for_the_shell()
    {
        string app = Path.Combine(Path.GetTempPath(), "it's", "GitExtensions");

        GitEditorCommand.Build(app, entryAssemblyPath: null).Should().Contain(@"it'\''s");
    }

    [Test]
    public void Environment_should_set_the_sequence_editor_and_the_message_editor()
    {
        GitEditorCommand.Environment(Editor).Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["GIT_SEQUENCE_EDITOR"] = Editor,
            ["GIT_EDITOR"] = Editor,
        });
    }

    [Test]
    public async Task RebaseInteractiveAsync_without_an_editor_should_not_run_git()
    {
        bool rebased = await _viewModel.RebaseInteractiveAsync(RepositoryPath, "0123456789abcdef");

        rebased.Should().BeFalse();
        _git.Count.Should().Be(0);
        _viewModel.ErrorMessage.Should().Be("No editor is available for the rebase todo list.");
    }

    [Test]
    public async Task RebaseInteractiveAsync_should_run_as_an_output_operation_with_the_editor()
    {
        _viewModel.EditorCommand = Editor;
        int started = 0;
        _viewModel.RemoteOperationStarted += (_, _) => started++;

        Task<bool> rebase = _viewModel.RebaseInteractiveAsync(RepositoryPath, "0123456789abcdef");

        started.Should().Be(1);
        _viewModel.OutputTitle.Should().Be("Rebase interactively onto 01234567");
        _git.NameAt(0).Should().Be("RebaseInteractive");
        _git.ArgumentsAt(0).Should().Be($"{RepositoryPath} 0123456789abcdef editor={Editor}");
        _git.Complete(0);
        (await rebase).Should().BeTrue();
        _viewModel.StatusMessage.Should().Be("Rebased");
    }

    [Test]
    public async Task Continue_skip_and_edit_todo_should_pass_the_editor()
    {
        _viewModel.EditorCommand = Editor;

        Task<bool> continueRebase = _viewModel.ContinueRebaseAsync(RepositoryPath);
        _git.Complete(0);
        await continueRebase;
        Task<bool> skip = _viewModel.SkipRebaseAsync(RepositoryPath);
        _git.Complete(1);
        await skip;
        Task<bool> editTodo = _viewModel.EditRebaseTodoAsync(RepositoryPath);
        _git.Complete(2);
        await editTodo;

        _git.ArgumentsAt(0).Should().Be($"{RepositoryPath} editor={Editor}");
        _git.NameAt(1).Should().Be("SkipRebase");
        _git.ArgumentsAt(1).Should().Be($"{RepositoryPath} editor={Editor}");
        _git.NameAt(2).Should().Be("EditRebaseTodo");
        _viewModel.StatusMessage.Should().Be("Todo list edited");
    }

    [Test]
    public async Task ContinueRebaseAsync_without_an_editor_should_pass_none()
    {
        Task<bool> continueRebase = _viewModel.ContinueRebaseAsync(RepositoryPath);
        _git.Complete(0);
        await continueRebase;

        _git.ArgumentsAt(0).Should().Be($"{RepositoryPath} editor=");
    }
}

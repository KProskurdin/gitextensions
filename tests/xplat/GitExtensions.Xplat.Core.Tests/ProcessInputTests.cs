using AwesomeAssertions;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Settings;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

// Upstream's process dialog input (FormStatus' "Show password input" and PasswordInput, FormProcess.DataReceived).
internal sealed class ProcessInputTests
{
    private const string RepositoryPath = "/work/repo";

    [Test]
    public void Pump_should_report_text_left_without_a_line_end_as_a_prompt_the_rest_of_the_line_replaces()
    {
        List<GitOutputLine> lines = [];

        GitOutputRunner.Pump(new ChunkReader("remote: one\nPassword for 'https://host': ", "\ndone\n"),
            new ImmediateProgress(lines), collected: null);

        lines.Should().Equal(
            new GitOutputLine("remote: one", IsProgress: false),
            new GitOutputLine("Password for 'https://host': ", IsProgress: true, IsPrompt: true),
            new GitOutputLine("Password for 'https://host': ", IsProgress: false),
            new GitOutputLine("done", IsProgress: false));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void StartInfo_should_redirect_the_input_only_when_asked(bool redirectInput)
        => GitOutputRunner.StartInfo("git", "push", RepositoryPath, null, showWindow: false, redirectInput)
            .RedirectStandardInput.Should().Be(redirectInput);

    [Test]
    public async Task The_box_should_show_as_upstreams_setting_says_and_only_while_the_operation_runs()
    {
        FakeGitOperations git = new();
        RepositoryOperationsViewModel actions = new(git);
        InMemoryAppPreferences preferences = new() { ShowProcessDialogPasswordInput = true };
        ProcessViewModel viewModel = new(new FakeDialogHost(), actions, preferences);
        viewModel.ShowPasswordInput.Should().BeTrue();
        viewModel.IsPasswordInputVisible.Should().BeFalse("nothing runs");

        Task fetch = actions.FetchAsync(RepositoryPath, "origin", prune: false);
        viewModel.IsPasswordInputVisible.Should().BeTrue();
        viewModel.ShowPasswordInput = false;
        viewModel.IsPasswordInputVisible.Should().BeFalse();
        preferences.ShowProcessDialogPasswordInput.Should().BeFalse("upstream stores the box's state at once");

        git.Complete(0);
        await fetch;
        viewModel.IsRunning.Should().BeFalse();
        viewModel.Detach();
    }

    [Test]
    public async Task A_prompt_should_show_the_hidden_box_without_storing_it_and_ask_for_the_focus()
    {
        FakeGitOperations git = new();
        RepositoryOperationsViewModel actions = new(git);
        InMemoryAppPreferences preferences = new();
        ProcessViewModel viewModel = new(new FakeDialogHost(), actions, preferences);
        int focusRequests = 0;
        viewModel.PasswordInputFocusRequested += (_, _) => focusRequests++;
        Task fetch = actions.FetchAsync(RepositoryPath, "origin", prune: false);
        viewModel.IsPasswordInputVisible.Should().BeFalse();

        git.OutputAt(0)!.Report(new GitOutputLine("Username: ", IsProgress: true, IsPrompt: true));

        viewModel.ShowPasswordInput.Should().BeNull();
        viewModel.IsPasswordInputVisible.Should().BeTrue();
        focusRequests.Should().Be(1);
        preferences.ShowProcessDialogPasswordInput.Should().BeFalse();
        git.Complete(0);
        await fetch;
        viewModel.Detach();
    }

    [Test]
    public async Task Send_input_should_write_a_line_to_gits_input_and_empty_the_box()
    {
        FakeGitOperations git = new();
        RepositoryOperationsViewModel actions = new(git);
        ProcessViewModel viewModel = new(new FakeDialogHost(), actions, new InMemoryAppPreferences());
        Task fetch = actions.FetchAsync(RepositoryPath, "origin", prune: false);
        StringWriter input = new();
        IGitInputTarget target = (IGitInputTarget)git.OutputAt(0)!;
        target.Attach(input);

        viewModel.Password = "secret";
        viewModel.SendInputCommand.Execute(null);
        target.Attach(null);
        viewModel.Password = "late";
        viewModel.SendInputCommand.Execute(null);

        input.ToString().Should().Be("secret\n", "nothing is written once git has exited");
        viewModel.Password.Should().BeEmpty();
        git.Complete(0);
        await fetch;
        viewModel.SendInputCommand.CanExecute(null).Should().BeFalse();
        viewModel.Detach();
    }

    [Test]
    public void The_eye_should_show_and_hide_the_text()
    {
        ProcessViewModel viewModel = new(new FakeDialogHost(),
            new RepositoryOperationsViewModel(new FakeGitOperations()),
            new InMemoryAppPreferences());

        viewModel.RevealPasswordCommand.Execute(null);
        viewModel.IsPasswordRevealed.Should().BeTrue();
        viewModel.RevealPasswordCommand.Execute(null);
        viewModel.IsPasswordRevealed.Should().BeFalse();
    }

    private sealed class ImmediateProgress(List<GitOutputLine> lines) : IProgress<GitOutputLine>
    {
        public void Report(GitOutputLine value) => lines.Add(value);
    }

    // Hands out its texts one read at a time, as a pipe does when git stops writing between them.
    private sealed class ChunkReader(params string[] chunks) : TextReader
    {
        private readonly Queue<string> _chunks = new(chunks);

        public override int Read(char[] buffer, int index, int count)
        {
            if (!_chunks.TryDequeue(out string? chunk))
            {
                return 0;
            }

            chunk.CopyTo(0, buffer, index, chunk.Length);
            return chunk.Length;
        }
    }
}

using Avalonia.Headless.NUnit;
using GitExtensions.Xplat.Core.Operations;
using GitExtUtils;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

// Upstream's process dialog input with a real git: what is typed reaches git's standard input while git runs.
internal sealed class ProcessInputGitTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(20);

    private TestRepository _repo = null!;

    [SetUp]
    public void Setup() => _repo = new TestRepository();

    [TearDown]
    public void TearDown() => _repo.Dispose();

    [AvaloniaTest]
    public void RunAsync_gives_a_target_gits_input_while_git_runs_and_takes_it_back_after()
        => GitUI.ThreadHelper.JoinableTaskFactory.Run(async () =>
        {
            string head = _repo.Run("rev-parse", "HEAD").Trim();
            InputTarget output = new();
            TaskCompletionSource answered = new(TaskCreationOptions.RunContinuationsAsynchronously);
            output.LineReported = line =>
            {
                if (line.Text.StartsWith(head, StringComparison.Ordinal))
                {
                    answered.TrySetResult();
                }
            };
            using CancellationTokenSource stop = new();

            // cat-file answers each line of its input, and waits for more until git is stopped.
            Task run = GitOutputRunner.RunAsync(_repo.Path, new GitArgumentBuilder("cat-file") { "--batch-check" },
                output, stop.Token);
            try
            {
                await output.Attached.Task.WaitAsync(_timeout);
                await output.Input!.WriteAsync("HEAD\n");
                await output.Input.FlushAsync();

                Func<Task> waitForAnswer = () => answered.Task.WaitAsync(_timeout);
                await waitForAnswer.Should().NotThrowAsync(
                    $"git answers the line written to its input; it wrote: {string.Join(" | ", output.Texts)}");
            }
            finally
            {
                await stop.CancelAsync();
                Func<Task> waitForExit = () => run.WaitAsync(_timeout);
                await waitForExit.Should().ThrowAsync<OperationCanceledException>();
            }

            output.Input.Should().BeNull("the input is taken back once git has exited");
        });

    private sealed class InputTarget : IProgress<GitOutputLine>, IGitInputTarget
    {
        private readonly List<string> _texts = [];

        public TaskCompletionSource Attached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TextWriter? Input { get; private set; }

        public IReadOnlyList<string> Texts
        {
            get
            {
                lock (_texts)
                {
                    return [.. _texts];
                }
            }
        }

        public Action<GitOutputLine> LineReported { get; set; } = _ => { };

        public void Attach(TextWriter? input)
        {
            Input = input;
            if (input is not null)
            {
                Attached.TrySetResult();
            }
        }

        public void Report(GitOutputLine value)
        {
            lock (_texts)
            {
                _texts.Add(value.Text);
            }

            LineReported(value);
        }
    }
}

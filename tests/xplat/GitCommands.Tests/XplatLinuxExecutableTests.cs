using System.Text;
using AwesomeAssertions;
using GitCommands;
using GitCommands.Logging;
using GitExtensions.Extensibility;
using GitUI;
using Microsoft.VisualStudio.Threading;
using NUnit.Framework;

namespace GitCommandsTests;

/// <summary>
///  POSIX counterparts of <c>ExecutableTests</c>, which start <c>ping.exe</c> and <c>cmd.exe</c>. <c>sleep</c> replaces
///  <c>ping</c>, and a shell that has to wait for a child process replaces <c>cmd /c</c>.
/// </summary>
[Platform(Include = "Linux,MacOsX")]
// Mirrors: ExecutableTests
internal sealed class XplatLinuxExecutableTests
{
    private const string Shell = "sh";
    private const string Sleep = "sleep";

    [Test]
    public async Task Process_shall_be_killed_on_cancellation()
    {
        TimeSpan cancelDelay = TimeSpan.FromSeconds(1);

        await TaskScheduler.Default;

        using CancellationTokenSource cts = new();

        IExecutable executable = new Executable(Sleep);
        IProcess process = executable.Start($"{cancelDelay.TotalSeconds + 60}", cancellationToken: cts.Token);
        DateTime startedAt = DateTime.Now;

        await Task.Delay(cancelDelay);
        await cts.CancelAsync();

        // The process is killed on dispose but not before.
        using (CancellationTokenSource ctsWaitExit = new())
        {
            try
            {
                ctsWaitExit.CancelAfter(cancelDelay);
                int exitCode = await process.WaitForExitAsync(ctsWaitExit.Token);
                Assert.Fail($"should not have exited, received: {exitCode}");
            }
            catch (OperationCanceledException)
            {
            }
        }

        process.Dispose();

        TimeSpan durationWaitTimeout = DateTime.Now - startedAt;
        durationWaitTimeout.Should().BeGreaterThan(1.5 * cancelDelay).And.BeLessThan(4 * cancelDelay);

        CommandLogEntry? cmd = CommandLog.Commands.LastOrDefault();
        (cmd?.Exception?.Message).Should().Be("Process killed");
    }

    [Test]
    public async Task WaitForProcessExitAsync_shall_return_latest_after_timeout()
    {
        TimeSpan halfRuntime = TimeSpan.FromSeconds(3);

        await TaskScheduler.Default;

        IExecutable executable = new Executable(Sleep);
        using IProcess process = executable.Start($"{halfRuntime.TotalSeconds * 2}");

        // Wait for the exit, but cancel the wait while the process is still running.
        using CancellationTokenSource cts = new();
        DateTime startedAt = DateTime.Now;
        cts.CancelAfter(halfRuntime);
        try
        {
            await process.WaitForExitAsync(cts.Token); // may or may not throw on cancel
        }
        catch (OperationCanceledException)
        {
            // The wait was given up on purpose; the process keeps running and is waited for below.
        }

        TimeSpan durationWaitTimeout = DateTime.Now - startedAt;
        durationWaitTimeout.Should().BeGreaterThan(0.5 * halfRuntime).And.BeLessThan(1.5 * halfRuntime);

        await process.WaitForExitAsync(CancellationToken.None);

        TimeSpan durationExit = DateTime.Now - startedAt;
        durationExit.Should().BeGreaterThan(1.5 * halfRuntime).And.BeLessThan(2.5 * halfRuntime);
    }

    // "; true" keeps the shell from replacing itself with sleep, so killing the shell leaves a child holding its output
    // pipes open, the same shape as cmd.exe running ping upstream.
    [TestCase(Sleep, "4")]
    [TestCase(Shell, "-c \"sleep 4; true\"")]
    public async Task ExecuteAsync_shall_return_latest_after_timeout(string exeFile, string arguments)
    {
        const int cancelDelay = 1000;
        const int exitDelay = cancelDelay;

        using CancellationTokenSource cancellationTokenSource = new();
        CancellationToken cancellationToken = cancellationTokenSource.Token;
        IExecutable executable = new Executable(exeFile);

        Exception? exception = null;
        ExecutionResult? executionResult = null;

        _ = ThreadHelper.JoinableTaskFactory.RunAsync(ExecuteAsync, JoinableTaskCreationOptions.LongRunning);

        await Task.Delay(cancelDelay);
        await cancellationTokenSource.CancelAsync();
        await Task.Delay(exitDelay);

        exception.Should().NotBeNull();
        exception.GetType().Should().Be<OperationCanceledException>();
        executionResult.Should().BeNull();
        return;

        async Task ExecuteAsync()
        {
            try
            {
                await TaskScheduler.Default.SwitchTo(alwaysYield: true);
                executionResult = await executable.ExecuteAsync(arguments, outputEncoding: Encoding.Default, cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        }
    }
}

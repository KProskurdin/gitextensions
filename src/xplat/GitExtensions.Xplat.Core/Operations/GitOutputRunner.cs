using System.Diagnostics;
using System.Text;
using GitCommands;
using GitCommands.Logging;
using GitExtensions.Extensibility;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  A piece of git's output. <see cref="IsProgress"/> marks a line that git ended with a carriage return: it is redrawn in
///  place, like "Receiving objects:  45%", so the next line replaces it. <see cref="IsPrompt"/> marks text git wrote
///  without ending the line and then waited, as a question for input does; it is shown at once and the rest of the line
///  replaces it.
/// </summary>
public sealed record GitOutputLine(string Text, bool IsProgress, bool IsPrompt = false);

/// <summary>
///  An output that also takes input for git (upstream's process dialog's <c>PasswordInput</c>): while git runs, its
///  standard input is attached; null once git has exited.
/// </summary>
public interface IGitInputTarget
{
    void Attach(TextWriter? input);
}

/// <summary>
///  Runs git and reports its output while it runs, for the long remote operations (fetch, pull, push, clone). Upstream's
///  <see cref="IExecutable"/> returns standard error only after git exits, so this starts the process itself; like upstream's
///  <c>Executable</c>, it sets the environment first and records the call in <see cref="CommandLog"/>.
/// </summary>
public static class GitOutputRunner
{
    private const int BufferSize = 4096;
    private const int ErrorLinesInMessage = 8;

    /// <summary>
    ///  How <see cref="RunAsync"/> starts <paramref name="fileName"/>: output read as it comes, the extra variables set, and
    ///  a console window only with <paramref name="showWindow"/>, upstream's <c>ShowGitCommandLine</c> as its
    ///  <c>PlainTextConsoleCommandRunner</c> reads it (Windows; elsewhere the OS opens none).
    /// </summary>
    public static ProcessStartInfo StartInfo(string fileName, string args, string workingDirectory,
        IReadOnlyDictionary<string, string>? environment, bool showWindow, bool redirectInput = false)
    {
        ProcessStartInfo info = new(fileName, args)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = !showWindow,
            RedirectStandardInput = redirectInput,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach ((string name, string value) in environment ?? new Dictionary<string, string>())
        {
            info.Environment[name] = value;
        }

        return info;
    }

    /// <summary>
    ///  Runs git with <paramref name="arguments"/> in <paramref name="workingDirectory"/>. Throws
    ///  <see cref="OperationCanceledException"/> when cancelled (git is killed) and <see cref="GitOperationException"/> with
    ///  git's last messages when it exits with an error. <paramref name="environment"/> adds variables for this git process
    ///  only. <paramref name="program"/> runs another program the same way (a user script), instead of git. When
    ///  <paramref name="output"/> takes input (<see cref="IGitInputTarget"/>), git's standard input is given to it while git
    ///  runs, as upstream's <c>PlainTextConsoleCommandRunner</c> keeps it open.
    /// </summary>
    public static async Task RunAsync(string workingDirectory, ArgumentString arguments,
        IProgress<GitOutputLine> output, CancellationToken cancellationToken,
        IReadOnlyDictionary<string, string>? environment = null, string? program = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnvironmentConfiguration.SetEnvironmentVariables();

        // Upstream's executable writes quotes inside arguments as this marker (see Executable.Start).
        string args = (arguments.Arguments ?? "").Replace("$QUOTE$", "\\\"");
        string fileName = program ?? AppSettings.GitCommand;
        IGitInputTarget? inputTarget = output as IGitInputTarget;
        ProcessStartInfo info = StartInfo(fileName, args, workingDirectory, environment, AppSettings.ShowGitCommandLine,
            redirectInput: inputTarget is not null);
        ProcessOperation log = CommandLog.LogProcessStart(fileName, args, workingDirectory);
        using Process process = Process.Start(info) ?? throw new GitOperationException($"Could not start {fileName}");
        log.SetProcessId(process.Id);
        List<string> errorLines = [];
        using CancellationTokenRegistration kill = cancellationToken.Register(() => KillQuietly(process));
        inputTarget?.Attach(process.StandardInput);
        try
        {
            // The streams are read to their end even after a cancel, so the last lines are not lost.
            Task errors = Task.Run(() => Pump(process.StandardError, output, errorLines), CancellationToken.None);
            Task standard = Task.Run(() => Pump(process.StandardOutput, output, collected: null), CancellationToken.None);
            await process.WaitForExitAsync(CancellationToken.None);
            await Task.WhenAll(errors, standard);
        }
        finally
        {
            inputTarget?.Attach(null);
        }

        log.LogProcessEnd(process.ExitCode);

        cancellationToken.ThrowIfCancellationRequested();
        if (process.ExitCode != 0)
        {
            string detail = string.Join(Environment.NewLine, errorLines.TakeLast(ErrorLinesInMessage));
            throw new GitOperationException(detail.Length > 0
                ? detail
                : $"{program ?? "git"} {args} failed with exit code {process.ExitCode}")
            {
                Command = fileName, Arguments = args, WorkingDirectory = workingDirectory, ExitCode = process.ExitCode,
            };
        }
    }

    /// <summary>
    ///  Splits a stream into lines at '\n' and '\r'. A line ended by '\r' alone is a progress line. Text left without a line
    ///  end when git stops writing (a read shorter than the buffer) is reported at once as a prompt, which the rest of its
    ///  line replaces, as upstream's process dialog shows such text and offers its input box.
    /// </summary>
    public static void Pump(TextReader reader, IProgress<GitOutputLine> output, List<string>? collected)
    {
        char[] buffer = new char[BufferSize];
        StringBuilder line = new();
        bool pendingCarriageReturn = false;
        int read;
        while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (int i = 0; i < read; i++)
            {
                char c = buffer[i];
                if (pendingCarriageReturn)
                {
                    pendingCarriageReturn = false;
                    if (c == '\n')
                    {
                        // "\r\n" ends an ordinary line.
                        Emit(line, isProgress: false);
                        continue;
                    }

                    Emit(line, isProgress: true);
                }

                if (c == '\r')
                {
                    pendingCarriageReturn = true;
                }
                else if (c == '\n')
                {
                    Emit(line, isProgress: false);
                }
                else
                {
                    line.Append(c);
                }
            }

            if (read < buffer.Length && line.Length > 0 && !pendingCarriageReturn)
            {
                output.Report(new GitOutputLine(line.ToString(), IsProgress: true, IsPrompt: true));
            }
        }

        if (pendingCarriageReturn)
        {
            Emit(line, isProgress: true);
        }
        else if (line.Length > 0)
        {
            Emit(line, isProgress: false);
        }

        return;

        void Emit(StringBuilder text, bool isProgress)
        {
            string value = text.ToString();
            text.Clear();
            if (value.Length == 0)
            {
                return;
            }

            if (!isProgress)
            {
                collected?.Add(value);
            }

            output.Report(new GitOutputLine(value, isProgress));
        }
    }

    private static void KillQuietly(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // Already exited.
        }
    }
}

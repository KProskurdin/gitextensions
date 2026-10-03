using System.Diagnostics;
using AwesomeAssertions;

namespace GitExtensions.Xplat.App.Tests;

/// <summary>
///  Runs git directly for test setup and checks, independent of the code under test.
/// </summary>
internal static class GitProcess
{
    private const int _deleteAttempts = 20;
    private static readonly TimeSpan _deleteRetryDelay = TimeSpan.FromMilliseconds(50);

    public static string Run(string workingDir, params string[] arguments)
    {
        ProcessStartInfo info = new("git")
        {
            WorkingDirectory = workingDir, RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (string argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(info) ?? throw new InvalidOperationException("git did not start");
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        process.ExitCode.Should().Be(0, error);
        return output;
    }

    public static void DeleteFolder(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        // git writes read-only object files, which Windows refuses to delete.
        foreach (string file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        // A git process that just exited can still hold its working directory for a moment, so retry briefly.
        for (int attempt = 1;; attempt++)
        {
            try
            {
                Directory.Delete(path, recursive: true);
                return;
            }
            catch (IOException) when (attempt < _deleteAttempts)
            {
                Thread.Sleep(_deleteRetryDelay);
            }
        }
    }
}

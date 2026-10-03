using System.Diagnostics;

namespace GitExtensions.Xplat.App.Tests;

/// <summary>
///  A temporary repository with two commits: "first" (root) and "second" with a body, both on HEAD.
/// </summary>
internal sealed class TestRepository : IDisposable
{
    public TestRepository()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "xplat-app-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
        Run("init", "-q");
        Run("config", "user.name", "Test");
        Run("config", "user.email", "test@example.com");
        File.WriteAllText(System.IO.Path.Combine(Path, "a.txt"), "one");
        Run("add", "a.txt");
        Run("commit", "-q", "-m", "first");
        File.WriteAllText(System.IO.Path.Combine(Path, "a.txt"), "two");
        Run("commit", "-q", "-am", "second\n\nbody line");
    }

    public string Path { get; }

    public string Run(params string[] arguments)
    {
        ProcessStartInfo info = new("git")
        {
            WorkingDirectory = Path,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (string argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(info) ?? throw new InvalidOperationException("git did not start");
        string output = process.StandardOutput.ReadToEnd();
        process.StandardError.ReadToEnd();
        process.WaitForExit();
        process.ExitCode.Should().Be(0);
        return output;
    }

    public void Dispose()
    {
        if (!Directory.Exists(Path))
        {
            return;
        }

        // git writes read-only object files, which Windows refuses to delete.
        foreach (string file in Directory.EnumerateFiles(Path, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(Path, recursive: true);
    }
}

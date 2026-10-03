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

    public string Run(params string[] arguments) => GitProcess.Run(Path, arguments);

    public void Dispose() => GitProcess.DeleteFolder(Path);
}

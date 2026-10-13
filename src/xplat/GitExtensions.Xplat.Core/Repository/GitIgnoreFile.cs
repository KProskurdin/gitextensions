using GitCommands;
using GitCommands.Git;

namespace GitExtensions.Xplat.Core.Repository;

/// <summary>
///  The repository's top-level <c>.gitignore</c>, or its local excludes (<c>.git/info/exclude</c>), read and written as text,
///  as upstream's <c>FormGitIgnore</c> edits them (<c>GitIgnoreModel</c> and <c>GitLocalExcludeModel</c>).
/// </summary>
public static class GitIgnoreFile
{
    public const string FileName = ".gitignore";

    public static string PathIn(string repositoryPath) => Path.Combine(repositoryPath, FileName);

    /// <summary>
    ///  The file <paramref name="localExclude"/> chooses: the local excludes in the repository's git folder (also for a
    ///  worktree), as upstream's <c>ResolveGitInternalPath("info")</c> finds it, or the <c>.gitignore</c>.
    /// </summary>
    public static string PathIn(string repositoryPath, bool localExclude)
        => localExclude
            ? Path.Join(new GitModule(new GitExecutorProvider(new GitDirectoryResolver()), repositoryPath)
                .ResolveGitInternalPath("info"), "exclude")
            : PathIn(repositoryPath);

    /// <summary>
    ///  The file's text, or empty when the repository has none yet.
    /// </summary>
    public static Task<string> ReadAsync(string repositoryPath) => ReadAsync(repositoryPath, localExclude: false);

    public static async Task<string> ReadAsync(string repositoryPath, bool localExclude)
    {
        string path = PathIn(repositoryPath, localExclude);
        return File.Exists(path) ? await File.ReadAllTextAsync(path) : "";
    }

    public static Task WriteAsync(string repositoryPath, string content)
        => WriteAsync(repositoryPath, content, localExclude: false);

    public static Task WriteAsync(string repositoryPath, string content, bool localExclude)
    {
        string path = PathIn(repositoryPath, localExclude);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return File.WriteAllTextAsync(path, content);
    }

    /// <summary>
    ///  The text with <paramref name="patterns"/> added on lines of their own, skipping patterns it already has. The file's
    ///  own line ending is kept.
    /// </summary>
    public static string AddPatterns(string content, IEnumerable<string> patterns)
    {
        string newLine = content.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        HashSet<string> existing = new(content.Split('\n').Select(line => line.TrimEnd('\r').Trim()),
            StringComparer.Ordinal);
        List<string> added =
        [
            .. patterns.Select(pattern => pattern.Trim()).Where(pattern => pattern.Length > 0 && existing.Add(pattern))
        ];
        if (added.Count == 0)
        {
            return content;
        }

        string separator = content.Length == 0 || content.EndsWith('\n') ? "" : newLine;
        return content + separator + string.Join(newLine, added) + newLine;
    }

    /// <summary>
    ///  The pattern that ignores exactly one file: anchored at the repository root, so a file of the same name in another
    ///  folder is not ignored too.
    /// </summary>
    public static string PatternFor(string repositoryRelativePath) =>
        "/" + repositoryRelativePath.Replace('\\', '/').TrimStart('/');
}

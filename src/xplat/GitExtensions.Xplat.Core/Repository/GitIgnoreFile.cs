namespace GitExtensions.Xplat.Core.Repository;

/// <summary>
///  Where the repository's top-level <c>.gitignore</c> and its local excludes (<c>.git/info/exclude</c>) are, as upstream's
///  <c>GitIgnoreModel</c> and <c>GitLocalExcludeModel</c> find them, and the pattern for one file. The editing is in
///  <see cref="GitIgnoreDialog"/>.
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
            ? Path.Join(GitModules.Open(repositoryPath).ResolveGitInternalPath("info"), "exclude")
            : PathIn(repositoryPath);

    /// <summary>
    ///  The pattern that ignores exactly one file: anchored at the repository root, so a file of the same name in another
    ///  folder is not ignored too (upstream's <c>AddFileToIgnoreFile</c>).
    /// </summary>
    public static string PatternFor(string repositoryRelativePath) =>
        "/" + repositoryRelativePath.Replace('\\', '/').TrimStart('/');
}

using GitUI;

namespace GitExtensions.Xplat.Core.Repository;

/// <summary>
///  Upstream's "Find file": which files match what the user typed in its search window (upstream's
///  <c>FindFilePredicateProvider</c>, linked unchanged): a path below the working directory matches the files it starts,
///  other text the files whose path contains it, case ignored.
/// </summary>
public static class FindFile
{
    /// <summary>
    ///  Upstream SearchWindow's label.
    /// </summary>
    public const string EnterFileNameText = "Enter File Name";

    /// <summary>
    ///  Upstream SearchControl shows at most this many matches.
    /// </summary>
    public const int MaxCandidates = 20;

    /// <summary>
    ///  The files of <paramref name="files"/> whose path, or path before a rename, matches <paramref name="pattern"/>.
    /// </summary>
    /// <param name="files">Each file's path and its path before a rename or copy (null for none).</param>
    public static IEnumerable<string> Matches(IEnumerable<(string Path, string? OldPath)> files, string pattern,
        string workingDir)
    {
        Func<string?, bool> predicate = new FindFilePredicateProvider().Get(pattern, workingDir);
        return files.Where(file => predicate(file.Path) || predicate(file.OldPath)).Select(file => file.Path);
    }
}

using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility.Git;
using ResourceManager;

namespace GitExtensions.Xplat.Core.Diff;

/// <summary>
///  Upstream's <c>ViewChangesAsync</c> for a submodule: not git's "Subproject commit" lines, but which commits it moved between
///  and whether it is dirty, by upstream's <c>SubmoduleHelpers</c> and <c>SubmoduleResources</c>.
/// </summary>
public static class SubmoduleDiff
{
    private const string SubprojectCommit = "Subproject commit ";

    /// <summary>
    ///  Whether a diff is git's way of showing a submodule that moved or is dirty.
    /// </summary>
    public static bool IsSubmoduleDiff(IReadOnlyList<DiffLine> lines)
        => lines.Any(line => line.Kind is DiffLineKind.Added or DiffLineKind.Removed
                             && line.Text.AsSpan(1).StartsWith(SubprojectCommit, StringComparison.Ordinal));

    /// <summary>
    ///  The submodule's change between two revisions.
    /// </summary>
    public static async Task<IReadOnlyList<DiffLine>> LinesAsync(GitModule module, string path, string? oldPath,
        ObjectId first,
        ObjectId second, CancellationToken cancellationToken = default)
        => Lines(module, path,
            await SubmoduleHelpers.GetSubmoduleDiffChangesAsync(module, path, oldPath, first, second,
                cancellationToken));

    /// <summary>
    ///  The submodule's change in the working directory or the index.
    /// </summary>
    public static async Task<IReadOnlyList<DiffLine>> CurrentLinesAsync(GitModule module, string path, bool staged)
        => Lines(module, path,
            await SubmoduleHelpers.GetSubmoduleCurrentChangesAsync(module, path, oldFileName: null, staged));

    /// <summary>
    ///  The two sides of what a viewer shows for a commit: its first parent and the commit; null without a parent.
    /// </summary>
    public static (ObjectId First, ObjectId Second)? CommitRevisions(GitModule module, string commitHash)
    {
        ObjectId second = module.RevParse(commitHash);
        ObjectId first = module.RevParse($"{commitHash}^");
        return second.IsZero || first.IsZero ? null : (first, second);
    }

    /// <summary>
    ///  Upstream's <c>GetSubmoduleText</c>: a submodule's entry in a tree, or its folder for an empty <paramref name="hash"/>.
    /// </summary>
    public static string EntryText(GitModule module, string path, string hash)
        => SubmoduleResources.GetSubmoduleText(module, path.TrimEnd('/'), hash);

    private static IReadOnlyList<DiffLine> Lines(GitModule module, string path, GitSubmoduleStatus? status)
    {
        string text = status is null
            ? $"Failed to get status for submodule \"{path}\""
            : SubmoduleResources.GetSubmoduleStatusText(module, status);

        // No line numbers: this is no file's text.
        return [.. DiffParser.ParseText(text).Select(line => line with { NewNumber = null })];
    }
}

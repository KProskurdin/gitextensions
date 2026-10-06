using GitCommands;
using GitCommands.Git;

namespace GitExtensions.Xplat.Core.Settings;

/// <summary>
///  The diff and merge tools that can run on this computer, for the settings editor to offer.
/// </summary>
public interface IDiffMergeToolCatalog
{
    /// <summary>
    ///  The tool names git can start here (<paramref name="diff"/> for diff tools, otherwise merge tools), the configured
    ///  one first. Empty when git cannot tell.
    /// </summary>
    Task<IReadOnlyList<string>> GetAvailableAsync(bool diff, CancellationToken cancellationToken = default);
}

/// <summary>
///  Asks git through upstream's <see cref="CustomDiffMergeToolCache"/>, used as is: it runs <c>git difftool --tool-help</c>
///  (or mergetool), keeps the tools listed as available, including user-defined ones, and leaves out the ones that need a
///  terminal. git checks each tool's program on this OS, so the list is per OS without a list of paths in the fork
///  (PLAN.md M5: "detect the ones that exist on the current OS").
/// </summary>
public sealed class GitDiffMergeToolCatalog : IDiffMergeToolCatalog
{
    public async Task<IReadOnlyList<string>> GetAvailableAsync(bool diff, CancellationToken cancellationToken = default)
    {
        // --tool-help needs no repository; the user's folder gives git a working directory that always exists.
        GitModule module = new(new GitExecutorProvider(new GitDirectoryResolver()),
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        CustomDiffMergeToolCache cache = diff ? CustomDiffMergeToolCache.DiffToolCache : CustomDiffMergeToolCache.MergeToolCache;
        return [.. await cache.GetToolsAsync(module, delay: 0, cancellationToken)];
    }
}

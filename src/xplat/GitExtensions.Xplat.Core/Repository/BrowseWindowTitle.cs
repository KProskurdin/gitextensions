using GitCommands;
using GitCommands.Git;
using GitCommands.UserRepositoryHistory;

namespace GitExtensions.Xplat.Core.Repository;

/// <summary>
///  The browse window's title as upstream's <c>FormBrowse</c> sets it with <see cref="AppTitleGenerator"/>: the path
///  filter, the repository's description and the current branch, e.g. "gitextensions (master) - Git Extensions".
/// </summary>
public static class BrowseWindowTitle
{
    private static readonly AppTitleGenerator _generator =
        new(new RepositoryDescriptionProvider(new GitDirectoryResolver()));

    /// <summary>
    ///  The title for <paramref name="repositoryPath"/> (null when none is open) on <paramref name="branch"/>;
    ///  <paramref name="noBranch"/> is shown while the branch is not known yet.
    /// </summary>
    public static string Of(string? repositoryPath, string? branch, string noBranch, string? pathFilter = null)
        => _generator.Generate(repositoryPath, isValidWorkingDir: repositoryPath is not null, branch, noBranch, pathFilter);
}

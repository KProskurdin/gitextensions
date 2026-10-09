using GitCommands;
using GitCommands.Git;

namespace GitExtensions.Xplat.Core.Repository;

/// <summary>
///  Opens upstream's <see cref="GitModule"/> for a working directory, with upstream's git executor, which is internal to
///  GitCommands.
/// </summary>
public static class GitModules
{
    public static GitModule Open(string workingDir) => new(new GitExecutorProvider(new GitDirectoryResolver()), workingDir);
}

using GitCommands.Git;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  What a push sends and where, as upstream's <c>FormPush</c> asks for it. <see cref="RemoteBranch"/> empty pushes to the
///  branch of the same name.
/// </summary>
public sealed record PushRequest(string Remote, string LocalBranch, string RemoteBranch, ForcePushOptions Force, bool Track);

public enum PullAction
{
    Merge,
    Rebase,
    FetchOnly
}

/// <summary>
///  What a pull or fetch gets, as upstream's <c>FormPull</c> asks for it. <see cref="RemoteBranch"/> empty fetches every
///  branch of the remote (a pull then uses the branch's configured upstream). <see cref="Prune"/> applies to a fetch;
///  <see cref="AutoStash"/> to a merge or rebase.
/// </summary>
public sealed record PullRequest(string Remote, string RemoteBranch, PullAction Action, bool Prune, bool AutoStash);

using GitCommands;
using GitCommands.Git;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  What upstream's push dialog pushes: a branch, every branch, a tag, every tag, or the branches of its "Push multiple
///  branches" tab.
/// </summary>
public enum PushKind
{
    Branch,
    AllBranches,
    Tag,
    AllTags,
    Multiple,
}

/// <summary>
///  What a push sends and where, as upstream's <c>FormPush</c> asks for it. <see cref="Remote"/> is a remote's name or a
///  URL. <see cref="RemoteBranch"/> empty pushes to the branch of the same name. <see cref="Tag"/> is the tag of a
///  <see cref="PushKind.Tag"/> push; <see cref="Actions"/> the pushes and deletions of a <see cref="PushKind.Multiple"/>
///  push; <see cref="RecursiveSubmodules"/> upstream's choice (0 none, 1 check, 2 on demand) for a branch push.
/// </summary>
public sealed record PushRequest(
    string Remote,
    string LocalBranch,
    string RemoteBranch,
    ForcePushOptions Force,
    bool Track,
    PushKind Kind = PushKind.Branch,
    string Tag = "",
    IReadOnlyList<GitPushAction>? Actions = null,
    int RecursiveSubmodules = 0);

public enum PullAction
{
    Merge,
    Rebase,
    FetchOnly
}

/// <summary>
///  What a pull or fetch gets, as upstream's <c>FormPull</c> asks for it. <see cref="RemoteBranch"/> empty fetches every
///  branch of the remote (a pull then uses the branch's configured upstream). <see cref="Prune"/> applies to a fetch;
///  <see cref="AutoStash"/> to a merge or rebase. <see cref="LocalBranch"/> is the branch a fetch updates; <see cref="FetchTags"/>
///  is upstream's tag option (true every tag, false none, null the remote's <c>tagopt</c>); <see cref="Unshallow"/> downloads
///  the full history; <see cref="PruneTags"/> prunes tags too (a fetch).
/// </summary>
public sealed record PullRequest(
    string Remote,
    string RemoteBranch,
    PullAction Action,
    bool Prune,
    bool AutoStash,
    string LocalBranch = "",
    bool? FetchTags = false,
    bool Unshallow = false,
    bool PruneTags = false);

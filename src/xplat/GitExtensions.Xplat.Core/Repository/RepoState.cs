namespace GitExtensions.Xplat.Core.Repository;

/// <summary>
///  The state upstream's <c>RepoStateVisualiser</c> shows on the Commit button, by upstream's image names.
/// </summary>
public enum RepoState
{
    RepoStateClean,
    RepoStateDirty,
    RepoStateUntrackedOnly,
    RepoStateStaged,
    RepoStateMixed,
}

public static class RepoStates
{
    /// <summary>
    ///  Upstream's rule: nothing changed is clean; only work tree changes are dirty, or untracked only when all of them are
    ///  untracked files; only staged changes are staged; both are mixed. (Upstream's dirty-submodules state needs the
    ///  submodule status of each change, which the snapshot does not have; such a change counts as dirty.)
    /// </summary>
    public static RepoState Of(IReadOnlyList<FileChange> changes)
    {
        int index = changes.Count(change => change.Staged);
        int workTree = changes.Count - index;
        int untracked = changes.Count(change => change.Kind == ChangeKind.Untracked);
        return (index, workTree) switch
        {
            (0, 0) => RepoState.RepoStateClean,
            (0, _) when workTree == untracked => RepoState.RepoStateUntrackedOnly,
            (0, _) => RepoState.RepoStateDirty,
            (_, 0) => RepoState.RepoStateStaged,
            _ => RepoState.RepoStateMixed,
        };
    }
}

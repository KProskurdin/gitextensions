namespace GitExtensions.Xplat.Core.CommitHistory;

/// <summary>
///  Upstream's stash rows in the revision grid (<c>RevisionGridControl</c>'s <c>OnRevisionRead</c> with
///  <c>ShowStashes</c>): a stash that the log already lists gets its selector; any other stash goes just before the commit
///  it was made on (its first parent), followed by its "untracked files" commit when that has changes; a stash whose
///  parent is not listed is not shown.
/// </summary>
public static class StashRows
{
    /// <param name="rows">The log's rows, in the grid's order.</param>
    /// <param name="stashes">The stashes as <c>git stash list</c> gives them, each with its reflog selector.</param>
    /// <param name="untrackedByStash">A stash's "untracked files" commit, by the stash's hash, for those that have one.</param>
    public static IReadOnlyList<CommitRow> Insert(IReadOnlyList<CommitRow> rows, IReadOnlyList<CommitRow> stashes,
        IReadOnlyDictionary<string, CommitRow> untrackedByStash)
    {
        if (stashes.Count == 0)
        {
            return rows;
        }

        Dictionary<string, CommitRow> byId = stashes.DistinctBy(stash => stash.Hash).ToDictionary(stash => stash.Hash);
        ILookup<string, CommitRow> byParent = stashes
            .Where(stash => stash.ParentHashes is [_, ..])
            .ToLookup(stash => stash.ParentHashes![0]);
        List<CommitRow> result = new(rows.Count + stashes.Count);
        foreach (CommitRow row in rows)
        {
            if (byId.Remove(row.Hash, out CommitRow? listed))
            {
                result.Add(row with { ReflogSelector = listed.ReflogSelector });
                continue;
            }

            foreach (CommitRow stash in byParent[row.Hash])
            {
                if (!byId.Remove(stash.Hash))
                {
                    continue;
                }

                // Upstream drops the parents it does not show: the "index" commit, and an empty "untracked" one.
                CommitRow? untracked = untrackedByStash.GetValueOrDefault(stash.Hash);
                result.Add(stash with { ParentHashes = untracked is null ? [row.Hash] : [row.Hash, untracked.Hash] });
                if (untracked is not null)
                {
                    result.Add(untracked);
                }
            }

            result.Add(row);
        }

        return result;
    }

    /// <summary>
    ///  Upstream's choice of the "untracked files" commits to read: the third parent of the stashes that have one, at most
    ///  <paramref name="limit"/> (upstream's <c>MaxStashesWithUntrackedFiles</c>), by the stash's hash.
    /// </summary>
    public static IReadOnlyDictionary<string, string> UntrackedCommits(IReadOnlyList<CommitRow> stashes, int limit)
        => stashes
            .Where(stash => stash.ParentHashes is { Count: >= 3 })
            .Take(limit)
            .DistinctBy(stash => stash.Hash)
            .ToDictionary(stash => stash.Hash, stash => stash.ParentHashes![2]);
}

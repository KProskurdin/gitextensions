using GitCommands;
using GitExtensions.Extensibility.Git;
using GitUIPluginInterfaces;

namespace GitExtensions.Xplat.Core.CommitHistory;

/// <summary>
///  A grid's rows as upstream's <c>FileStatusDiffCalculator</c> takes them: as <see cref="GitRevision"/>s, and described as
///  upstream's <c>DescribeRevision</c> names a group's revisions.
/// </summary>
public static class GridRevisions
{
    /// <summary>
    ///  Upstream's <c>GitRevision</c> of a grid row: its id and its parents.
    /// </summary>
    public static GitRevision Revision(CommitRow row)
        => new(ObjectId.Parse(row.Hash))
        {
            ParentIds =
            [
                .. (row.ParentHashes ?? []).Select(hash => ObjectId.TryParse(hash, out ObjectId id) ? id : default)
                .Where(id => !id.IsZero)
            ],
        };

    /// <summary>
    ///  Upstream's <c>DescribeRevision</c> over <paramref name="rows"/> as they are now, for a calculation off the UI
    ///  thread: the short hash with the first branch or tag, else the subject; the work tree has upstream's name.
    /// </summary>
    public static Func<ObjectId, string> Describer(IEnumerable<CommitRow> rows)
    {
        Dictionary<string, CommitRow> byHash = [];
        foreach (CommitRow row in rows)
        {
            byHash.TryAdd(row.Hash, row);
        }

        return id => Describe(id, byHash);
    }

    private static string Describe(ObjectId id, IReadOnlyDictionary<string, CommitRow> rows)
    {
        if (id.IsZero)
        {
            return ResourceManager.TranslatedStrings.Workspace;
        }

        if (!rows.TryGetValue(id.ToString(), out CommitRow? row))
        {
            return id.ToShortString();
        }

        string description = id.IsArtificial ? "" : id.ToShortString() + ": ";
        RefLabel? label =
            (row.Labels ?? []).FirstOrDefault(label => label.Kind is RefKind.Branch or RefKind.RemoteBranch)
            ?? (row.Labels ?? []).FirstOrDefault(label => label.Kind == RefKind.Tag);
        return description + (label?.Name ?? row.Subject);
    }
}

namespace GitExtensions.Xplat.Core.CommitHistory;

/// <summary>
///  Upstream's ref label outlines (<c>RefLabelShape</c>): a rounded rectangle, or a side that points out or is notched in
///  so that two labels fit against each other.
/// </summary>
public enum GridLabelShape
{
    Rect,
    PointLeft,
    PointRight,
    NotchLeft,
    NotchRight,
}

/// <summary>
///  A ref label as upstream's message column draws it (<c>MessageColumnProvider</c>, <c>RevisionGridRefRenderer</c>).
/// </summary>
/// <param name="Text">The text in the label.</param>
/// <param name="Kind">The kind of ref, which picks the color (and for a bisect mark the image instead of a label).</param>
/// <param name="Shape">The outline.</param>
/// <param name="IsCurrent">The checked-out branch: bold, with upstream's filled arrow before the text.</param>
public sealed record GridLabel(string Text, RefKind Kind, GridLabelShape Shape = GridLabelShape.Rect, bool IsCurrent = false)
{
    /// <summary>
    ///  The label drawn against this one: the remote branch it tracks on the same commit, or upstream's virtual label with
    ///  the ahead and behind arrows of the tracked or tracking branch.
    /// </summary>
    public GridLabel? Nestled { get; init; }

    /// <summary>
    ///  The ref the label stands for; null for a virtual label.
    /// </summary>
    public RefLabel? Ref { get; init; }

    /// <summary>
    ///  The remote whose color (upstream's <c>RemoteColors</c>, a remote's color in its settings) the label takes instead of
    ///  its kind's: a remote branch's remote, or for the label nestled against a local branch the remote it tracks.
    /// </summary>
    public string? Remote { get; init; }
}

/// <summary>
///  Upstream's revision grid choices of the View menu: the "Grid labels" (<c>ShowTags</c>, <c>ShowRemoteBranches</c>)
///  and the commit body after the subject (<c>ShowCommitBodyInRevisionGrid</c>).
/// </summary>
public sealed record GridViewOptions(bool ShowTags = true, bool ShowRemoteBranches = true, bool ShowCommitBody = true);

/// <summary>
///  Lays out a commit's labels as upstream's <c>MessageColumnProvider.OnCellPainting</c> draws them: HEAD is not a label
///  (the checked-out branch is), the labels are sorted by kind and name, a local branch and the remote branch it tracks on
///  the same commit are drawn as one pair, a branch tracking or tracked elsewhere gets the ahead and behind arrows as a
///  virtual label, a tag points left, and next to a single local branch a remote branch of the same name shows only its
///  remote.
/// </summary>
public static class GridLabels
{
    public static IReadOnlyList<GridLabel> Layout(IReadOnlyList<RefLabel> labels, GridViewOptions options)
    {
        List<RefLabel> refs = [.. labels.Where(label => label.Kind != RefKind.Head && Shows(label, options))];
        refs.Sort(Compare);

        Dictionary<string, RefLabel> trackedRemotes = new(StringComparer.Ordinal);
        foreach (RefLabel local in refs.Where(label => label.Kind == RefKind.Branch && label.Upstream is not null))
        {
            if (refs.FirstOrDefault(label => label.Kind == RefKind.RemoteBranch && label.Name == local.Upstream) is { } remote)
            {
                trackedRemotes[local.Name] = remote;
            }
        }

        string? singleLocalBranch = refs.Count(label => label.Kind == RefKind.Branch) == 1
            ? trackedRemotes.Keys.FirstOrDefault()
            : null;

        List<GridLabel> layout = [];
        foreach (RefLabel label in refs)
        {
            if (trackedRemotes.ContainsValue(label))
            {
                continue;
            }

            if (label.IsBisect)
            {
                layout.Add(new GridLabel("", label.Kind) { Ref = label });
                continue;
            }

            if (label.Kind == RefKind.Branch && trackedRemotes.TryGetValue(label.Name, out RefLabel? tracked))
            {
                layout.Add(new GridLabel(label.Name, label.Kind, GridLabelShape.PointRight, label.IsCurrent)
                {
                    Ref = label,
                    Nestled = new GridLabel(tracked.Remote ?? tracked.Name, RefKind.RemoteBranch, GridLabelShape.NotchLeft)
                    {
                        Ref = tracked,
                        Remote = tracked.Remote,
                    },
                });
                continue;
            }

            if (label.AheadBehind is { Length: > 0 } arrows && label.Kind is RefKind.Branch or RefKind.RemoteBranch)
            {
                bool remote = label.Kind == RefKind.RemoteBranch;
                layout.Add(new GridLabel(label.Name, label.Kind, remote ? GridLabelShape.NotchRight : GridLabelShape.PointRight,
                    label.IsCurrent)
                {
                    Ref = label,
                    Remote = label.Remote,
                    Nestled = new GridLabel(arrows, remote ? RefKind.Branch : RefKind.RemoteBranch,
                        remote ? GridLabelShape.PointLeft : GridLabelShape.NotchLeft)
                    {
                        Remote = remote ? null : label.TrackingRemote,
                    },
                });
                continue;
            }

            string text = singleLocalBranch is not null && label.Kind == RefKind.RemoteBranch && label.Remote is { } name
                          && label.Name == $"{name}/{singleLocalBranch}"
                ? name
                : label.Name;
            layout.Add(new GridLabel(text, label.Kind, label.Kind == RefKind.Tag ? GridLabelShape.PointLeft : GridLabelShape.Rect,
                label.IsCurrent)
            {
                Ref = label,
                Remote = label.Remote,
            });
        }

        return layout;
    }

    private static bool Shows(RefLabel label, GridViewOptions options) => label.Kind switch
    {
        RefKind.Tag => options.ShowTags,
        RefKind.RemoteBranch => options.ShowRemoteBranches,
        _ => true,
    };

    // Upstream's SortRefs: bisect marks, the checked-out branch, local branches, remote branches, then the rest; by name.
    private static int Compare(RefLabel left, RefLabel right)
    {
        int rank = Rank(left).CompareTo(Rank(right));
        return rank != 0 ? rank : string.CompareOrdinal(left.Name, right.Name);

        static int Rank(RefLabel label)
            => label.IsBisect ? 0
                : label.IsCurrent ? 1
                : label.Kind == RefKind.Branch ? 3
                : label.Kind == RefKind.RemoteBranch ? 4
                : 5;
    }
}

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

    /// <summary>
    ///  Upstream's <c>RefLabelIcon.HeadMergeSource</c>: the remote branch the checked-out branch tracks, drawn after an
    ///  outlined arrow; never for a label nestled against another.
    /// </summary>
    public bool IsMergeSource { get; init; }

    /// <summary>
    ///  Upstream's dashed outline for a submodule's grid: the superproject has this ref too, or the label is only the
    ///  superproject's (<see cref="SuperprojectLabels"/>).
    /// </summary>
    public bool InSuperproject { get; init; }

    /// <summary>
    ///  One of upstream's <c>DrawSuperprojectInfo</c> labels, in its orange red: the commit the superproject records, or a
    ///  side of a conflict on the submodule.
    /// </summary>
    public bool IsSuperprojectCommit { get; init; }
}

/// <summary>
///  Upstream's revision grid choices of the View menu: the "Grid labels" (<c>ShowTags</c>, <c>ShowRemoteBranches</c>),
///  the commit body after the subject (<c>ShowCommitBodyInRevisionGrid</c>), and the git notes (<c>ShowGitNotes</c>: the
///  notes after the body; <c>ShowGitNotesColumn</c>: a column of their own instead), as upstream's message column settings
///  hold them.
/// </summary>
public sealed record GridViewOptions(bool ShowTags = true, bool ShowRemoteBranches = true, bool ShowCommitBody = true,
    bool ShowGitNotes = false, bool NotesInSeparateColumn = false)
{
    /// <summary>
    ///  Upstream's <c>RevisionReader.GetLog</c> argument: the log reads the notes while either choice shows them.
    /// </summary>
    public bool ReadsNotes => ShowGitNotes || NotesInSeparateColumn;
}

/// <summary>
///  Lays out a commit's labels as upstream's <c>MessageColumnProvider.OnCellPainting</c> draws them: HEAD is not a label
///  (the checked-out branch is), the labels are sorted by kind and name, a local branch and the remote branch it tracks on
///  the same commit are drawn as one pair, a branch tracking or tracked elsewhere gets the ahead and behind arrows as a
///  virtual label, a tag points left, and next to a single local branch a remote branch of the same name shows only its
///  remote.
/// </summary>
public static class GridLabels
{
    /// <summary>
    ///  The labels of a commit with <paramref name="labels"/>. In a submodule, a label whose ref the superproject has too
    ///  (in <paramref name="superprojectRefs"/>) is dashed, and the superproject's other refs on the commit follow as dashed
    ///  labels of their own (<see cref="SuperprojectLabels.Remaining"/>).
    /// </summary>
    public static IReadOnlyList<GridLabel> Layout(IReadOnlyList<RefLabel> labels, GridViewOptions options,
        IReadOnlyList<RefLabel>? superprojectRefs = null)
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

        // As upstream, a remote branch nestled against its local branch is not matched, so the superproject's ref of that
        // name is drawn again after the labels.
        List<RefLabel> superproject = [.. superprojectRefs ?? []];
        List<GridLabel> layout = [];
        foreach (RefLabel label in refs)
        {
            if (trackedRemotes.ContainsValue(label))
            {
                continue;
            }

            bool inSuperproject = superproject.FirstOrDefault(other => other.Kind == label.Kind && other.Name == label.Name)
                is { } match && superproject.Remove(match);

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
                    InSuperproject = inSuperproject,
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
                    IsMergeSource = label.IsMergeSource,
                    InSuperproject = inSuperproject,
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
                IsMergeSource = label.IsMergeSource,
                InSuperproject = inSuperproject,
            });
        }

        layout.AddRange(SuperprojectLabels.Remaining(superproject));
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

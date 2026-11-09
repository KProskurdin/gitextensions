using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtUtils;
using GitUIPluginInterfaces;

namespace GitExtensions.Xplat.Core.CommitHistory;

/// <summary>
///  Upstream's <c>SuperProjectInfo</c> of a submodule's revision grid (<c>RevisionGridControl.GetSuperprojectCheckoutAsync</c>):
///  the commit the superproject records for the submodule, or the three commits of a conflict on it, and the
///  superproject's refs by the submodule commit each of them records. The grid draws them as dashed labels.
/// </summary>
public sealed record SuperprojectLabels
{
    /// <summary>
    ///  Upstream's <c>MessageColumnProvider.MaxSuperprojectRefs</c>: a row shows at most this many of the superproject's
    ///  refs, the last one as "…" for the rest.
    /// </summary>
    public const int MaxRefs = 4;

    // Upstream's GetSubmoduleItemsForEachRefAsync reads the superproject's most recently committed refs only.
    private const int MaxRefsRead = 100;

    private const string Ellipsis = "…";

    /// <summary>
    ///  The submodule commit the superproject's index records; null during a conflict on the submodule.
    /// </summary>
    public string? CurrentCommit { get; init; }

    /// <summary>
    ///  During a conflict on the submodule in the superproject: the merge base's commit.
    /// </summary>
    public string? ConflictBase { get; init; }

    /// <summary>
    ///  During a conflict on the submodule in the superproject: our side's commit.
    /// </summary>
    public string? ConflictLocal { get; init; }

    /// <summary>
    ///  During a conflict on the submodule in the superproject: their side's commit.
    /// </summary>
    public string? ConflictRemote { get; init; }

    /// <summary>
    ///  The superproject's refs by the submodule commit each records, most recently committed first.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<RefLabel>> Refs { get; init; } =
        new Dictionary<string, IReadOnlyList<RefLabel>>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///  Upstream's View menu choices "Show superproject branches", "Show superproject remote branches" and "Show
    ///  superproject tags" as the refs to read.
    /// </summary>
    public static RefsFilter Filter(bool branches, bool remoteBranches, bool tags)
        => (branches ? RefsFilter.Heads : RefsFilter.NoFilter)
           | (remoteBranches ? RefsFilter.Remotes : RefsFilter.NoFilter)
           | (tags ? RefsFilter.Tags : RefsFilter.NoFilter);

    /// <summary>
    ///  Reads the labels of the repository at <paramref name="repositoryPath"/>; null when it is not a submodule.
    /// </summary>
    public static Task<SuperprojectLabels?> ReadAsync(string repositoryPath, RefsFilter refs)
        => Task.Run(() => ReadAsync(new GitModule(new GitExecutorProvider(new GitDirectoryResolver()), repositoryPath), refs));

    /// <summary>
    ///  Upstream's <c>GetSuperprojectCheckoutAsync</c>: null when <paramref name="module"/> is not a submodule. The refs are
    ///  read as upstream's <c>GetSubmoduleItemsForEachRefAsync</c> reads them, with <paramref name="refs"/> for its settings
    ///  (none when it is <see cref="RefsFilter.NoFilter"/>).
    /// </summary>
    public static async Task<SuperprojectLabels?> ReadAsync(IGitModule module, RefsFilter refs)
    {
        if (module.SuperprojectModule is not { } superproject)
        {
            return null;
        }

        SuperprojectLabels labels = new();
        (char code, ObjectId commit) = await module.GetSuperprojectCurrentCheckoutAsync().ConfigureAwait(false);
        if (code == 'U')
        {
            ConflictData conflict = await superproject.GetConflictAsync(module.SubmodulePath).ConfigureAwait(false);
            labels = labels with
            {
                ConflictBase = HashOf(conflict.Base.ObjectId),
                ConflictLocal = HashOf(conflict.Local.ObjectId),
                ConflictRemote = HashOf(conflict.Remote.ObjectId),
            };
        }
        else
        {
            labels = labels with { CurrentCommit = HashOf(commit) };
        }

        return labels with { Refs = await ReadRefsAsync(superproject, module.SubmodulePath.ToPosixPath(), refs).ConfigureAwait(false) };
    }

    /// <summary>
    ///  Upstream's <c>DrawSuperprojectInfo</c>: the labels drawn first on the commit <paramref name="hash"/>, an empty one
    ///  after a filled arrow on the commit the superproject records, and "Base", "Local" and "Remote" after an outlined
    ///  arrow on the commits of a conflict.
    /// </summary>
    public IReadOnlyList<GridLabel> Marks(string hash)
    {
        List<GridLabel> marks = [];
        Add(CurrentCommit, "", current: true);
        Add(ConflictBase, "Base", current: false);
        Add(ConflictLocal, "Local", current: false);
        Add(ConflictRemote, "Remote", current: false);
        return marks;

        void Add(string? commit, string text, bool current)
        {
            if (string.Equals(commit, hash, StringComparison.OrdinalIgnoreCase))
            {
                marks.Add(new GridLabel(text, RefKind.Head, IsCurrent: current)
                {
                    IsMergeSource = !current,
                    InSuperproject = true,
                    IsSuperprojectCommit = true,
                });
            }
        }
    }

    /// <summary>
    ///  The superproject's refs that record the commit <paramref name="hash"/>.
    /// </summary>
    public IReadOnlyList<RefLabel> RefsOf(string hash) => Refs.TryGetValue(hash, out IReadOnlyList<RefLabel>? refs) ? refs : [];

    /// <summary>
    ///  Upstream's <c>DrawSuperprojectRefs</c>: the superproject's refs that the commit's own labels did not show, at most
    ///  <see cref="MaxRefs"/>, the last of them as "…"; in their kind's color, without the remote's.
    /// </summary>
    internal static IEnumerable<GridLabel> Remaining(IReadOnlyList<RefLabel> refs)
        => refs.Take(MaxRefs).Select((label, i) => new GridLabel(i < MaxRefs - 1 ? label.Name : Ellipsis, label.Kind)
        {
            InSuperproject = true,
        });

    private static async Task<IReadOnlyDictionary<string, IReadOnlyList<RefLabel>>> ReadRefsAsync(IGitModule superproject,
        string submodulePath, RefsFilter filter)
    {
        Dictionary<string, IReadOnlyList<RefLabel>> byCommit = new(StringComparer.OrdinalIgnoreCase);
        if (filter == RefsFilter.NoFilter)
        {
            return byCommit;
        }

        string refList = await superproject.GitExecutable.GetOutputAsync(Commands.GetRefs(filter, noLocks: true,
            GitRefsSortBy.committerdate, GitRefsSortOrder.Descending, MaxRefsRead)).ConfigureAwait(false);
        GitTreeParser parser = new();
        Dictionary<string, List<RefLabel>> lists = new(StringComparer.OrdinalIgnoreCase);
        foreach (IGitRef gitRef in superproject.ParseRefs(refList))
        {
            if (LabelOf(gitRef) is not { } label)
            {
                continue;
            }

            // Upstream's GetSubmoduleCommitHash: the submodule's entry in the ref's tree.
            GitArgumentBuilder args = new("ls-tree") { gitRef.Name.Quote(), "--", submodulePath.QuoteNE() };
            ExecutionResult result = await superproject.GitExecutable.ExecuteAsync(args, throwOnErrorExit: false)
                .ConfigureAwait(false);
            if (parser.ParseSingle(result.StandardOutput.TrimEnd('\n', '\r')) is not { } item || item.ObjectId.IsZero)
            {
                continue;
            }

            string commit = item.ObjectId.ToString();
            if (!lists.TryGetValue(commit, out List<RefLabel>? list))
            {
                lists[commit] = list = [];
            }

            list.Add(label);
        }

        foreach ((string commit, List<RefLabel> list) in lists)
        {
            byCommit[commit] = list;
        }

        return byCommit;
    }

    // The kinds upstream's GetHeadColor gives a color of their own; the filter reads no other.
    private static RefLabel? LabelOf(IGitRef gitRef)
        => gitRef.IsHead ? new RefLabel(gitRef.Name, RefKind.Branch)
            : gitRef.IsRemote ? new RefLabel(gitRef.Name, RefKind.RemoteBranch)
            : gitRef.IsTag ? new RefLabel(gitRef.Name, RefKind.Tag)
            : null;

    private static string? HashOf(ObjectId id) => id.IsZero ? null : id.ToString();
}

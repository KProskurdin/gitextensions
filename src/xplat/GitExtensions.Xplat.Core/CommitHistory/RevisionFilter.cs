using System.Globalization;

namespace GitExtensions.Xplat.Core.CommitHistory;

/// <summary>
///  Which commits the revision grid shows: the new shell's version of upstream's <c>FilterInfo</c> (the filter toolbar and
///  <c>FormRevisionFilter</c>). The default shows every branch, as upstream does.
/// </summary>
/// <param name="ShowFilteredBranches">Upstream's "Filtered branches" choice: the grid shows the branch filter's branches
/// instead of all of them, unless only the current branch is shown.</param>
/// <param name="BranchFilter">Upstream's "Branches:" box: revisions, branch patterns or git log options, separated by
/// spaces.</param>
/// <param name="ShowReflog">Upstream's "Show reflog" button: the commits only the reflog still reaches are shown too.</param>
/// <param name="FullHistory">Upstream's <c>FullHistoryInFileHistory</c>: a filtered history is read with
/// <c>--full-history</c>.</param>
/// <param name="SimplifyMerges">Upstream's <c>SimplifyMergesInFileHistory</c>: with the full history, also
/// <c>--simplify-merges</c>.</param>
public sealed record RevisionFilter(
    bool CurrentBranchOnly = false,
    string Author = "",
    string Committer = "",
    string Message = "",
    DateTime? Since = null,
    DateTime? Until = null,
    string PathFilter = "",
    bool NoMerges = false,
    bool FirstParent = false,
    string DiffContent = "",
    bool ShowFilteredBranches = false,
    string BranchFilter = "",
    bool ShowReflog = false,
    bool FullHistory = false,
    bool SimplifyMerges = false)
{
    // Upstream's Delimiters.WildcardBranchSearchValues: a branch filter word with these is a pattern for --branches=.
    private static readonly char[] _wildcards = ['?', '*', '['];

    public static RevisionFilter AllBranches { get; } = new();

    /// <summary>
    ///  True when a branch filter word is a pattern git matches against branch names (<c>--branches=</c>), not a revision
    ///  or an option.
    /// </summary>
    public static bool IsBranchPattern(string word)
        => word.IndexOfAny(_wildcards) >= 0 && !word.StartsWith("--", StringComparison.Ordinal)
                                            && !word.Contains("..", StringComparison.Ordinal);

    /// <summary>
    ///  True when the filter hides commits beyond the branch choice, so the view can say the list is filtered.
    /// </summary>
    public bool IsNarrowed => Author.Length > 0 || Committer.Length > 0 || Message.Length > 0 || DiffContent.Length > 0
                              || Since is not null || Until is not null || PathFilter.Length > 0 || NoMerges ||
                              FirstParent;

    /// <summary>
    ///  Upstream's <c>IsShowFilteredBranchesChecked</c> with a filter to apply: the grid shows the branch filter's branches.
    /// </summary>
    public bool ShowsFilteredBranches => ShowFilteredBranches && !CurrentBranchOnly && BranchFilter.Trim().Length > 0;

    /// <summary>
    ///  Upstream's <c>HasFilter</c>: anything that hides commits, including the branch filter but not the current branch
    ///  or reflog choices. The toolbar shows it with its funnel image and enables "Reset revision filters".
    /// </summary>
    public bool HasFilter => IsNarrowed || ShowsFilteredBranches;

    /// <summary>
    ///  Upstream's <c>ResetAllFilters</c>: every filter off; the current branch and reflog choices stay.
    /// </summary>
    public RevisionFilter ResetAll() => new(CurrentBranchOnly: CurrentBranchOnly, ShowReflog: ShowReflog);

    /// <summary>
    ///  The revision arguments for <c>git log</c>, in upstream's order: commit filters, limiting filters, branch filters.
    /// </summary>
    public string ToRevisionArguments()
    {
        List<string> arguments = [];
        if (Since is { } since)
        {
            arguments.Add($"--since=\"{since.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}\"");
        }

        if (Until is { } until)
        {
            arguments.Add($"--until=\"{until.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}\"");
        }

        if (NoMerges)
        {
            arguments.Add("--no-merges");
        }

        if (Author.Length > 0)
        {
            arguments.Add($"--author=\"{Clean(Author)}\"");
        }

        if (Committer.Length > 0)
        {
            arguments.Add($"--committer=\"{Clean(Committer)}\"");
        }

        if (Author.Length > 0 || Committer.Length > 0 || Message.Length > 0 || DiffContent.Length > 0)
        {
            arguments.Add("--regexp-ignore-case");
        }

        if (DiffContent.Length > 0)
        {
            arguments.Add($"-G\"{Clean(DiffContent)}\"");
        }

        if (Message.Length > 0)
        {
            // As upstream: a message filter that starts with "--" is a set of git log options (the box's presets use it).
            arguments.Add(IsGitLogOptions(Message) ? Message.Trim() : $"--grep=\"{Clean(Message)}\"");
        }

        if (FirstParent)
        {
            arguments.Add("--first-parent");
        }

        // Upstream's FilterInfo: the full history (and merge simplification) apply to a history its revision filter narrows
        // (author, committer, message, diff content, path or no merges).
        if (FullHistory && (Author.Length > 0 || Committer.Length > 0 || Message.Length > 0 || DiffContent.Length > 0
                            || PathFilter.Length > 0 || NoMerges))
        {
            arguments.Add("--full-history");
            if (SimplifyMerges)
            {
                arguments.Add("--simplify-merges");
            }
        }

        if (ShowReflog)
        {
            arguments.Add("--reflog");
        }

        if (CurrentBranchOnly)
        {
            arguments.Add("HEAD");
        }
        else if (ShowsFilteredBranches)
        {
            // Upstream's GetBranchRevisionFilter: a word with wildcards is a --branches= pattern (a plain name would get
            // an implicit /* from git); options and ranges are passed as they are.
            foreach (string word in BranchFilter.Split((char[]?)null,
                         StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                arguments.Add(IsBranchPattern(word) ? $"--branches={word}" : word);
            }
        }
        else
        {
            // As upstream with its default settings: every ref except notes, the stash and the refs coding agents keep
            // (upstream c16194306; literal prefixes, as GitRefName.RefsAgentsPrefix is newer than the fork base).
            arguments.Add("--exclude=refs/notes/");
            arguments.Add("--exclude=refs/stash");
            arguments.Add("--exclude=refs/agents/**");
            arguments.Add("--exclude=refs/sessions/**");
            arguments.Add("--exclude=refs/copilot/checkpoints/**");
            arguments.Add("--all");
        }

        return string.Join(' ', arguments);
    }

    /// <summary>
    ///  Upstream's <c>FilterInfo.GetSummary</c>, the advanced filter button's tooltip: one line per filter, with upstream's
    ///  labels passed through <paramref name="translate"/>.
    /// </summary>
    public string Summary(Func<string, string> translate)
    {
        List<string> lines = [];
        if (Since is { } since)
        {
            lines.Add($"{translate("Since")}: {since.ToString(CultureInfo.CurrentCulture)}");
        }

        if (Until is { } until)
        {
            lines.Add($"{translate("Until")}: {until.ToString(CultureInfo.CurrentCulture)}");
        }

        Add("Path filter", PathFilter);
        Add("Author", Author);
        Add("Committer", Committer);
        if (IsGitLogOptions(Message))
        {
            lines.Add(Message.Trim());
        }
        else
        {
            Add("Message", Message);
        }

        Add("Diff contains", DiffContent);
        if (FirstParent)
        {
            lines.Add(translate("Show only first parent"));
        }

        if (ShowReflog)
        {
            lines.Add(translate("Show reflog"));
        }

        if (CurrentBranchOnly)
        {
            lines.Add(translate("Show current branch only"));
        }
        else if (ShowsFilteredBranches)
        {
            Add("Branches", BranchFilter);
        }

        return string.Join(Environment.NewLine, lines);

        void Add(string label, string value)
        {
            if (value.Trim().Length > 0)
            {
                lines.Add($"{translate(label)}: {value.Trim()}");
            }
        }
    }

    private static bool IsGitLogOptions(string value) => value.TrimStart().StartsWith("--", StringComparison.Ordinal);

    // The values go between double quotes on the command line; a quote in them would end the argument early.
    private static string Clean(string value) => value.Trim().Replace("\"", "", StringComparison.Ordinal);
}

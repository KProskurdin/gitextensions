using System.Globalization;

namespace GitExtensions.Xplat.Core.CommitHistory;

/// <summary>
///  Which commits the revision grid shows: the new shell's version of upstream's <c>FilterInfo</c> (the filter toolbar and
///  <c>FormRevisionFilter</c>). The default shows every branch, as upstream does.
/// </summary>
public sealed record RevisionFilter(
    bool CurrentBranchOnly = false,
    string Author = "",
    string Committer = "",
    string Message = "",
    DateTime? Since = null,
    DateTime? Until = null,
    string PathFilter = "",
    bool NoMerges = false,
    bool FirstParent = false)
{
    public static RevisionFilter AllBranches { get; } = new();

    /// <summary>
    ///  True when the filter hides commits beyond the branch choice, so the view can say the list is filtered.
    /// </summary>
    public bool IsNarrowed => Author.Length > 0 || Committer.Length > 0 || Message.Length > 0 || Since is not null || Until is not null
                              || PathFilter.Length > 0 || NoMerges || FirstParent;

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

        if (Author.Length > 0 || Committer.Length > 0 || Message.Length > 0)
        {
            arguments.Add("--regexp-ignore-case");
        }

        if (Message.Length > 0)
        {
            arguments.Add($"--grep=\"{Clean(Message)}\"");
        }

        if (FirstParent)
        {
            arguments.Add("--first-parent");
        }

        if (CurrentBranchOnly)
        {
            arguments.Add("HEAD");
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

    // The values go between double quotes on the command line; a quote in them would end the argument early.
    private static string Clean(string value) => value.Trim().Replace("\"", "", StringComparison.Ordinal);
}

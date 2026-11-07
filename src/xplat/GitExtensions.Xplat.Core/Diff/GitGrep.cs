using System.Text;
using System.Text.RegularExpressions;
using GitCommands;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtUtils;

namespace GitExtensions.Xplat.Core.Diff;

/// <summary>
///  The kinds of line of upstream's grep view (<c>GrepHighlightService</c>).
/// </summary>
public enum GrepLineKind
{
    /// <summary>
    ///  A line that matches (git's ':').
    /// </summary>
    Match,

    /// <summary>
    ///  A line around a match (git's '-').
    /// </summary>
    Context,

    /// <summary>
    ///  The function a match is in (git's '=', <c>--show-function</c>).
    /// </summary>
    Function,

    /// <summary>
    ///  Between two separate parts of the file (git's "--").
    /// </summary>
    Separator,
}

/// <summary>
///  A line of a file's git-grep output: its line number in the file (none for a separator), its kind, its text and where
///  git marked the matches in it.
/// </summary>
public sealed record GrepLine(int? Number, GrepLineKind Kind, string Text, IReadOnlyList<(int Start, int Length)> Matches);

/// <summary>
///  Upstream's "Find in commit files using git-grep" (<c>FileStatusList.FindInCommitFilesGitGrep</c>,
///  <c>FormFindInCommitFilesGitGrep</c>, <c>GrepHighlightService</c>): the search text as git-grep's argument and a file's
///  matches as upstream's viewer shows them.
/// </summary>
public static partial class GitGrep
{
    // Upstream texts.
    public const string Title = "Find in commit files using git-grep";
    public const string Watermark = "Find in commit files using git-grep regular expression...";

    // Upstream's GrepHighlightService line kinds.
    private const string FunctionKind = "=";
    private const string MatchKind = ":";
    private const string SeparatorLine = "--";

    private const char Escape = '\u001b';

    [GeneratedRegex(@"(^|\s)-e(\s|\s+['""])", RegexOptions.ExplicitCapture)]
    private static partial Regex GrepStringRegex { get; }

    [GeneratedRegex(@"^(?<line>\d+)(?<kind>:|.)(?<text>.*)$", RegexOptions.ExplicitCapture)]
    private static partial Regex GrepLineRegex { get; }

    /// <summary>
    ///  Upstream's <c>FindInCommitFilesGitGrep</c>: a text without git-grep's <c>-e</c> becomes one pattern (<c>-e "text"</c>,
    ///  its backslashes doubled); "" for no search.
    /// </summary>
    public static string SearchArgument(string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return "";
        }

        return GrepStringRegex.IsMatch(search) ? search : $@"-e ""{search.Replace(@"\\", @"\\\\")}""";
    }

    /// <summary>
    ///  Upstream's grep view of a file: <c>git grep</c> of <paramref name="grepString"/> in <paramref name="path"/> at
    ///  <paramref name="revision"/> (the working directory's or the index's artificial id for those), with line numbers, the
    ///  functions the matches are in and git's colors for the matches, as upstream's <c>GetGrepFileAsync</c> runs it with
    ///  <c>GrepHighlightService</c>'s configuration and the viewer's <see cref="DiffOptions.GrepArguments"/>. On git's error,
    ///  its message.
    /// </summary>
    public static async Task<(IReadOnlyList<GrepLine> Lines, string? Error)> FileAsync(GitModule module, ObjectId revision,
        string path, string grepString, DiffOptions options, CancellationToken cancellationToken = default)
    {
        ExecutionResult result = await module.GetGrepFileAsync(revision, path, string.Join(" ", options.GrepArguments), grepString,
            useGitColoring: true, showFunctionName: true, Configuration(module), options.EncodingFor(module),
            cancellationToken);
        return result.ExitedSuccessfully
            ? (Parse(result.StandardOutput), null)
            : ([], $"{result.StandardError}{Environment.NewLine}Git command (exit code: {result.ExitCodeDisplay}): {result}");
    }

    /// <summary>
    ///  Upstream's <c>GetGrepFilesStatus</c> with its settings applied, as the File tree tab lists a search's files: the
    ///  paths of <paramref name="revision"/> (a commit, or the name of one) that git-grep matches; none for no match or
    ///  git's error, which upstream cannot tell apart either.
    /// </summary>
    public static Task<IReadOnlyList<string>> FilesAsync(GitModule module, string revision, string grepString,
        CancellationToken cancellationToken = default)
        => Task.Run<IReadOnlyList<string>>(() =>
        {
            ObjectId? id = ObjectId.TryParse(revision, out ObjectId parsed) ? parsed : module.RevParse(revision);
            return id is { } objectId
                ?
                [
                    .. module.GetGrepFilesStatus(objectId, grepString, applyAppSettings: true, cancellationToken)
                        .Select(file => file.Name)
                ]
                : [];
        }, cancellationToken);

    /// <summary>
    ///  Upstream <c>GrepHighlightService.GetGitCommandConfiguration</c>: git's own grep configuration, line numbers and
    ///  separators without colors (they are parsed), and the function line reversed and a match in reverse when git has no
    ///  colors of its own for them.
    /// </summary>
    public static IGitCommandConfiguration Configuration(IGitModule module)
    {
        GitCommandConfiguration configuration = new();
        foreach (GitConfigItem item in GitCommandConfiguration.Default.Get("grep"))
        {
            configuration.Add(item, "grep");
        }

        configuration.Add(new GitConfigItem("color.grep.linenumber", ""), "grep");
        configuration.Add(new GitConfigItem("color.grep.separator", ""), "grep");
        SetIfUnsetInGit("color.grep.function", "white dim reverse");
        if (AppSettings.ReverseGitColoring.Value)
        {
            SetIfUnsetInGit("color.grep.matchselected", "red bold reverse");
        }

        return configuration;

        void SetIfUnsetInGit(string key, string value)
        {
            if (string.IsNullOrEmpty(module.GetEffectiveSetting(key)))
            {
                configuration.Add(new GitConfigItem(key, value), "grep");
            }
        }
    }

    /// <summary>
    ///  Upstream <c>GrepHighlightService.SetText</c>: each "line:text", "line-text" or "line=text" of git's output, a
    ///  separator only between two parts and never right before a function line, git's colored text as the matches.
    /// </summary>
    public static IReadOnlyList<GrepLine> Parse(string output)
    {
        List<GrepLine> lines = [];
        bool skipNextSeparator = false;
        bool pendingSeparator = false;
        foreach (string line in output.Split('\n'))
        {
            if (line == SeparatorLine)
            {
                if (!skipNextSeparator && lines.Count > 0)
                {
                    pendingSeparator = true;
                }

                continue;
            }

            Match match = GrepLineRegex.Match(line);
            if (!match.Success || !int.TryParse(match.Groups["line"].ValueSpan, out int number))
            {
                // git-grep's last line is empty; upstream shows any other line it cannot read as it is.
                if (line.Length > 0)
                {
                    (string plain, IReadOnlyList<(int, int)> marked) = Uncolor(line);
                    lines.Add(new GrepLine(null, GrepLineKind.Context, plain, marked));
                }

                pendingSeparator = false;
                continue;
            }

            string kind = match.Groups["kind"].Value;
            skipNextSeparator = kind == FunctionKind;
            if (pendingSeparator && !skipNextSeparator)
            {
                lines.Add(new GrepLine(null, GrepLineKind.Separator, SeparatorLine, []));
            }

            pendingSeparator = false;
            (string text, IReadOnlyList<(int Start, int Length)> matches) = Uncolor(match.Groups["text"].Value);
            lines.Add(new GrepLine(number,
                kind == FunctionKind ? GrepLineKind.Function
                : kind == MatchKind ? GrepLineKind.Match
                : GrepLineKind.Context,
                text, kind == FunctionKind ? [] : matches));
        }

        return lines;
    }

    // The text without git's escape sequences, and the parts git colored.
    private static (string Text, IReadOnlyList<(int Start, int Length)> Colored) Uncolor(string text)
    {
        StringBuilder plain = new(text.Length);
        List<(int Start, int Length)> colored = [];
        int? coloredFrom = null;
        int i = 0;
        while (i < text.Length)
        {
            if (text[i] != Escape || i + 1 >= text.Length || text[i + 1] != '[')
            {
                plain.Append(text[i++]);
                continue;
            }

            int end = i + 2;
            while (end < text.Length && !char.IsAsciiLetter(text[end]))
            {
                end++;
            }

            if (end < text.Length && text[end] == 'm')
            {
                string parameters = text[(i + 2)..end];
                bool reset = parameters.Length == 0 || parameters.Split(';').All(code => code is "0" or "");
                if (reset && coloredFrom is { } from)
                {
                    Add(from);
                    coloredFrom = null;
                }
                else if (!reset)
                {
                    coloredFrom ??= plain.Length;
                }
            }

            i = end + 1;
        }

        if (coloredFrom is { } open)
        {
            Add(open);
        }

        return (plain.ToString(), colored);

        void Add(int from)
        {
            if (plain.Length > from)
            {
                colored.Add((from, plain.Length - from));
            }
        }
    }
}

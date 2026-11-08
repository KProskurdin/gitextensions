using System.Drawing;
using System.Text;
using System.Text.RegularExpressions;
using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility.Git;
using GitExtUtils;
using GitExtUtils.GitUI.Theming;
using GitUI.Editor.Diff;
using GitUI.Theming;
using ICSharpCode.TextEditor.Document;

namespace GitExtensions.Xplat.Core.Diff;

/// <summary>
///  A part of a line in the colors git gave it. A null color is the viewer's.
/// </summary>
public sealed record DiffColorRun(int Start, int Length, Color? Foreground, Color? Background);

/// <summary>
///  Upstream's git coloring (<c>DiffHighlightService</c>): git's output read with <c>--color=always</c>, by upstream's
///  <c>AnsiEscapeUtilities</c> and <c>DiffLineNumAnalyzer</c>, linked unchanged.
/// </summary>
public static partial class GitColoring
{
    private const string ReverseArgument = "reverse";
    private const char LineFeed = '\n';
    private const char CarriageReturn = '\r';

    // Upstream's "Fix: Force black foreground" needs bright colors, from this git.
    private static readonly GitVersion _supportsBrightColors = new("2.26.0.0");

    [GeneratedRegex(@"^\s*(\d+|-):", RegexOptions.ExplicitCapture)]
    private static partial Regex RangeHeaderRegex { get; }

    /// <summary>
    ///  Upstream's <c>GetGitCommandConfiguration</c>: git's colors for <paramref name="command"/>, each unless the user's git
    ///  configuration sets it, on the theme's backgrounds with <c>ReverseGitColoring</c>.
    /// </summary>
    public static IGitCommandConfiguration Configuration(IGitModule module, string command)
    {
        GitCommandConfiguration configuration = new();
        foreach (GitConfigItem item in GitCommandConfiguration.Default.Get(command))
        {
            configuration.Add(item, command);
        }

        // Disabled by default, which the user manual says can be enabled.
        SetIfUnsetInGit("diff.colormovedws", "no");
        SetIfUnsetInGit("diff.wordregex", "\"[a-z0-9_]+|.\"");
        SetIfUnsetInGit("diff.colormoved", "dimmed-zebra");
        bool reversed = AppSettings.ReverseGitColoring.Value;
        string reverse = reversed ? ReverseArgument : "";
        SetIfUnsetInGit("color.diff.old", $"red {reverse}");
        SetIfUnsetInGit("color.diff.new", $"green {reverse}");
        if (reversed)
        {
            // Upstream forces a black foreground, or it would be calculated to white.
            bool bright = module.GitVersion >= _supportsBrightColors;
            SetIfUnsetInGit("color.diff.oldmoved", bright ? "black brightmagenta" : "reverse bold magenta");
            SetIfUnsetInGit("color.diff.newmoved", bright ? "black brightblue" : "reverse bold blue");
            SetIfUnsetInGit("color.diff.oldmovedalternative", bright ? "black brightcyan" : "reverse bold cyan");
            SetIfUnsetInGit("color.diff.newmovedalternative", bright ? "black brightyellow" : "reverse bold yellow");
        }

        SetIfUnsetInGit("color.diff.oldmoveddimmed", $"magenta dim {reverse}");
        SetIfUnsetInGit("color.diff.newmoveddimmed", $"blue dim {reverse}");
        SetIfUnsetInGit("color.diff.oldmovedalternativedimmed", $"cyan dim {reverse}");
        SetIfUnsetInGit("color.diff.newmovedalternativedimmed", $"yellow dim {reverse}");
        if (command == RangeDiffCommand)
        {
            SetIfUnsetInGit("color.diff.oldbold", $"brightred {reverse}");
            SetIfUnsetInGit("color.diff.newbold", $"brightgreen {reverse}");
            SetIfUnsetInGit("color.diff.olddimmed", $"red dim {reverse}");
            SetIfUnsetInGit("color.diff.newdimmed", $"green dim {reverse}");
        }

        return configuration;

        void SetIfUnsetInGit(string key, string value)
        {
            if (string.IsNullOrEmpty(module.GetEffectiveSetting(key)))
            {
                configuration.Add(new GitConfigItem(key, value), command);
            }
        }
    }

    /// <summary>
    ///  Git's name of the range diff command, whose colors differ.
    /// </summary>
    public const string RangeDiffCommand = "range-diff";

    /// <summary>
    ///  A diff as upstream's <c>PatchHighlightService</c> and <c>CombinedDiffHighlightService</c> read it: without the escape
    ///  codes, each line with its colors, kind and numbers. In a word diff (no first column of + or -) upstream's analyzer
    ///  guesses a line's kind from its colors.
    /// </summary>
    public static IReadOnlyList<DiffLine> ParseDiff(string colored, bool isCombinedDiff = false, bool isGitWordDiff = false)
    {
        (string text, List<TextMarker> markers) = RemoveEscapeCodes(colored);
        DiffLinesInfo info = DiffLineNumAnalyzer.Analyze(text, markers, isCombinedDiff, isGitWordDiff);
        return Lines(text, markers, (number, line) =>
        {
            if (!info.DiffLines.TryGetValue(number, out DiffLineInfo? lineInfo))
            {
                return new DiffLine(line, DiffLineKind.Header);
            }

            DiffLineKind kind = lineInfo.LineType switch
            {
                DiffLineType.Header => line.StartsWith("@@", StringComparison.Ordinal) ? DiffLineKind.Hunk : DiffLineKind.Header,
                DiffLineType.Minus or DiffLineType.MinusLeft => DiffLineKind.Removed,
                DiffLineType.Plus or DiffLineType.PlusRight => DiffLineKind.Added,
                _ => DiffLineKind.Context,
            };
            return new DiffLine(line, kind, Number(lineInfo.LeftLineNumber), Number(lineInfo.RightLineNumber))
            {
                IsMovedLine = lineInfo.IsMovedLine,
            };
        });
    }

    /// <summary>
    ///  <c>git range-diff</c>'s output as upstream's <c>RangeDiffHighlightService</c> reads it: numbered lines, a commit's
    ///  line as a header.
    /// </summary>
    public static IReadOnlyList<DiffLine> ParseRangeDiff(string colored)
    {
        (string text, List<TextMarker> markers) = RemoveEscapeCodes(colored);
        return Lines(text, markers, (number, line) => new DiffLine(line,
            RangeHeaderRegex.IsMatch(line) ? DiffLineKind.Header : DiffLineKind.Context, NewNumber: number));
    }

    private static int? Number(int number) => number == DiffLineInfo.NotApplicableLineNum ? null : number;

    private static (string Text, List<TextMarker> Markers) RemoveEscapeCodes(string colored)
    {
        StringBuilder text = new(colored.Length);
        List<TextMarker> markers = [];
        AnsiEscapeUtilities.ParseEscape(colored, text, markers);
        return (text.ToString(), markers);
    }

    private static List<DiffLine> Lines(string text, IReadOnlyList<TextMarker> markers, Func<int, string, DiffLine> create)
    {
        List<DiffLine> lines = [];
        Color editorBackground = AppColor.EditorBackground.GetThemeColor();
        int lineStart = 0;
        int number = 0;
        int firstMarker = 0;
        while (lineStart < text.Length)
        {
            int newLine = text.IndexOf(LineFeed, lineStart);
            int lineEnd = newLine < 0 ? text.Length : newLine;
            bool carriageReturn = lineEnd > lineStart && text[lineEnd - 1] == CarriageReturn;
            int textEnd = carriageReturn ? lineEnd - 1 : lineEnd;
            number++;

            while (firstMarker < markers.Count && markers[firstMarker].EndOffset < lineStart)
            {
                firstMarker++;
            }

            List<DiffColorRun> runs = [];
            for (int i = firstMarker; i < markers.Count && markers[i].Offset < textEnd; i++)
            {
                TextMarker marker = markers[i];
                int start = Math.Max(marker.Offset, lineStart);
                int end = Math.Min(marker.EndOffset + 1, textEnd);
                if (end > start)
                {
                    runs.Add(new DiffColorRun(start - lineStart, end - start, marker.ForeColor,
                        marker.Color == editorBackground ? null : marker.Color));
                }
            }

            lines.Add(create(number, text[lineStart..textEnd]) with
            {
                Ending = newLine < 0 ? LineEnding.None
                    : carriageReturn ? LineEnding.CarriageReturnLineFeed
                    : LineEnding.LineFeed,
                ColorRuns = runs,
            });
            lineStart = lineEnd + 1;
        }

        return lines;
    }
}

using System.Drawing;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using GitCommands;
using GitCommands.Settings;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtUtils;
using GitUI.Editor.Diff;
using ICSharpCode.TextEditor.Document;

namespace GitExtensions.Xplat.Core.Diff;

/// <summary>
///  Upstream's "Difftastic" diff appearance: <c>git difftool --tool=difftastic</c>, whose colored columns upstream's
///  <c>DifftasticHighlightService</c> reads (the service is tied to WinForms' line number control, so its parsing is ported
///  here) and <c>FileViewer.GetDifftasticArguments</c> sets up.
/// </summary>
public static partial class Difftastic
{
    private const string ToolSetting = "difftool.difftastic.cmd";
    private const string ToolArgument = "--tool=difftastic";
    private const string TextArgument = "--text";
    private const int EntireFileContext = 9000;
    private const int MinWidth = 88;
    private const int MaxWidth = 200;
    private const int PixelsPerColumn = 7;

    [GeneratedRegex(@"^(\s*(?<matchStart>(?<lineNo>\d+)|(\.+)) )", RegexOptions.ExplicitCapture)]
    private static partial Regex LineNoRegex { get; }

    /// <summary>
    ///  Upstream FileViewer's <c>IsDifftasticEnabled</c>: git knows the tool.
    /// </summary>
    public static bool IsEnabled(IGitModule module) => !string.IsNullOrEmpty(module.GetEffectiveSetting(ToolSetting));

    /// <summary>
    ///  Upstream's guess of a reasonable even width in columns from the viewer's width, so that the scroll bar is barely used.
    /// </summary>
    public static int WidthFor(double viewerPixels)
        => Math.Max(MinWidth, Math.Min(MaxWidth, (int)viewerPixels / PixelsPerColumn)) / 2 * 2;

    /// <summary>
    ///  Upstream's <c>GetSelectedPatchAsync</c> for Difftastic: the tool's output between two revisions as lines, or its error.
    /// </summary>
    public static async Task<IReadOnlyList<DiffLine>> DiffAsync(GitModule module, ObjectId first, ObjectId second,
        string filePath, string? oldPath, bool isTracked, DiffOptions options,
        CancellationToken cancellationToken = default)
    {
        int width = options.DifftasticWidth;
        SetEnvironment(options, width);
        ExecutionResult result = await module.GetSingleDifftoolAsync(first, second, filePath, oldPath,
            new ArgumentBuilder { ToolArgument, { options.TreatAllFilesAsText, TextArgument } }, cacheResult: false,
            extraCacheKey: "", isTracked, useGitColoring: true, cancellationToken);
        if (!result.ExitedSuccessfully)
        {
            return DiffParser.ParseText(
                $"Git command exit code: {result.ExitCodeDisplay}{Environment.NewLine}{result.StandardError}");
        }

        return Parse(GitModule.ReEncodeStringFromLossless(result.StandardOutput, options.EncodingFor(module)) ?? "",
            width,
            AppSettings.ReverseGitColoring.Value);
    }

    /// <summary>
    ///  Upstream's <c>DifftasticHighlightService</c>: a file's header after an empty line, and for each line the left and right
    ///  line numbers taken out of the text, whose colors say whether the line was removed or added.
    /// </summary>
    /// <param name="column">The width the tool was given, whose half is where the second column starts.</param>
    /// <param name="reverseGitColoring">Upstream's <c>ReverseGitColoring</c>, which puts the colors on the backgrounds.</param>
    public static IReadOnlyList<DiffLine> Parse(string text, int column, bool reverseGitColoring)
    {
        List<DiffLine> lines = [];
        int rightColumnStart = 0;
        int halfColumn = column / 2;
        bool nextIsHeader = true;
        foreach (string rawLine in text.Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                nextIsHeader = true;
                continue;
            }

            StringBuilder lineBuilder = new(column);
            List<TextMarker> markers = [];
            AnsiEscapeUtilities.ParseEscape(rawLine, lineBuilder, markers, themeColors: reverseGitColoring);
            int leftLineNo = DiffLineInfo.NotApplicableLineNum;
            int rightLineNo = DiffLineInfo.NotApplicableLineNum;
            if (nextIsHeader)
            {
                nextIsHeader = false;
                Add(leftLineNo, rightLineNo, DiffLineKind.Header);
                continue;
            }

            DiffLineKind kind = DiffLineKind.Context;
            Match matchLeft = LineNoRegex.Match(lineBuilder.ToString());
            if (!matchLeft.Success)
            {
                Add(leftLineNo, rightLineNo, kind);
                continue;
            }

            int leftLength;
            if (matchLeft.Groups["matchStart"].Index >= halfColumn)
            {
                // No left line number, as with zero context; a consistent start for the second column.
                leftLength = rightColumnStart > 0 ? halfColumn - rightColumnStart : 0;
            }
            else
            {
                leftLength = matchLeft.Length;
                if (matchLeft.Groups["lineNo"].Success
                    && int.TryParse(matchLeft.Groups["lineNo"].ValueSpan, CultureInfo.InvariantCulture, out int lineNo))
                {
                    leftLineNo = lineNo;
                }

                if (markers.Count > 0 && markers[0].Offset < leftLength)
                {
                    Color color = ColorOf(markers[0], reverseGitColoring);
                    if (!IsUnchanged(color))
                    {
                        if (color.R > color.G)
                        {
                            kind = DiffLineKind.Removed;
                        }
                        else
                        {
                            kind = DiffLineKind.Added;
                            rightLineNo = leftLineNo;
                            leftLineNo = DiffLineInfo.NotApplicableLineNum;
                        }
                    }
                }
            }

            if (leftLength > 0)
            {
                lineBuilder.Remove(0, Math.Min(leftLength, lineBuilder.Length));
                foreach (TextMarker marker in markers)
                {
                    RemoveLineNoPart(marker, 0, leftLength);
                }
            }

            int rightStartOffset = halfColumn - leftLength;
            Match matchRight;
            if (lineBuilder.Length > rightStartOffset && rightStartOffset >= 0
                                                      && LineNoRegex.Match(lineBuilder.ToString()[rightStartOffset..])
                                                          is { Success: true } match)
            {
                matchRight = match;
                if (rightColumnStart == 0)
                {
                    rightColumnStart = rightStartOffset;
                }
            }
            else
            {
                rightStartOffset = 0;
                matchRight = LineNoRegex.Match(lineBuilder.ToString());
            }

            if (!matchRight.Success)
            {
                Add(leftLineNo, rightLineNo, kind);
                continue;
            }

            if (matchRight.Groups["lineNo"].Success
                && int.TryParse(matchRight.Groups["lineNo"].ValueSpan, CultureInfo.InvariantCulture,
                    out int rightNumber))
            {
                rightLineNo = rightNumber;
            }

            int rightLength = matchRight.Length;
            lineBuilder.Remove(rightStartOffset, rightLength);
            int columnGap = 0;
            if (rightStartOffset > 0)
            {
                if (rightColumnStart == 0)
                {
                    rightColumnStart = rightStartOffset;
                }

                // Spaces, so that both sides are aligned.
                columnGap = rightColumnStart - rightStartOffset;
                if (columnGap > 0)
                {
                    lineBuilder.Insert(rightStartOffset, new string(' ', columnGap));
                }
            }

            bool first = true;
            foreach (TextMarker marker in markers)
            {
                if (marker.EndOffset < rightStartOffset)
                {
                    continue;
                }

                if (first)
                {
                    first = false;
                    if (!IsUnchanged(ColorOf(marker, reverseGitColoring)))
                    {
                        // Merged with what the left side said: changed on both sides is neither added nor removed.
                        kind = kind == DiffLineKind.Context ? DiffLineKind.Added : DiffLineKind.Context;
                    }
                }

                RemoveLineNoPart(marker, rightStartOffset, rightLength);
                if (marker.Offset >= rightStartOffset)
                {
                    marker.Offset += columnGap;
                }
            }

            Add(leftLineNo, rightLineNo, kind);

            continue;

            void Add(int left, int right, DiffLineKind lineKind)
                => lines.Add(new DiffLine(lineBuilder.ToString(), lineKind, Number(left), Number(right))
                {
                    ColorRuns =
                    [
                        .. markers.Where(marker => marker.Length > 0).Select(marker => Run(marker, lineBuilder.Length))
                            .Where(run => run.Length > 0)
                    ],
                });
        }

        return lines;
    }

    private static DiffColorRun Run(TextMarker marker, int lineLength)
    {
        int start = Math.Clamp(marker.Offset, 0, lineLength);
        return new DiffColorRun(start, Math.Clamp(marker.Offset + marker.Length, 0, lineLength) - start,
            marker.ForeColor,
            marker.Color);
    }

    private static int? Number(int number) => number == DiffLineInfo.NotApplicableLineNum ? null : number;

    private static Color ColorOf(TextMarker marker, bool reverseGitColoring)
        => reverseGitColoring ? marker.Color : marker.ForeColor ?? Color.Empty;

    // Upstream's guess: the tool's theme is gray for what is unchanged.
    private static bool IsUnchanged(Color color) => color.R == color.G;

    private static void RemoveLineNoPart(TextMarker marker, int offset, int length)
    {
        if (marker.Offset + marker.Length <= offset)
        {
            return;
        }

        if (marker.Offset >= offset + length)
        {
            marker.Offset -= length;
            return;
        }

        if (marker.Offset <= offset && offset + length <= marker.Offset + marker.Length)
        {
            marker.Length -= length;
            return;
        }

        if (marker.Offset > offset)
        {
            marker.Length -= marker.Offset - offset;
            marker.Offset = offset;
            return;
        }

        marker.Length -= marker.Offset + marker.Length - offset;
    }

    // Upstream's GetDifftasticArguments: the tool reads its options from the environment, also through WSL.
    private static void SetEnvironment(DiffOptions options, int width)
    {
        EnvironmentAbstraction environment = new();
        environment.SetEnvironmentVariable("DFT_COLOR", "always");

        // "dark" applies bold-bold colors, "light" corresponds better with git's.
        environment.SetEnvironmentVariable("DFT_BACKGROUND", "light");
        environment.SetEnvironmentVariable("DFT_SYNTAX_HIGHLIGHT", options.ShowSyntaxHighlighting ? "on" : "off");
        int context = options.ShowEntireFile ? EntireFileContext : options.ContextLines;
        environment.SetEnvironmentVariable("DFT_CONTEXT", context.ToString(CultureInfo.InvariantCulture));

        // Reasonably similar to ignoring whitespace at the end of a line.
        environment.SetEnvironmentVariable("DFT_STRIP_CR",
            options.IgnoreWhitespace == IgnoreWhitespaceKind.None ? "off" : "on");
        environment.SetEnvironmentVariable("DFT_WIDTH", width.ToString(CultureInfo.InvariantCulture));
        environment.SetEnvironmentVariable("WSLENV",
            "DFT_COLOR:DFT_BACKGROUND:DFT_SYNTAX_HIGHLIGHT:DFT_CONTEXT:DFT_STRIP_CR:DFT_WIDTH");
    }
}

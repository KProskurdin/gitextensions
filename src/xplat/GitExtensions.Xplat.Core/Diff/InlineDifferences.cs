using System.Drawing;
using GitCommands;
using GitExtUtils.GitUI.Theming;
using GitUI.Editor.Diff;
using GitUI.Theming;
using ICSharpCode.TextEditor.Document;

namespace GitExtensions.Xplat.Core.Diff;

/// <summary>
///  Upstream <c>DiffHighlightService.AddInlineDifferenceMarkers</c>, with its linked <c>LinesMatcher</c>: a removed and an added
///  line that belong together have what they share dimmed, so that what changed stands out. The service itself is tied to the
///  WinForms text editor, so its logic is ported here.
/// </summary>
/// <summary>
///  Upstream's <c>CreateAnchorMarker</c>: a zero-width mark before <paramref name="Column"/> of a line (a
///  <c>TextMarkerType.InterChar</c>), in the color of the text the paired line has there.
/// </summary>
public sealed record DiffAnchor(int Column, Color Color);

public static class InlineDifferences
{
    // In order to skip the + or - of a line.
    private const int DiffContentOffset = 1;

    private const int MaxLength = 2000;
    private const int MaxGapLines = 5;

    /// <summary>
    ///  <paramref name="lines"/> with <see cref="DiffLine.DimRuns"/> set as <paramref name="options"/> ask: upstream's
    ///  <c>SetHighlighting</c> does nothing for a git word diff, and dims the background unless git's colors are on the
    ///  foreground (<c>ReverseGitColoring</c> off).
    /// </summary>
    public static IReadOnlyList<DiffLine> Add(IReadOnlyList<DiffLine> lines, DiffOptions options)
        => options.IsGitWordDiff
            ? lines
            : Add(lines, dimBackground: !options.UsesGitColors || AppSettings.ReverseGitColoring.Value);

    /// <summary>
    ///  <paramref name="lines"/> with what the blocks of removed and added lines share dimmed.
    /// </summary>
    public static IReadOnlyList<DiffLine> Add(IReadOnlyList<DiffLine> lines, bool dimBackground)
    {
        Dictionary<int, List<DiffColorRun>> dims = [];
        Dictionary<int, List<DiffAnchor>> anchors = [];
        int index = 0;
        while (index < lines.Count)
        {
            // Git presents the removed lines directly followed by the added, in a block.
            List<ISegment> removed = BlockOfLines(lines, DiffLineKind.Removed, ref index, found: false);
            if (removed.Count == 0)
            {
                continue;
            }

            List<ISegment> added = BlockOfLines(lines, DiffLineKind.Added, ref index, found: true);
            if (added.Count == 0)
            {
                continue;
            }

            foreach ((ISegment lineRemoved, ISegment lineAdded) in LinesMatcher.FindLinePairs(Text, removed, added))
            {
                AddDifferences(dims, anchors, lineRemoved.Offset, lineAdded.Offset, Text(lineRemoved), Text(lineAdded),
                    dimBackground);
            }
        }

        if (dims.Count == 0 && anchors.Count == 0)
        {
            return lines;
        }

        return
        [
            .. lines.Select((line, lineIndex) => (dims.TryGetValue(lineIndex, out List<DiffColorRun>? runs),
                    anchors.TryGetValue(lineIndex, out List<DiffAnchor>? marks)) switch
                {
                    (false, false) => line,
                    _ => line with
                    {
                        DimRuns = runs is null ? line.DimRuns : [.. runs.Where(run => run.Length > 0).OrderBy(run => run.Start)],
                        Anchors = marks is null ? line.Anchors : [.. marks.OrderBy(mark => mark.Column)],
                    },
                })
        ];

        // A segment's offset here is the line's index in the list.
        string Text(ISegment line) => lines[line.Offset].Text[Math.Min(DiffContentOffset, lines[line.Offset].Text.Length)..];
    }

    // Upstream's GetBlockOfLines: the next lines of a kind, across a few context lines, without those that seem moved.
    private static List<ISegment> BlockOfLines(IReadOnlyList<DiffLine> lines, DiffLineKind kind, ref int index, bool found)
    {
        List<ISegment> result = [];
        int gapLines = 0;
        for (; index < lines.Count; ++index)
        {
            DiffLine line = lines[index];
            if (line.Kind != kind)
            {
                if (!found)
                {
                    continue;
                }

                if (line.Kind == DiffLineKind.Context && gapLines < MaxGapLines)
                {
                    ++gapLines;
                    continue;
                }

                break;
            }

            gapLines = 0;
            if (line.IsMovedLine)
            {
                continue;
            }

            found = true;
            result.Add(new Segment { Offset = index, Length = line.Text.Length });
        }

        return result;
    }

    private static void AddDifferences(Dictionary<int, List<DiffColorRun>> dims, Dictionary<int, List<DiffAnchor>> anchors,
        int lineRemoved, int lineAdded, string removed, string added, bool dimBackground)
    {
        ReadOnlySpan<char> textRemoved = removed.Length <= MaxLength ? removed : removed.AsSpan(0, MaxLength);
        ReadOnlySpan<char> textAdded = added.Length <= MaxLength ? added : added.AsSpan(0, MaxLength);
        (int identicalAtStart, int identicalAtEnd) = AddDifferences(dims, anchors, lineRemoved, lineAdded, textRemoved,
            textAdded, DiffContentOffset, DiffContentOffset, dimBackground);
        if (identicalAtStart > 0)
        {
            AddDimmed(dims, lineRemoved, lineAdded, DiffContentOffset, DiffContentOffset, identicalAtStart, dimBackground);
        }

        if (identicalAtEnd > 0)
        {
            AddDimmed(dims, lineRemoved, lineAdded, DiffContentOffset + textRemoved.Length - identicalAtEnd,
                DiffContentOffset + textAdded.Length - identicalAtEnd, identicalAtEnd, dimBackground);
        }
    }

    // Upstream's recursive AddDifferenceMarkers: split at the best common word, then what remains on each side; where only one
    // side has text left, the other gets upstream's zero-width anchor.
    private static (int IdenticalAtStart, int IdenticalAtEnd) AddDifferences(Dictionary<int, List<DiffColorRun>> dims,
        Dictionary<int, List<DiffAnchor>> anchors, int lineRemoved, int lineAdded, ReadOnlySpan<char> textRemoved,
        ReadOnlySpan<char> textAdded, int offsetRemoved, int offsetAdded, bool dimBackground)
    {
        int identicalAtStart = 0;
        int identicalAtEnd = 0;
        int endRemoved = textRemoved.Length;
        int endAdded = textAdded.Length;
        if (endRemoved == endAdded && textRemoved.SequenceEqual(textAdded))
        {
            return (endRemoved, identicalAtEnd);
        }

        (string? commonWord, int startRemoved, int startAdded) =
            LinesMatcher.FindBestMatch(textRemoved.ToString(), textAdded.ToString());
        if (commonWord is not null)
        {
            int lengthIdentical = commonWord.Length;
            int rightRemoved = startRemoved + lengthIdentical;
            int rightAdded = startAdded + lengthIdentical;
            (int identicalAtStartOfRight, identicalAtEnd) = AddDifferences(dims, anchors, lineRemoved, lineAdded,
                textRemoved[rightRemoved..], textAdded[rightAdded..], offsetRemoved + rightRemoved, offsetAdded + rightAdded,
                dimBackground);
            lengthIdentical += identicalAtStartOfRight;
            (identicalAtStart, int identicalAtEndOfLeft) = AddDifferences(dims, anchors, lineRemoved, lineAdded,
                textRemoved[..startRemoved], textAdded[..startAdded], offsetRemoved, offsetAdded, dimBackground);
            lengthIdentical += identicalAtEndOfLeft;
            startRemoved -= identicalAtEndOfLeft;
            startAdded -= identicalAtEndOfLeft;
            if (startRemoved == identicalAtStart && startAdded == identicalAtStart)
            {
                identicalAtStart += lengthIdentical;
            }
            else if (startRemoved + lengthIdentical + identicalAtEnd == endRemoved
                     && startAdded + lengthIdentical + identicalAtEnd == endAdded)
            {
                identicalAtEnd += lengthIdentical;
            }
            else
            {
                AddDimmed(dims, lineRemoved, lineAdded, offsetRemoved + startRemoved, offsetAdded + startAdded, lengthIdentical,
                    dimBackground);
            }

            return (identicalAtStart, identicalAtEnd);
        }

        int minEnd = Math.Min(endRemoved, endAdded);
        while (identicalAtStart < minEnd && textRemoved[identicalAtStart] == textAdded[identicalAtStart])
        {
            ++identicalAtStart;
        }

        int endOfRemoved = endRemoved;
        int endOfAdded = endAdded;
        while (endOfRemoved > identicalAtStart && endOfAdded > identicalAtStart
               && textRemoved[endOfRemoved - 1] == textAdded[endOfAdded - 1])
        {
            --endOfRemoved;
            --endOfAdded;
            ++identicalAtEnd;
        }

        // Upstream's CreateAnchorMarker in the color of the other side's text.
        int differentRemoved = endOfRemoved - identicalAtStart;
        int differentAdded = endOfAdded - identicalAtStart;
        if (differentRemoved == 0 && differentAdded > 0)
        {
            AddAnchor(anchors, lineRemoved,
                new DiffAnchor(offsetRemoved + identicalAtStart, AppColor.AnsiTerminalGreenForeBold.GetThemeColor()));
        }
        else if (differentRemoved > 0 && differentAdded == 0)
        {
            AddAnchor(anchors, lineAdded,
                new DiffAnchor(offsetAdded + identicalAtStart, AppColor.AnsiTerminalRedForeBold.GetThemeColor()));
        }

        return (identicalAtStart, identicalAtEnd);
    }

    private static void AddAnchor(Dictionary<int, List<DiffAnchor>> anchors, int line, DiffAnchor anchor)
    {
        if (!anchors.TryGetValue(line, out List<DiffAnchor>? marks))
        {
            anchors[line] = marks = [];
        }

        marks.Add(anchor);
    }

    private static void AddDimmed(Dictionary<int, List<DiffColorRun>> dims, int lineRemoved, int lineAdded, int startRemoved,
        int startAdded, int length, bool dimBackground)
    {
        Add(lineRemoved, Dimmed(startRemoved, length, isRemoved: true, dimBackground));
        Add(lineAdded, Dimmed(startAdded, length, isRemoved: false, dimBackground));

        void Add(int line, DiffColorRun run)
        {
            if (!dims.TryGetValue(line, out List<DiffColorRun>? runs))
            {
                dims[line] = runs = [];
            }

            runs.Add(run);
        }
    }

    // Upstream's CreateDimmedMarker.
    private static DiffColorRun Dimmed(int start, int length, bool isRemoved, bool dimBackground)
    {
        if (dimBackground)
        {
            Color back = (isRemoved ? AppColor.AnsiTerminalRedBackNormal : AppColor.AnsiTerminalGreenBackNormal)
                .GetThemeColor().DimColor().DimColor();
            return new DiffColorRun(start, length, back.GetTextColor(), back);
        }

        Color fore = (isRemoved ? AppColor.AnsiTerminalRedForeBold : AppColor.AnsiTerminalGreenForeBold)
            .GetThemeColor().DimColor();
        return new DiffColorRun(start, length, fore, Background: null);
    }
}

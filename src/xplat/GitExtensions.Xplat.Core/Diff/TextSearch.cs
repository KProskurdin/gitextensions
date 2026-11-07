namespace GitExtensions.Xplat.Core.Diff;

/// <summary>
///  Upstream's <c>TextEditorSearcher</c> (FindAndReplaceForm.cs) over a viewer's text: the next or previous occurrence of
///  <see cref="LookFor"/> from an offset, wrapping around at the end of the text or of the scan region, case-insensitive
///  unless <see cref="MatchCase"/>, and only whole words with <see cref="MatchWholeWordOnly"/> (a word is letters, digits
///  and underscores).
/// </summary>
public sealed class TextSearch(string text)
{
    // Upstream's search string, upper case when the case does not matter.
    private string _lookFor2 = "";

    public string LookFor { get; set; } = "";

    public bool MatchCase { get; set; }

    public bool MatchWholeWordOnly { get; set; }

    /// <summary>
    ///  Upstream's scan region (a multi-line selection the search keeps to): its start and length, or null for the whole
    ///  text.
    /// </summary>
    public (int Offset, int Length)? ScanRegion { get; set; }

    /// <summary>
    ///  Upstream's <c>BeginOffset</c>: where the search starts.
    /// </summary>
    public int BeginOffset => ScanRegion?.Offset ?? 0;

    /// <summary>
    ///  Upstream's <c>EndOffset</c>: where the search ends.
    /// </summary>
    public int EndOffset => ScanRegion is { } region ? region.Offset + region.Length : text.Length;

    /// <summary>
    ///  Upstream's <c>FindNext</c>: the occurrence after (or, searching backward, before) <paramref name="beginAtOffset"/>,
    ///  else the first one from the other end; <paramref name="loopedAround"/> tells which. Null when there is none.
    /// </summary>
    public (int Offset, int Length)? FindNext(int beginAtOffset, bool searchBackward, out bool loopedAround)
    {
        loopedAround = false;
        if (LookFor.Length == 0)
        {
            return null;
        }

        int startAt = BeginOffset;
        int endAt = EndOffset;
        int current = Math.Clamp(beginAtOffset, startAt, endAt);
        _lookFor2 = MatchCase ? LookFor : LookFor.ToUpperInvariant();
        (int, int)? result;
        if (searchBackward)
        {
            result = FindNextIn(startAt, current, searchBackward: true);
            if (result is null)
            {
                loopedAround = true;
                result = FindNextIn(current, endAt, searchBackward: true);
            }
        }
        else
        {
            result = FindNextIn(current, endAt, searchBackward: false);
            if (result is null)
            {
                loopedAround = true;
                result = FindNextIn(startAt, current, searchBackward: false);
            }
        }

        return result;
    }

    /// <summary>
    ///  Every occurrence in the text or the scan region, in order (upstream's "Find &amp; highlight all").
    /// </summary>
    public IReadOnlyList<(int Offset, int Length)> FindAll()
    {
        List<(int Offset, int Length)> found = [];
        int offset = BeginOffset;
        while (FindNext(offset, searchBackward: false, out bool looped) is { } range && !looped)
        {
            found.Add(range);
            offset = range.Offset + range.Length;
        }

        return found;
    }

    private static bool MatchFirstChar(char a, char b, bool matchCase)
        => a == b || (!matchCase && a == char.ToUpperInvariant(b));

    private (int, int)? FindNextIn(int offset1, int offset2, bool searchBackward)
    {
        offset2 -= LookFor.Length;
        char first = _lookFor2[0];
        if (searchBackward)
        {
            for (int offset = offset2; offset >= offset1; offset--)
            {
                if (IsMatch(offset))
                {
                    return (offset, LookFor.Length);
                }
            }
        }
        else
        {
            for (int offset = offset1; offset <= offset2; offset++)
            {
                if (IsMatch(offset))
                {
                    return (offset, LookFor.Length);
                }
            }
        }

        return null;

        bool IsMatch(int offset)
            => MatchFirstChar(first, text[offset], MatchCase)
               && (IsWholeWordMatch(offset) || (!MatchWholeWordOnly && IsPartWordMatch(offset)));
    }

    private bool IsWholeWordMatch(int offset)
        => IsWordBoundary(offset) && IsWordBoundary(offset + LookFor.Length) && IsPartWordMatch(offset);

    private bool IsWordBoundary(int offset)
        => offset <= 0 || offset >= text.Length || !IsAlphaNumeric(offset - 1) || !IsAlphaNumeric(offset);

    private bool IsAlphaNumeric(int offset) => char.IsLetterOrDigit(text[offset]) || text[offset] == '_';

    private bool IsPartWordMatch(int offset)
    {
        string part = text.Substring(offset, LookFor.Length);
        return (MatchCase ? part : part.ToUpperInvariant()) == _lookFor2;
    }
}

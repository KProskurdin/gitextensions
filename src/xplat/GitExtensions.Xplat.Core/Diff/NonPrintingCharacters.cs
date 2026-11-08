namespace GitExtensions.Xplat.Core.Diff;

/// <summary>
///  Upstream's "Show nonprinting characters": its text editor's markers for spaces, tabs and line ends
///  (<c>ShowSpaces</c>, <c>ShowTabs</c>, <c>ShowEOLMarkers</c>).
/// </summary>
public static class NonPrintingCharacters
{
    public const char Space = '·';
    public const char Tab = '»';
    public const char EndOfLine = '¶';

    // Upstream's text editor (TextView.DrawEOLMarker): the glyph, or the text, of each line ending.
    private const string CarriageReturnLineFeedGlyph = "¤";
    private const string LineFeedText = @"\n";
    private const string CarriageReturnLineFeedText = @"\r\n";

    /// <summary>
    ///  Upstream's <c>EolMarkerStyle</c>: the mark of a line's end as a glyph (<c>ShowEolMarkerAsGlyph</c>) or as text;
    ///  nothing for a last line without a newline.
    /// </summary>
    public static string EndOfLineMark(LineEnding ending, bool asGlyph) => ending switch
    {
        LineEnding.LineFeed => asGlyph ? EndOfLine.ToString() : LineFeedText,
        LineEnding.CarriageReturnLineFeed => asGlyph ? CarriageReturnLineFeedGlyph : CarriageReturnLineFeedText,
        _ => "",
    };

    /// <summary>
    ///  <paramref name="text"/> with a marker for each space and before each tab, which keeps its width.
    /// </summary>
    public static string Mark(string text) => text.Replace(' ', Space).Replace("\t", $"{Tab}\t");
}

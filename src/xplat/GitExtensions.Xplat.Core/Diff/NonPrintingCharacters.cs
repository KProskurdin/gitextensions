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

    /// <summary>
    ///  <paramref name="text"/> with a marker for each space and before each tab, which keeps its width.
    /// </summary>
    public static string Mark(string text) => text.Replace(' ', Space).Replace("\t", $"{Tab}\t");
}

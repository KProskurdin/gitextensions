using System.Globalization;
using GitCommands.Logging;

namespace GitExtensions.Xplat.Core.Repository;

/// <summary>
///  Upstream's <c>FormGitCommandLog</c> without its window: which row stays selected when a list refills, the detail of a
///  cached command, and the file "Save to file" writes.
/// </summary>
public static class CommandLogDialog
{
    // Upstream's mnuSaveToFile_Click filter and extensions.
    public const string TextFiles = "Text files (*.txt)";
    public const string CsvFiles = "CSV files";
    public const string AllFiles = "All files *.*";
    public const string TextExtension = "txt";

    // Upstream's separator line between a cached command, its output and its error.
    private const string Separator = "\n-------------------------------------\n\n";

    private const string CsvExtension = "csv";
    private const string TabSeparator = "\t";

    /// <summary>
    ///  Upstream's <c>RefreshListBox</c>: the row to select after a list of <paramref name="oldCount"/> rows with
    ///  <paramref name="oldSelected"/> selected refills with <paramref name="newCount"/> rows. The last row stays selected
    ///  (so the newest command is followed) when it was selected or the list was empty; else the same index, which
    ///  falls back to the last row when the list became shorter. -1 for an empty list.
    /// </summary>
    public static int SelectionAfterRefresh(int oldCount, int oldSelected, int newCount)
    {
        if (newCount < 1)
        {
            return -1;
        }

        bool lastWasSelected = oldCount == 0 || oldSelected == oldCount - 1;
        return lastWasSelected || oldSelected < 0 || oldSelected >= newCount ? newCount - 1 : oldSelected;
    }

    /// <summary>
    ///  Upstream's <c>CacheItem.DisplayString</c>: a cached command's arguments without git's <c>-c</c> configuration.
    /// </summary>
    public static string CacheDisplay(string command) => CommandLogEntry.GetGitArgumentsWithoutConfiguration(command);

    /// <summary>
    ///  Upstream's <c>CommandCacheItems_SelectedIndexChanged</c>: the command, its output and its error with the
    ///  invisible characters shown.
    /// </summary>
    public static string CacheDetail(string command, string? output, string? error)
        => command + Separator + PrintableChars(output) + Separator + PrintableChars(error);

    /// <summary>
    ///  Upstream's <c>PrintableChars</c>: NUL, CR and escape as their C escapes, a line feed as <c>\n</c> before the line
    ///  break, a tab as », a space as ·.
    /// </summary>
    public static string? PrintableChars(string? text)
        => text?.Replace("\0", @"\0").Replace("\r", @"\r").Replace("\n", "\\n\n").Replace("\t", "\u00bb")
            .Replace(" ", "\u00b7").Replace("\u001b", @"\x1b");

    /// <summary>
    ///  Upstream's <c>mnuSaveToFile_Click</c>: one line per command (<see cref="CommandLogEntry.FullLine"/>), with the
    ///  culture's list separator for a .csv file and a tab otherwise.
    /// </summary>
    public static IEnumerable<string> SaveLines(string fileName, IEnumerable<CommandLogEntry> commands)
    {
        string separator = fileName.EndsWith(CsvExtension, StringComparison.Ordinal)
            ? CultureInfo.CurrentCulture.TextInfo.ListSeparator
            : TabSeparator;
        return commands.Select(entry => entry.FullLine(separator));
    }
}

using System.Globalization;
using GitCommands;

namespace GitExtensions.Xplat.Core.Scripts;

/// <summary>
///  The options a file list adds for a script, as upstream's <c>ScriptOptionsProvider</c>: the selected files' paths
///  relative to the repository, and the line and column of the diff shown for them.
/// </summary>
public static class ScriptFileOptions
{
    public const string SelectedRelativePaths = "SelectedRelativePaths";
    public const string LineNumber = "LineNumber";
    public const string ColumnNumber = "ColumnNumber";

    /// <summary>
    ///  The values for <paramref name="paths"/>, escaped for the command line as upstream escapes them; a line or column
    ///  that is not known gives no value.
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> For(IEnumerable<string> paths, int? line = null,
        int? column = null)
        => new Dictionary<string, IReadOnlyList<string>>
        {
            [SelectedRelativePaths] = [.. paths.Select(path => path.EscapeForCommandLine())],
            [LineNumber] = line is { } number ? [number.ToString(CultureInfo.InvariantCulture)] : [],
            [ColumnNumber] = column is { } columnNumber ? [columnNumber.ToString(CultureInfo.InvariantCulture)] : [],
        };
}

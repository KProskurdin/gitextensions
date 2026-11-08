using System.Text;
using GitCommands;
using GitCommands.DiffMergeTools;
using GitExtensions.Extensibility.Settings;

namespace GitExtensions.Xplat.Core.Settings;

/// <summary>
///  A tool's path and command as upstream's Git config page shows them.
/// </summary>
public sealed record DiffMergeToolRow(string Path, string Command);

/// <summary>
///  The path and command rows of upstream's <c>GitConfigSettingsPage</c>, through upstream's
///  <see cref="DiffMergeToolConfigurationManager"/> for what it knows of a tool, which already knows each OS's file name.
/// </summary>
public static class DiffMergeToolSettings
{
    private const string DiffToolPrefix = "difftool";
    private const string MergeToolPrefix = "mergetool";
    private const string PathSuffix = "path";
    private const string CommandSuffix = "cmd";

    // Upstream's defaults for a tool only need the file system, so no settings are given.
    private static readonly DiffMergeToolConfigurationManager _tools = new(() => null);

    /// <summary>
    ///  Gets the git config key of the tool's path.
    /// </summary>
    public static string PathKey(bool diff, string tool) => $"{Prefix(diff)}.{tool}.{PathSuffix}";

    /// <summary>
    ///  Gets the git config key of the tool's command.
    /// </summary>
    public static string CommandKey(bool diff, string tool) => $"{Prefix(diff)}.{tool}.{CommandSuffix}";

    /// <summary>
    ///  Whether upstream knows the tool's command, as only then upstream suggests one while the path is typed.
    /// </summary>
    public static bool IsRegistered(string tool, bool diff) => RegisteredDiffMergeTools.All(ToolType(diff)).Contains(tool);

    /// <summary>
    ///  Upstream's <c>GetToolPath</c> and <c>GetToolCommand</c>: the stored value at <paramref name="scope"/>, else what
    ///  upstream finds for the tool. An empty tool has neither.
    /// </summary>
    public static async Task<DiffMergeToolRow> ReadAsync(IGitConfigService config, ConfigScope scope, string tool, bool diff,
        string? repositoryPath)
    {
        if (string.IsNullOrWhiteSpace(tool))
        {
            return new DiffMergeToolRow("", "");
        }

        string path = await config.GetAsync(scope, PathKey(diff, tool), repositoryPath);
        string command = await config.GetAsync(scope, CommandKey(diff, tool), repositoryPath);
        if (path.Length > 0 && command.Length > 0)
        {
            return new DiffMergeToolRow(path, command);
        }

        DiffMergeToolConfiguration found = await Task.Run(() => _tools.LoadDiffMergeToolConfig(tool, null));
        return new DiffMergeToolRow(path.Length > 0 ? path : found.Path, command.Length > 0 ? command : FullCommand(found, diff));
    }

    /// <summary>
    ///  Upstream's <c>SuggestDiffToolCommand</c> and <c>SuggestMergeToolCommand</c>, for the path on the page.
    /// </summary>
    public static string SuggestCommand(string tool, bool diff, string? path)
        => string.IsNullOrWhiteSpace(tool) ? "" : FullCommand(_tools.LoadDiffMergeToolConfig(tool, path), diff);

    /// <summary>
    ///  A path or command in the form upstream writes to git config.
    /// </summary>
    public static string ToGitSetting(string value) => value.ConvertPathToGitSetting() ?? "";

    private static string FullCommand(DiffMergeToolConfiguration tool, bool diff) => diff ? tool.FullDiffCommand : tool.FullMergeCommand;

    private static string Prefix(bool diff) => diff ? DiffToolPrefix : MergeToolPrefix;

    private static DiffMergeToolType ToolType(bool diff) => diff ? DiffMergeToolType.Diff : DiffMergeToolType.Merge;
}

/// <summary>
///  Upstream's <c>FormAvailableEncodings</c> without its window.
/// </summary>
public static class AvailableEncodingsDialog
{
    /// <summary>
    ///  Upstream's <c>LoadEncoding</c>: the encodings that can be added, each once and without UTF-8's byte order mark, and
    ///  never UTF-7, which .NET no longer supports.
    /// </summary>
    public static IReadOnlyList<Encoding> Selectable(IReadOnlyDictionary<string, Encoding> included)
    {
#pragma warning disable SYSLIB0001 // Type or member is obsolete
        Encoding utf7 = Encoding.UTF7;
#pragma warning restore SYSLIB0001 // Type or member is obsolete
        return
        [
            .. Encoding.GetEncodings()
                .Select(info => info.GetEncoding())
                .Select(encoding => encoding.GetType() == typeof(UTF8Encoding) ? new UTF8Encoding(encoderShouldEmitUTF8Identifier: false) : encoding)
                .Where(encoding => encoding != utf7)
                .Where(encoding => !included.ContainsKey(encoding.WebName))
                .GroupBy(encoding => encoding.WebName)
                .Select(group => group.First())
                .OrderBy(encoding => encoding.EncodingName, StringComparer.CurrentCulture)
        ];
    }

    /// <summary>
    ///  Upstream's <c>ListIncludedEncodings_SelectedValueChanged</c>: the default encodings stay. Upstream compares exact
    ///  types, which today's <see cref="Encoding.ASCII"/> and <see cref="Encoding.UTF8"/> do not have, as they are sealed
    ///  subclasses; what it means is these kinds.
    /// </summary>
    public static bool CanRemove(Encoding? encoding)
        => encoding is not (null or ASCIIEncoding or UnicodeEncoding or UTF8Encoding or UTF7Encoding) && encoding != Encoding.Default;

    /// <summary>
    ///  Upstream's <c>ButtonOk_Click</c>.
    /// </summary>
    public static void Apply(IDictionary<string, Encoding> available, IEnumerable<Encoding> included)
    {
        available.Clear();
        foreach (Encoding encoding in included)
        {
            available[encoding.WebName] = encoding;
        }
    }
}

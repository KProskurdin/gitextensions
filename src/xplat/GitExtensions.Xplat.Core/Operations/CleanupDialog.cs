using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  Upstream's <c>FormCleanupRepository</c> without its window: the <c>git clean</c> commands its choices make, and the
///  paths its "Add a path..." buttons accept.
/// </summary>
public static class CleanupDialog
{
    // Upstream's texts.
    public const string ReallyCleanupQuestion = "Are you sure you want to cleanup the repository?";
    public const string ReallyCleanupQuestionCaption = "Cleanup";

    /// <summary>
    ///  Upstream's <c>GetInclusivePathArgumentFromGui</c>: the non-empty lines, quoted, or null when the filter is off.
    /// </summary>
    public static string? IncludePaths(bool enabled, string? lines)
        => enabled ? string.Join(" ", Lines(lines).Select(path => $"\"{path}\"")) : null;

    /// <summary>
    ///  Upstream's <c>GetExclusivePathArgumentFromGui</c>: an <c>--exclude</c> per non-empty line, spaces as <c>?</c>, in
    ///  POSIX form, or null when the filter is off.
    /// </summary>
    public static string? ExcludePaths(bool enabled, string? lines)
        => enabled
            ? string.Join(" ", Lines(lines).Select(path => $"--exclude={path.Replace(" ", "?")}".ToPosixPath()))
            : null;

    /// <summary>
    ///  Upstream's <c>CleanUp</c>: <c>git clean</c>, then, when asked, the same in every submodule.
    /// </summary>
    public static IReadOnlyList<ArgumentString> Commands(CleanMode mode, bool dryRun, bool directories, bool submodules,
        string? includePaths, string? excludePaths)
    {
        List<ArgumentString> commands =
            [GitCommands.Git.Commands.Clean(mode, dryRun, directories, includePaths, excludePaths)];
        if (submodules)
        {
            commands.Add(GitCommands.Git.Commands.CleanSubmodules(mode, dryRun, directories, includePaths));
        }

        return commands;
    }

    /// <summary>
    ///  Upstream's <c>RequestUserFolderPath</c>: a folder inside the work tree that exists and is not the .git folder.
    /// </summary>
    public static bool AcceptsIncludePath(string workingDir, string gitDir, string? folder)
        => folder is not null
           && folder.StartsWith(workingDir, StringComparison.Ordinal)
           && Directory.Exists(folder)
           && !folder.Equals(PathUtil.RemoveTrailingPathSeparator(gitDir), StringComparison.Ordinal);

    /// <summary>
    ///  Upstream's <c>AddExcludePath_Click</c>: a file inside the work tree, relative to it; null for another file.
    /// </summary>
    public static string? ExcludePath(string workingDir, string? file)
        => file is not null && file.StartsWith(workingDir, StringComparison.Ordinal)
            ? file.Replace(workingDir, "")
            : null;

    /// <summary>
    ///  Upstream's way to add a path to a box: on a line of its own after the others.
    /// </summary>
    public static string AddLine(string? text, string path)
        => string.IsNullOrEmpty(text) ? path : text + Environment.NewLine + path;

    private static IEnumerable<string> Lines(string? text)
        => (text ?? "").Split(["\r\n", "\n"], StringSplitOptions.None).Where(line => line.Length > 0);
}

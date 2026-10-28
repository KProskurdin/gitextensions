using GitCommands;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  Upstream's <c>FormArchive</c> without its window: the file name it suggests, the paths it archives and its
///  <c>git archive</c> command.
/// </summary>
public static class ArchiveDialog
{
    // Upstream's texts.
    public const string SaveFileDialogFilterZip = "Zip file (*.zip)";
    public const string SaveFileDialogFilterTar = "Tar file (*.tar)";
    public const string SaveFileDialogCaption = "Save archive as";
    public const string NoRevisionSelected = "You need to choose a target revision.";

    /// <summary>
    ///  Upstream's <c>filenameSuggestion</c>: the work tree's folder and the commit, and the one path when exactly one is
    ///  given (its dots as underscores).
    /// </summary>
    public static string FileNameSuggestion(string workingDir, string revision, bool pathFilter, string? paths)
    {
        string suggestion = $"{new DirectoryInfo(workingDir).Name}_{revision}";
        string[] lines = Lines(paths);
        return pathFilter && lines is [{ } path] && !string.IsNullOrWhiteSpace(path)
            ? suggestion + "_" + path.Trim().Replace(".", "_")
            : suggestion;
    }

    /// <summary>
    ///  Upstream's <c>GetPathArgumentFromGui</c> for the path filter: every line quoted, spaces between.
    /// </summary>
    public static string PathArgument(string? paths) => string.Join(" ", Lines(paths).Select(path => path.QuoteNE()));

    /// <summary>
    ///  Upstream's <c>GetPathArgumentFromGui</c> for the revision filter: the files changed and not deleted, quoted.
    /// </summary>
    public static string ChangedFilesArgument(IEnumerable<string> files) => string.Join(" ", files.Select(file => file.QuoteNE()));

    /// <summary>
    ///  Upstream's <c>Save_Click</c> command.
    /// </summary>
    public static string Arguments(bool zip, string revision, string output, string pathArgument)
        => string.Format(@"archive --format=""{0}"" {1} --output ""{2}"" {3}", zip ? "zip" : "tar", revision, output,
            pathArgument);

    private static string[] Lines(string? text) => (text ?? "").Split(["\r\n", "\n"], StringSplitOptions.None);
}

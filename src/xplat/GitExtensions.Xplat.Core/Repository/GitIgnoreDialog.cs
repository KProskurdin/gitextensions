using System.Text;
using GitCommands;

namespace GitExtensions.Xplat.Core.Repository;

/// <summary>
///  Upstream's texts of <c>FormGitIgnore</c> for one of its two files: <c>GitIgnoreModel</c> (.gitignore) or
///  <c>GitLocalExcludeModel</c> (.git/info/exclude).
/// </summary>
/// <param name="Caption">The window's title.</param>
/// <param name="OnlyInWorkingDir">The error for a bare repository.</param>
/// <param name="CannotAccess">The error when the file could not be saved.</param>
/// <param name="CannotAccessCaption">That error's caption.</param>
/// <param name="SaveQuestion">The question when the window closes with unsaved changes.</param>
public sealed record GitIgnoreTexts(
    string Caption,
    string OnlyInWorkingDir,
    string CannotAccess,
    string CannotAccessCaption,
    string SaveQuestion);

/// <summary>
///  Upstream's <c>FormGitIgnore</c> and <c>FormAddToGitIgnore</c> without their windows: their texts, the default ignore
///  patterns, saving the edited file, appending patterns to it, and the files a set of patterns would ignore.
/// </summary>
public static class GitIgnoreDialog
{
    // Upstream's texts.
    public const string NoWorkingDirCaption = "No working directory";
    public const string SaveQuestionCaption = "Save changes?";
    public const string AddToLocalExcludeTitle = "Add file(s) to .git/info/exclude";
    public const string MatchingFiles = "{0} file(s) matched";
    public const string Updating = "Updating ...";
    public const string HelpIntroduction = "Specify filepatterns you want git to ignore.";
    public const string HelpExample = "Example:";

    // Upstream's lnkGitIgnorePatterns and lnkGitIgnoreGenerate.
    public const string PatternsUrl = "https://github.com/github/gitignore";
    public const string GenerateUrl = "https://www.gitignore.io/";

    /// <summary>
    ///  The pattern upstream's "Add pattern" button proposes.
    /// </summary>
    public const string ProposedPattern = "*.dll";

    /// <summary>
    ///  How long upstream's preview waits after the last change of the patterns before it asks git.
    /// </summary>
    public static readonly TimeSpan PreviewDelay = TimeSpan.FromMilliseconds(300);

    /// <summary>
    ///  Upstream's <c>DefaultIgnorePatterns</c>, used when the user has no <see cref="DefaultIgnorePatternsFile"/>.
    /// </summary>
    public static IReadOnlyList<string> DefaultIgnorePatterns { get; } =
    [
        "#Ignore thumbnails created by Windows",
        "Thumbs.db",
        "#Ignore files built by Visual Studio",
        "*.obj",
        "*.exe",
        "*.pdb",
        "*.user",
        "*.aps",
        "*.pch",
        "*.vspscc",
        "*_i.c",
        "*_p.c",
        "*.ncb",
        "*.suo",
        "*.tlb",
        "*.tlh",
        "*.bak",
        "*.cache",
        "*.ilk",
        "*.log",
        "[Bb]in",
        "[Dd]ebug*/",
        "*.lib",
        "*.sbr",
        "obj/",
        "[Rr]elease*/",
        "_ReSharper*/",
        "[Tt]est[Rr]esult*",
        ".vs/",
        ".idea/",
        "#Nuget packages folder",
        "packages/"
    ];

    /// <summary>
    ///  Upstream's <c>DefaultIgnorePatternsFile</c>: the user's own default patterns, one per line, in the application
    ///  data folder.
    /// </summary>
    public static string DefaultIgnorePatternsFile { get; } =
        Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "GitExtensions/DefaultIgnorePatterns.txt");

    /// <summary>
    ///  Upstream's <c>label1</c> beside the editor: the introduction and the built-in default patterns as the example.
    /// </summary>
    public static string HelpText { get; } =
        $"{HelpIntroduction}\n\n{HelpExample}\n{string.Join('\n', DefaultIgnorePatterns)}";

    /// <summary>
    ///  Upstream's texts for the file <paramref name="localExclude"/> chooses.
    /// </summary>
    public static GitIgnoreTexts TextsFor(bool localExclude)
        => localExclude
            ? new GitIgnoreTexts(
                "Edit .git/info/exclude",
                ".git/info/exclude is only supported when there is a working directory.",
                "Failed to save .git/info/exclude." + Environment.NewLine + "Check if file is accessible.",
                "Failed to save .git/info/exclude",
                "Save changes to .git/info/exclude?")
            : new GitIgnoreTexts(
                "Edit .gitignore",
                ".gitignore is only supported when there is a working directory.",
                "Failed to save .gitignore." + Environment.NewLine + "Check if file is accessible.",
                "Failed to save .gitignore",
                "Save changes to .gitignore?");

    /// <summary>
    ///  Upstream's <c>FormGitIgnoreLoad</c> check: the file needs a working directory.
    /// </summary>
    public static bool IsBare(string repositoryPath) => GitModules.Open(repositoryPath).IsBareRepository();

    /// <summary>
    ///  The file's text as upstream's editor shows it, or null when the file does not exist yet (upstream's editor stays
    ///  empty then).
    /// </summary>
    public static async Task<string?> ReadAsync(string path)
        => File.Exists(path) ? await File.ReadAllTextAsync(path, GitModule.SystemEncoding) : null;

    /// <summary>
    ///  The line ending of <paramref name="text"/>: its first one, else the platform's, which upstream uses throughout.
    /// </summary>
    public static string NewLineOf(string text)
    {
        int index = text.IndexOf('\n');
        return index < 0 ? Environment.NewLine : index > 0 && text[index - 1] == '\r' ? "\r\n" : "\n";
    }

    /// <summary>
    ///  Upstream's <c>AddDefaultClick</c>: the default patterns the text does not have yet, after a blank line, each on a
    ///  line of its own; null when it has them all. Upstream's own <see cref="DefaultIgnorePatternsFile"/> is read when it
    ///  exists.
    /// </summary>
    public static string? AddDefaults(string text, IReadOnlyList<string> defaults)
    {
        string newLine = NewLineOf(text);
        HashSet<string> lines = new(text.Split('\n').Select(line => line.TrimEnd('\r')), StringComparer.Ordinal);
        string[] patternsToAdd = [.. defaults.Except(lines, StringComparer.Ordinal)];
        return patternsToAdd.Length == 0
            ? null
            : $"{text}{newLine}{string.Join(newLine, patternsToAdd)}{newLine}";
    }

    /// <summary>
    ///  The default patterns: the user's <see cref="DefaultIgnorePatternsFile"/> when there is one, else upstream's.
    /// </summary>
    public static async Task<IReadOnlyList<string>> DefaultPatternsAsync()
        => File.Exists(DefaultIgnorePatternsFile)
            ? await File.ReadAllLinesAsync(DefaultIgnorePatternsFile)
            : DefaultIgnorePatterns;

    /// <summary>
    ///  Upstream's <c>SaveGitIgnore</c>: the text ended with a line ending, written in git's encoding, with a read-only file
    ///  made writable for the write. Returns the text as written, which is the window's new saved state.
    /// </summary>
    public static string Save(string path, string text)
    {
        string newLine = NewLineOf(text);
        string content = text.EndsWith(newLine, StringComparison.Ordinal) ? text : text + newLine;
        FileInfoExtensions.MakeFileTemporaryWritable(path, file =>
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllBytes(file, GitModule.SystemEncoding.GetBytes(content));
        });
        return content;
    }

    /// <summary>
    ///  Upstream's <c>GetCurrentPatterns</c>: the box's non-empty lines.
    /// </summary>
    public static IReadOnlyList<string> PatternsOf(string text)
        => [.. text.Split('\n').Select(line => line.TrimEnd('\r')).Where(line => line.Length > 0)];

    /// <summary>
    ///  Upstream's <c>AddToIgnoreClick</c>: <paramref name="patterns"/> appended to the file, each on a line of its own, after
    ///  a line ending when the file does not end with one. The file's own line ending is used (upstream: the platform's,
    ///  which adds a blank line to a file with the other ending).
    /// </summary>
    public static void Append(string path, IReadOnlyList<string> patterns)
    {
        FileInfoExtensions.MakeFileTemporaryWritable(path, file =>
        {
            string existing = File.Exists(file) ? File.ReadAllText(file, GitModule.SystemEncoding) : "";
            string newLine = NewLineOf(existing);
            StringBuilder addition = new();
            if (existing.Length > 0 && !existing.EndsWith('\n'))
            {
                addition.Append(newLine);
            }

            foreach (string pattern in patterns)
            {
                addition.Append(pattern).Append(newLine);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            using StreamWriter writer = new(file, append: true, GitModule.SystemEncoding);
            writer.Write(addition);
        });
    }

    /// <summary>
    ///  Upstream's preview (<c>GitModule.GetIgnoredFiles</c>): the files, tracked or not, that <paramref name="patterns"/>
    ///  match.
    /// </summary>
    public static Task<IReadOnlyList<string>> IgnoredFilesAsync(string repositoryPath, IReadOnlyList<string> patterns,
        CancellationToken cancellationToken)
        => Task.Run(() => GitModules.Open(repositoryPath).GetIgnoredFiles(patterns), cancellationToken);
}

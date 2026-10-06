using System.Text;
using GitCommands;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  The command git runs when it needs an editor (the rebase todo list, a reworded message): the app itself with upstream's
///  <c>fileeditor</c> verb, as upstream's <c>AppSettings.FileEditorCommand</c>. Upstream writes it into the global
///  <c>core.editor</c>; the new shell passes it to the single git call instead (<see cref="Environment"/>), so the user's
///  git config is not changed.
/// </summary>
public static class GitEditorCommand
{
    /// <summary>
    ///  The command-line verb, the same as upstream's, so the app opens the editor window instead of the browse window.
    /// </summary>
    public const string Verb = "fileeditor";

    private const string DotnetHost = "dotnet";

    /// <summary>
    ///  Builds the command for the running app. <paramref name="processPath"/> is the executable that runs (the app host, or
    ///  <c>dotnet</c> when the app was started as <c>dotnet GitExtensions.dll</c>, then <paramref name="entryAssemblyPath"/>
    ///  is passed to it). git runs the command through its shell and appends the file name, so each part is single-quoted and
    ///  written with forward slashes, which git's shell also accepts on Windows.
    /// </summary>
    public static string Build(string processPath, string? entryAssemblyPath)
    {
        StringBuilder command = new(Quote(processPath));
        if (entryAssemblyPath is not null
            && string.Equals(Path.GetFileNameWithoutExtension(processPath), DotnetHost, StringComparison.OrdinalIgnoreCase))
        {
            command.Append(' ').Append(Quote(entryAssemblyPath));
        }

        return command.Append(' ').Append(Verb).ToString();
    }

    /// <summary>
    ///  The variables that make git use <paramref name="editorCommand"/> for the todo list and for commit messages. They
    ///  take precedence over <c>sequence.editor</c> and <c>core.editor</c> in the user's config, whose editor could be a
    ///  terminal program that never appears, because git runs here without a terminal.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Environment(string editorCommand)
        => new Dictionary<string, string>
        {
            ["GIT_SEQUENCE_EDITOR"] = editorCommand,
            ["GIT_EDITOR"] = editorCommand,
        };

    // A single quote cannot appear inside single quotes in a POSIX shell, so it closes the quote, adds an escaped one and
    // opens a new quote.
    private static string Quote(string path) => $"'{path.ToPosixPath().Replace("'", @"'\''")}'";
}

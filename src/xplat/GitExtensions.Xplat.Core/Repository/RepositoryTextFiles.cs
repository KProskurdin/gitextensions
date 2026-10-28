using GitCommands;

namespace GitExtensions.Xplat.Core.Repository;

/// <summary>
///  A file of the work tree that one of upstream's simple editors edits: its name, the upstream form (for its
///  translations), the window's title and upstream's help beside the text.
/// </summary>
/// <param name="NotifiesRepositoryChanged">Upstream's FormMailMap tells the browse window the repository changed after a
///  save, as the authors the grid shows may change; FormGitAttributes does not.</param>
public sealed record RepositoryTextFile(string FileName, string Form, string Title, string Help,
    bool NotifiesRepositoryChanged);

/// <summary>
///  Upstream's <c>FormGitAttributes</c> and <c>FormMailMap</c> without their windows: the two files, and upstream's way to
///  read and write them.
/// </summary>
public static class RepositoryTextFiles
{
    // Upstream's texts, the same in both forms but for the file's name.
    public const string CannotSave = "Failed to save {0}.";
    public const string CheckAccessible = "Check if file is accessible.";
    public const string CannotSaveCaption = "Failed to save {0}";
    public const string SaveQuestion = "Save changes to {0}?";
    public const string SaveQuestionCaption = "Save changes?";

    public static RepositoryTextFile GitAttributes { get; } = new(".gitattributes", "FormGitAttributes",
        "Edit .gitattributes",
        "Edit the git attributes\nDefine attributes per path\n\nExamples\nMark all jpg files as binary:\n*.jpg binary\n\n"
        + "Mark sln files as binary:\n*.sln binary\n\nMark single file as text:\nweirdchars.txt text\n\n"
        + "For more information run\ncommand \"git help gitattributes\"",
        NotifiesRepositoryChanged: false);

    public static RepositoryTextFile MailMap { get; } = new(".mailmap", "FormMailMap", "Edit .mailmap",
        "Edit the mailmap.\nThis file is meant to correct usernames.\n\nExample:\nHenk Westhuis <Henk@.(none)>\n"
        + "Henk Westhuis <henk_westhuis@hotmail.com>\n\nFor more information run\ncommand \"git help shortlog\"",
        NotifiesRepositoryChanged: true);

    /// <summary>
    ///  Upstream's <c>LoadFile</c>: the file's text, empty when there is none.
    /// </summary>
    public static async Task<string> ReadAsync(string repositoryPath, RepositoryTextFile file)
    {
        string path = Path.Combine(repositoryPath, file.FileName);
        return File.Exists(path) ? await File.ReadAllTextAsync(path) : "";
    }

    /// <summary>
    ///  Upstream's <c>SaveFile</c>: the text ending with a line break, in git's encoding, written even to a read-only file.
    /// </summary>
    public static Task WriteAsync(string repositoryPath, RepositoryTextFile file, string text)
        => Task.Run(() =>
        {
            string content = text.EndsWith(Environment.NewLine, StringComparison.Ordinal)
                ? text
                : text + Environment.NewLine;
            FileInfoExtensions.MakeFileTemporaryWritable(Path.Combine(repositoryPath, file.FileName),
                path => File.WriteAllBytes(path, GitModule.SystemEncoding.GetBytes(content)));
        });
}

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtUtils;

namespace GitExtensions.Xplat.Core.Diff;

/// <summary>
///  Where a file is read from: a commit, the index, or the working directory.
/// </summary>
public sealed record FileRevision(string? Hash, bool Staged)
{
    public bool IsWorkTree => Hash is null && !Staged;

    /// <summary>
    ///  Git's name of the file in this revision; null for the working directory.
    /// </summary>
    public string? BlobName(string path) => IsWorkTree ? null : $"{Hash}:{path}";

    public static FileRevision Of(ObjectId id)
        => id == ObjectId.WorkTreeId ? new FileRevision(null, false)
            : id == ObjectId.IndexId ? new FileRevision(null, true)
            : new FileRevision(id.ToString(), false);

    /// <summary>
    ///  A commit's hash or name, which may be an artificial row's; without one the index or the working directory.
    /// </summary>
    public static FileRevision Of(string? hash, bool staged = false)
        => hash is null ? new FileRevision(null, staged)
            : ObjectId.TryParse(hash, out ObjectId id) ? Of(id)
            : new FileRevision(hash, false);
}

/// <summary>
///  A file as upstream's <c>FileViewer.ViewItemAsync</c> shows it: exactly one member is set.
/// </summary>
public sealed record ViewedFile
{
    /// <summary>
    ///  The bytes of a file whose name says it is an image, still to be decoded.
    /// </summary>
    public byte[]? Image { get; init; }

    /// <summary>
    ///  A text file's content.
    /// </summary>
    public string? Text { get; init; }

    /// <summary>
    ///  A binary file's bytes, one character per byte (<see cref="FileContent.HexDump"/>).
    /// </summary>
    public string? BinaryData { get; init; }

    /// <summary>
    ///  Why the file could not be read; a file the revision does not have.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    ///  The length of a file that was not shown, as upstream's <c>ShowOrDeferAsync</c>, until the user asks for it.
    /// </summary>
    public long? TooLarge { get; init; }
}

/// <summary>
///  Upstream <c>FileViewer</c>'s reading of a whole file: a picture, a text, or a hex dump of a binary file.
/// </summary>
public static partial class FileContent
{
    public const string CannotViewImage = "Cannot view image {0}";
    public const string BinaryFile = "Binary file: {0}";
    public const string FileSizeInMb = "MB";
    public const string Bytes = "bytes";
    public const string LargeFileSizeWarning = "This file is {0:N1} MB. Showing large files can be slow. Click to show anyway.";

    // Upstream's ShowOrDeferAsync.
    private const long MaxShownLength = 5 * 1024 * 1024;
    private const double BytesPerMb = 1024d * 1024;
    private const double MinShownMb = 0.1;

    // Upstream's ToHexDump: only the file's start is shown, as a dedicated editor is for the complete file.
    private const int ColumnWidth = 8;
    private const int ColumnCount = 2;
    private const int MaxDumpedLines = 256;
    private const string TruncatedText = "[Truncated]";

    [GeneratedRegex(@"\x1B\[[0-9;]*[A-Za-z]")]
    private static partial Regex AnsiEscapeRegex { get; }

    /// <summary>
    ///  Reads the file of <paramref name="revision"/> for the viewer.
    /// </summary>
    /// <param name="showLarge">Read even a file too large to show without asking.</param>
    public static Task<ViewedFile> ReadAsync(string repositoryPath, FileRevision revision, string path, DiffOptions options,
        bool showLarge = false, CancellationToken cancellationToken = default)
        => Task.Run<ViewedFile>(async () =>
        {
            GitModule module = new(new GitExecutorProvider(new GitDirectoryResolver()), repositoryPath);
            bool isImage = FileHelper.IsImage(path);
            byte[] bytes;
            if (revision.BlobName(path) is { } blob)
            {
                ExecutionResult exists = module.GitExecutable.Execute(
                    new GitArgumentBuilder("cat-file") { "-e", blob.Quote() }, throwOnErrorExit: false);
                if (!exists.ExitedSuccessfully)
                {
                    // Upstream's ViewItemAsync: a tree's entry for a submodule is a commit, which is in the submodule.
                    return revision.Hash is { } hash && SubmoduleCommit(module, hash, path) is { } commit
                        ? new ViewedFile { Text = SubmoduleDiff.EntryText(module, path, commit) }
                        : new ViewedFile { Error = exists.StandardError.Trim() };
                }

                // Upstream's GetFileStreamAsync, which also reads a file git-lfs keeps.
                using MemoryStream? stream = await module.GetFileStreamAsync(blob.Quote(), cancellationToken);
                if (stream is null)
                {
                    return new ViewedFile { Error = blob };
                }

                bytes = stream.ToArray();
                if (!showLarge && !isImage && bytes.Length > MaxShownLength)
                {
                    return new ViewedFile { TooLarge = bytes.Length };
                }
            }
            else
            {
                string fullPath = Path.Combine(module.WorkingDir, path);
                if (Directory.Exists(fullPath))
                {
                    // Upstream's ViewFileAsync.
                    return new ViewedFile
                    {
                        Text = GitModule.IsValidGitWorkingDir(fullPath)
                            ? SubmoduleDiff.EntryText(module, path, hash: "")
                            : $"Directory: {path}"
                    };
                }

                if (!File.Exists(fullPath))
                {
                    return new ViewedFile { Error = $"File {fullPath} does not exist" };
                }

                FileInfo info = new(fullPath);
                if (!showLarge && info.Length > MaxShownLength)
                {
                    return new ViewedFile { TooLarge = info.Length };
                }

                await using FileStream file = File.Open(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using MemoryStream content = new();
                await file.CopyToAsync(content, cancellationToken);
                bytes = content.ToArray();
            }

            if (isImage)
            {
                return new ViewedFile { Image = bytes };
            }

            return FileHelper.IsBinaryFileAccordingToContent(bytes) || FileHelper.IsBinaryFileName(module, path)
                ? new ViewedFile { BinaryData = BinaryData(bytes) }
                : new ViewedFile { Text = Decode(bytes, options.EncodingFor(module), revision.IsWorkTree) };
        }, cancellationToken);

    private static string? SubmoduleCommit(GitModule module, string revision, string path)
    {
        ObjectId commit = module.RevParse(revision);
        return !commit.IsZero && module.GetTree(commit, full: true, path).ToList() is [{ ObjectType: GitObjectType.Commit } entry]
            ? entry.ObjectId.ToString()
            : null;
    }

    /// <summary>
    ///  One character per byte, as <see cref="HexDump"/> takes a file's bytes.
    /// </summary>
    public static string BinaryData(byte[] bytes) => Encoding.Latin1.GetString(bytes);

    /// <summary>
    ///  Upstream's <c>DisplayAsHexDump</c> and <c>ToHexDump</c>: what the file is, its size, and the offset, bytes and
    ///  characters of its start.
    /// </summary>
    /// <param name="translate">Translates upstream's texts.</param>
    public static string HexDump(string fileNameFormat, string fileName, string data, Func<string, string> translate)
    {
        StringBuilder text = new StringBuilder()
            .AppendLine(string.Format(CultureInfo.CurrentCulture, translate(fileNameFormat), fileName))
            .AppendLine();
        double mb = data.Length / BytesPerMb;
        if (mb >= MinShownMb)
        {
            text.Append(CultureInfo.CurrentCulture, $"{mb:N1}").Append(' ').Append(translate(FileSizeInMb)).Append(" / ");
        }

        text.Append(CultureInfo.CurrentCulture, $"{data.Length:N0}").Append(' ').Append(translate(Bytes)).AppendLine(":")
            .AppendLine();
        AppendHexDump(text, data);
        return text.ToString();
    }

    private static void AppendHexDump(StringBuilder text, string data)
    {
        int limit = Math.Min(data.Length, ColumnWidth * ColumnCount * MaxDumpedLines);
        int i = 0;
        while (i < limit)
        {
            int lineStart = i;
            if (i != 0)
            {
                text.AppendLine();
            }

            text.Append(CultureInfo.InvariantCulture, $"{lineStart:X4}   ");
            for (int column = 0; column < ColumnCount; column++)
            {
                if (column != 0)
                {
                    text.Append("  ");
                }

                for (int j = 0; j < ColumnWidth; j++)
                {
                    if (j != 0)
                    {
                        text.Append(' ');
                    }

                    text.Append(i < data.Length ? ((byte)data[i]).ToString("X2", CultureInfo.InvariantCulture) : "  ");
                    i++;
                }
            }

            text.Append("   ");
            i = lineStart;
            for (int column = 0; column < ColumnCount; column++)
            {
                if (column != 0)
                {
                    text.Append(' ');
                }

                for (int j = 0; j < ColumnWidth; j++)
                {
                    text.Append(i < data.Length ? (char.IsControl(data[i]) ? '.' : data[i]) : ' ');
                    i++;
                }
            }
        }

        if (data.Length > limit)
        {
            text.AppendLine();
            text.Append(TruncatedText);
        }
    }

    // A byte order mark says the encoding, as upstream's FileReader; git's output has its escape codes removed, as upstream's
    // GetFileText does for a blob.
    private static string Decode(byte[] bytes, Encoding encoding, bool isWorkTree)
    {
        using StreamReader reader = new(new MemoryStream(bytes), encoding, detectEncodingFromByteOrderMarks: true);
        string text = reader.ReadToEnd();
        return isWorkTree ? text : AnsiEscapeRegex.Replace(text, "");
    }
}

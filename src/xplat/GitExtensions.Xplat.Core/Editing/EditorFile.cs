using System.Text;

namespace GitExtensions.Xplat.Core.Editing;

/// <summary>
///  A text file opened by the editor window (upstream <c>FormEditor</c>): the rebase todo list or a commit message that
///  git hands over, or any file given on the command line. It is written back with the line ending and byte order mark it
///  was read with, as upstream keeps the file's preamble, so git reads the same format it wrote.
/// </summary>
/// <param name="Text">The file's text, with '\n' line ends.</param>
/// <param name="LineEnding">The line ending to write.</param>
/// <param name="HasByteOrderMark">True when the file starts with its encoding's byte order mark, which is written back.</param>
/// <param name="Encoding">The encoding to write; UTF-8 when null.</param>
public sealed record EditorFile(string Text, string LineEnding, bool HasByteOrderMark, Encoding? Encoding = null)
{
    private const string CrLf = "\r\n";
    private const string Lf = "\n";

    private static readonly UTF8Encoding _withoutBom = new(encoderShouldEmitUTF8Identifier: false);
    private static readonly UTF8Encoding _withBom = new(encoderShouldEmitUTF8Identifier: true);

    /// <summary>
    ///  Reads <paramref name="path"/> as upstream's <c>FileViewer</c> does: in <paramref name="encoding"/> (upstream's
    ///  <c>FilesEncoding</c>; UTF-8 when null) unless a byte order mark says otherwise. The text uses '\n' only;
    ///  <see cref="LineEnding"/> is CRLF when the file's first line ends with one.
    /// </summary>
    public static async Task<EditorFile> ReadAsync(string path, Encoding? encoding = null)
    {
        byte[] bytes = await File.ReadAllBytesAsync(path);
        using StreamReader reader = new(new MemoryStream(bytes), encoding ?? _withoutBom, detectEncodingFromByteOrderMarks: true);
        string text = await reader.ReadToEndAsync();
        byte[] preamble = reader.CurrentEncoding.GetPreamble();
        bool hasBom = preamble.Length > 0 && bytes.AsSpan().StartsWith(preamble);
        int firstLineEnd = text.IndexOf('\n');
        string lineEnding = firstLineEnd > 0 && text[firstLineEnd - 1] == '\r' ? CrLf : Lf;
        return new EditorFile(Normalize(text), lineEnding, hasBom, reader.CurrentEncoding);
    }

    /// <summary>
    ///  Writes <paramref name="text"/> with this file's encoding, line ending and byte order mark. Line ends in
    ///  <paramref name="text"/> may be '\n' or "\r\n" (a pasted block).
    /// </summary>
    public Task WriteAsync(string path, string text)
    {
        string content = Normalize(text);
        if (LineEnding == CrLf)
        {
            content = content.Replace(Lf, CrLf);
        }

        Encoding encoding = Encoding ?? _withBom;
        byte[] preamble = HasByteOrderMark ? encoding.GetPreamble() : [];
        return File.WriteAllBytesAsync(path, [.. preamble, .. encoding.GetBytes(content)]);
    }

    private static string Normalize(string text) => text.Replace(CrLf, Lf);
}

using System.Text;

namespace GitExtensions.Xplat.Core.Editing;

/// <summary>
///  A text file opened by the editor window (upstream <c>FormEditor</c>): the rebase todo list or a commit message that
///  git hands over, or any file given on the command line. It is written back with the line ending and byte order mark it
///  was read with, as upstream keeps the file's preamble, so git reads the same format it wrote.
/// </summary>
public sealed record EditorFile(string Text, string LineEnding, bool HasByteOrderMark)
{
    private const string CrLf = "\r\n";
    private const string Lf = "\n";

    private static readonly UTF8Encoding _withoutBom = new(encoderShouldEmitUTF8Identifier: false);
    private static readonly UTF8Encoding _withBom = new(encoderShouldEmitUTF8Identifier: true);

    /// <summary>
    ///  Reads <paramref name="path"/> as UTF-8. The text uses '\n' only; <see cref="LineEnding"/> is CRLF when the file's
    ///  first line ends with one.
    /// </summary>
    public static async Task<EditorFile> ReadAsync(string path)
    {
        byte[] bytes = await File.ReadAllBytesAsync(path);
        byte[] preamble = _withBom.GetPreamble();
        bool hasBom = bytes.AsSpan().StartsWith(preamble);
        string text = _withoutBom.GetString(bytes, hasBom ? preamble.Length : 0, bytes.Length - (hasBom ? preamble.Length : 0));
        int firstLineEnd = text.IndexOf('\n');
        string lineEnding = firstLineEnd > 0 && text[firstLineEnd - 1] == '\r' ? CrLf : Lf;
        return new EditorFile(Normalize(text), lineEnding, hasBom);
    }

    /// <summary>
    ///  Writes <paramref name="text"/> with this file's line ending and byte order mark. Line ends in
    ///  <paramref name="text"/> may be '\n' or "\r\n" (a pasted block).
    /// </summary>
    public Task WriteAsync(string path, string text)
    {
        string content = Normalize(text);
        if (LineEnding == CrLf)
        {
            content = content.Replace(Lf, CrLf);
        }

        return File.WriteAllTextAsync(path, content, HasByteOrderMark ? _withBom : _withoutBom);
    }

    private static string Normalize(string text) => text.Replace(CrLf, Lf);
}

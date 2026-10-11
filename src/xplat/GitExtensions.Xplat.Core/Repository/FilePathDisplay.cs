using GitCommands;

namespace GitExtensions.Xplat.Core.Repository;

/// <summary>
///  A path in a file list, as upstream's <c>PathFormatter</c> writes it for its <c>TruncatePathMethod</c> setting.
/// </summary>
public static class FilePathDisplay
{
    /// <summary>
    ///  The text of <paramref name="path"/>: only its file name for "Filename only" (upstream's
    ///  <c>FormatTextForFileNameOnly</c>), otherwise the whole path, which the list shortens to fit with an ellipsis.
    /// </summary>
    public static string Text(string path, TruncatePathMethod method)
    {
        if (method != TruncatePathMethod.FileNameOnly)
        {
            return path;
        }

        string fileName = Path.GetFileName(path.TrimEnd('/'));
        return fileName.Length == 0 ? path : fileName;
    }
}

using Avalonia.Controls;
using GitCommands;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>CopyPathsToolStripMenuItem</c>: the items of a "Copy path(s)" submenu, relative paths (POSIX, native)
///  then full paths (native, WSL, Cygwin).
/// </summary>
internal static class CopyPathsMenu
{
    /// <param name="paths">The repository-relative paths to copy, read when an item is clicked.</param>
    /// <param name="repositoryPath">The working directory the full paths start from, read when an item is clicked.</param>
    /// <param name="copy">Puts the text on the clipboard.</param>
    public static List<MenuItem> Items(Func<IEnumerable<string>> paths, Func<string?> repositoryPath,
        Func<string, Task> copy)
    {
        return
        [
            Item("Copy relative path(s) - _POSIX", path => path.ToPosixPath(), full: false),
            Item("Copy relative path(s) - _native", path => path.ToNativePath(), full: false),
            Item("Copy _full path(s) - native", path => path.ToNativePath(), full: true, bold: true),
            Item("Copy full path(s) - _WSL", path => path.ToWslPath() ?? path, full: true),
            Item("Copy full path(s) - _Cygwin", path => path.ToCygwinPath() ?? path, full: true),
        ];

        MenuItem Item(string header, Func<string, string> convert, bool full, bool bold = false)
        {
            MenuItem item = new()
            {
                Header = UpstreamTranslation.ByText(header),
                FontWeight = bold ? Avalonia.Media.FontWeight.Bold : Avalonia.Media.FontWeight.Normal
            };
            item.Click += (_, _) =>
            {
                string root = repositoryPath() ?? "";
                string text = string.Join(Environment.NewLine, paths().Distinct()
                    .Select(path => convert(full ? Path.Combine(root, path) : path)));
                if (text.Length > 0)
                {
                    _ = copy(text);
                }
            };
            return item;
        }
    }
}

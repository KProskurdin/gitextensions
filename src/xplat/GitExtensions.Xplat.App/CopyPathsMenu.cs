using Avalonia;
using Avalonia.Controls;
using GitExtensions.Xplat.Core.Diff;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>CopyPathsToolStripMenuItem</c>: the items of a "Copy path(s)" submenu, from
///  <see cref="CopyPathsMenuViewModel"/>'s entries.
/// </summary>
internal static class CopyPathsMenu
{
    /// <param name="paths">The repository-relative paths to copy, read when an item is clicked.</param>
    /// <param name="repositoryPath">The working directory the full paths start from, read when an item is clicked.</param>
    /// <param name="copy">Puts the text on the clipboard.</param>
    public static List<MenuItem> Items(Func<IEnumerable<string>> paths, Func<string?> repositoryPath,
        Func<string, Task> copy)
        => [.. new CopyPathsMenuViewModel(paths, repositoryPath, copy, UpstreamTranslation.ByText).Entries()
            .Select(entry => (MenuItem)MenuEntries.Item(entry, Application.Current!))];
}

using System.Windows.Input;
using Avalonia.Controls;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>CopyContextMenuItem</c> as a submenu: the selected commits' branches and tags, hashes, messages, authors
///  and dates (<see cref="BrowseToolbarViewModel.CopyEntries"/>), shared by the browse grid, the left panel and the file
///  history window.
/// </summary>
internal static class CopyMenus
{
    /// <summary>
    ///  Fills <paramref name="host"/> for <paramref name="rows"/> (the latest selected first); a click runs
    ///  <paramref name="copy"/> with the value.
    /// </summary>
    /// <param name="keep">Which labels to list (the focused label menu lists fewer); null for all.</param>
    public static void Fill(MenuItem host, IReadOnlyList<CommitRow> rows, Func<RefLabel, bool>? keep, ICommand copy)
        => MenuEntries.Fill(host.Items, BrowseToolbarViewModel.CopyEntries(rows,
            UpstreamTranslation.Text("TranslatedStrings", "_branchesText", "Branches"),
            UpstreamTranslation.Text("TranslatedStrings", "_tagsText", "Tags"), keep, copy), host);
}

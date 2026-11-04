using Avalonia.Controls;
using Avalonia.Media;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>CopyContextMenuItem</c> as a submenu: the selected commits' branches and tags, hashes, messages, authors
///  and dates (<see cref="GridMenus.CopyItems(IReadOnlyList{CommitRow}, string, string, Func{RefLabel, bool}?)"/>), shared
///  by the browse grid, the left panel and the file history window.
/// </summary>
internal static class CopyMenus
{
    /// <summary>
    ///  Fills <paramref name="host"/> for <paramref name="rows"/> (the latest selected first); a click hands the value to
    ///  <paramref name="copy"/>.
    /// </summary>
    /// <param name="keep">Which labels to list (the focused label menu lists fewer); null for all.</param>
    public static void Fill(MenuItem host, IReadOnlyList<CommitRow> rows, Func<RefLabel, bool>? keep,
        Action<string?> copy)
    {
        host.Items.Clear();
        IReadOnlyList<GridCopyItem?> items = GridMenus.CopyItems(rows,
            UpstreamTranslation.Text("TranslatedStrings", "_branchesText", "Branches"),
            UpstreamTranslation.Text("TranslatedStrings", "_tagsText", "Tags"), keep);
        foreach (GridCopyItem? item in items)
        {
            if (item is null)
            {
                host.Items.Add(new Separator());
                continue;
            }

            MenuItem menuItem = new()
            {
                Header = item.Text,
                Icon = item.Image is null ? null : Icon(item.Image),
                IsEnabled = item.Value is not null,
                FontWeight = item.Value is null ? FontWeight.Bold : FontWeight.Normal,
            };
            string? value = item.Value;
            menuItem.Click += (_, _) => copy(value);
            host.Items.Add(menuItem);
        }
    }

    private static Image? Icon(string name)
        => UpstreamImages.Named(name) is { } picture ? ToolStripContent.Icon(picture) : null;
}

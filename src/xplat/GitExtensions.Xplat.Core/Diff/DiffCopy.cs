namespace GitExtensions.Xplat.Core.Diff;

/// <summary>
///  Upstream FileViewer's copy items for the selected lines, or all lines when none are selected.
/// </summary>
public static class DiffCopy
{
    /// <summary>
    ///  Upstream's "Copy": the lines, a diff's content lines without their leading '+', '-' or space.
    /// </summary>
    public static string Text(IEnumerable<DiffLine> lines, bool isDiff)
        => string.Join("\n", lines.Select(line => isDiff ? WithoutPrefix(line) : line.Text));

    /// <summary>
    ///  Upstream's "Copy patch": the lines as the diff has them.
    /// </summary>
    public static string Patch(IEnumerable<DiffLine> lines) => string.Join("\n", lines.Select(line => line.Text));

    /// <summary>
    ///  Upstream's "Copy new version" and "Copy old version" (<c>CopyNotStartingWith</c>): without the removed or the added
    ///  lines, and without the content lines' prefixes.
    /// </summary>
    public static string Version(IEnumerable<DiffLine> lines, bool isDiff, bool newVersion)
    {
        DiffLineKind left = newVersion ? DiffLineKind.Removed : DiffLineKind.Added;
        return Text(lines.Where(line => !isDiff || line.Kind != left), isDiff);
    }

    private static string WithoutPrefix(DiffLine line)
        => line.Kind is DiffLineKind.Context or DiffLineKind.Added or DiffLineKind.Removed && line.Text.Length > 0
            ? line.Text[1..]
            : line.Text;
}

using Avalonia.Media;
using GitExtensions.Xplat.Core.Diff;
using GitExtensions.Xplat.Core.Settings;
using GitExtUtils.GitUI.Theming;
using DrawingColor = System.Drawing.Color;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Colors that carry meaning (diff lines, graph lanes, ref labels), taken from the active upstream theme's
///  <see cref="AppColor"/> values as upstream uses them: diff lines on the green and red terminal background colors, hunk
///  headers on <see cref="AppColor.DiffSection"/>, labels in the branch, remote branch and tag colors, lanes in the graph
///  branch colors. The text and warning colors have no AppColor upstream and follow the light or dark set.
/// </summary>
internal static class ThemeBrushes
{
    private static readonly IReadOnlyList<string> _noVariations = [];

    /// <summary>
    ///  The palette of the theme <see cref="ThemeApplier"/> applied last; upstream's default theme until then.
    /// </summary>
    public static Palette Current { get; private set; } = Create(
        AppThemeColors.From(Theme.CreateDefaultTheme(), _noVariations));

    public static void Use(AppThemeColors colors) => Current = Create(colors);

    public static IBrush? BackgroundFor(DiffLineKind kind)
    {
        Palette palette = Current;
        return kind switch
        {
            DiffLineKind.Added => palette.AddedBackground,
            DiffLineKind.Removed => palette.RemovedBackground,
            DiffLineKind.Hunk => palette.HunkBackground,
            _ => null,
        };
    }

    public static IBrush ForegroundFor(DiffLineKind kind)
        => kind == DiffLineKind.Header ? Current.Header : Current.Context;

    private static Palette Create(AppThemeColors colors)
    {
        bool dark = colors.IsDark;
        List<IBrush> lanes =
        [
            .. Enumerable.Range(0, 8)
                .Select(i => colors.Get(AppColor.GraphBranch1 + i))
                .Where(color => !color.IsEmpty)
                .Select(Brush)
        ];
        return new Palette(
            IsDark: dark,
            AddedBackground: Brush(colors.Get(AppColor.AnsiTerminalGreenBackNormal)),
            RemovedBackground: Brush(colors.Get(AppColor.AnsiTerminalRedBackNormal)),
            HunkBackground: Brush(colors.Get(AppColor.DiffSection)),
            Removed: Brush(colors.Get(AppColor.AnsiTerminalRedForeNormal)),
            Header: Brush(dark ? "#8B949E" : "#57606A"),
            Context: Brush(dark ? "#E6EDF3" : "#1F2328"),
            BranchLabel: Brush(colors.Get(AppColor.Branch)),
            RemoteBranchLabel: Brush(colors.Get(AppColor.RemoteBranch)),
            TagLabel: Brush(colors.Get(AppColor.Tag)),
            Warning: Brush(dark ? "#D29922" : "#9A6700"),
            Lanes: lanes.Count > 0 ? lanes : [Brush(colors.Get(AppColor.GraphNonRelativeBranch))],
            NonRelativeLane: Brush(colors.Get(AppColor.GraphNonRelativeBranch)));
    }

    private static IBrush Brush(DrawingColor color)
        => new SolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B)).ToImmutable();

    private static IBrush Brush(string color) => new SolidColorBrush(Color.Parse(color)).ToImmutable();

    internal sealed record Palette(
        bool IsDark,
        IBrush AddedBackground,
        IBrush RemovedBackground,
        IBrush HunkBackground,
        IBrush Removed,
        IBrush Header,
        IBrush Context,
        IBrush BranchLabel,
        IBrush RemoteBranchLabel,
        IBrush TagLabel,
        IBrush Warning,
        IReadOnlyList<IBrush> Lanes,
        IBrush NonRelativeLane);
}

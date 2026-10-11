namespace GitUI.UserControls.RevisionGrid.Graph;

/// <summary>
///  Stands in for upstream's <c>RevisionGraphLaneColor</c>, which builds WinForms brushes from the theme. The graph model
///  only needs the lane color index; the new shell's <c>GraphCell</c> picks the brush from the theme's lane colors
///  (<c>AppThemeColors.LaneColors</c>), whose count is set here when a theme is applied, so the index is upstream's.
/// </summary>
public static class RevisionGraphLaneColor
{
    /// <summary>
    ///  The number of the theme's lane colors: upstream's distinct, non-empty GraphBranch1 to GraphBranch8 (seven in the
    ///  default theme).
    /// </summary>
    public static int LaneColorCount { get; set; } = 7;

    public static int GetColorForLane(int seed) => Math.Abs(seed) % LaneColorCount;
}

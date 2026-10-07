namespace GitUI.UserControls.RevisionGrid.Graph;

/// <summary>
///  Stands in for upstream's <c>RevisionGraphLaneColor</c>, which builds WinForms brushes from the theme. The graph model
///  only needs the lane color index; the new shell's <c>GraphCell</c> picks the brush from the theme's lane colors.
/// </summary>
public static class RevisionGraphLaneColor
{
    /// <summary>
    ///  The number of lane colors: upstream's theme colors GraphBranch1 to GraphBranch8.
    /// </summary>
    public const int LaneColorCount = 8;

    public static int GetColorForLane(int seed) => Math.Abs(seed) % LaneColorCount;
}

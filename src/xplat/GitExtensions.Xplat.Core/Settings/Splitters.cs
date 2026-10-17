using GitCommands;

namespace GitExtensions.Xplat.Core.Settings;

/// <summary>
///  WinForms' <c>SplitContainer.FixedPanel</c>: the panel that keeps its size when the splitter's container is resized.
/// </summary>
public enum SplitterFixedPanel
{
    None,
    Panel1,
    Panel2,
}

/// <summary>
///  What upstream's <c>SplitterManager</c> stored for one splitter: its size and the DPI then, the distance of the
///  splitter from the start, and whether either panel was collapsed. Null when not stored.
/// </summary>
public sealed record SplitterSettings(int? Size, int? Dpi, int? Distance, bool? Panel1Collapsed, bool? Panel2Collapsed)
{
    public static SplitterSettings None { get; } = new(null, null, null, null, null);
}

/// <summary>
///  Where splitter positions are kept.
/// </summary>
public interface ISplitterStore
{
    /// <param name="form">Upstream's settings path of the form, e.g. <c>FormBrowse</c> or <c>CommitDialog</c>.</param>
    /// <param name="name">Upstream's splitter name, e.g. <c>MainSplitContainer</c> or <c>revisionDiff.DiffSplitContainer</c>.</param>
    SplitterSettings Load(string form, string name);

    void Save(string form, string name, int size, int dpi, int distance, bool panel1Collapsed, bool panel2Collapsed);
}

/// <summary>
///  Upstream's keys in GitExtensions.settings (<c>SplitterManager.SplitterData</c>): <c>&lt;form&gt;.&lt;name&gt;_Size</c>,
///  <c>_Dpi</c>, <c>_Distance</c>, <c>_Panel1Collapsed</c> and <c>_Panel2Collapsed</c>, so both apps share the positions.
///  Upstream's <c>_FontSize</c> is not written: upstream never reads it back.
/// </summary>
public sealed class UpstreamSplitterStore : ISplitterStore
{
    public SplitterSettings Load(string form, string name)
    {
        AppSettingsPath path = new(form);
        return new SplitterSettings(path.GetInt(name + "_Size"), path.GetInt(name + "_Dpi"),
            path.GetInt(name + "_Distance"), path.GetBool(name + "_Panel1Collapsed"),
            path.GetBool(name + "_Panel2Collapsed"));
    }

    public void Save(string form, string name, int size, int dpi, int distance, bool panel1Collapsed,
        bool panel2Collapsed)
    {
        AppSettingsPath path = new(form);
        path.SetInt(name + "_Dpi", dpi);
        path.SetInt(name + "_Size", size);
        path.SetInt(name + "_Distance", distance);
        path.SetBool(name + "_Panel1Collapsed", panel1Collapsed);
        path.SetBool(name + "_Panel2Collapsed", panel2Collapsed);
        AppSettings.SaveSettings();
    }
}

/// <summary>
///  Upstream's <c>SplitterData.RestoreFromSettings</c> and <c>SetSplitterDistance</c>: where a stored splitter goes in a
///  container of another size or at another DPI, in pixels.
/// </summary>
public static class SplitterLayout
{
    // Upstream's FormCommit.WorkaroundPaddingIncreaseBug offset for a panel 2 that keeps its size.
    private const int PaddingOffset = 2;

    /// <summary>
    ///  The distance to restore, or null to keep the designer's: the stored one when the size and DPI are unchanged;
    ///  else in proportion (no fixed panel), the fixed panel 1 scaled to the DPI, or the fixed panel 2's size kept. A
    ///  distance that would squeeze a panel below its minimum falls back to <paramref name="defaultDistance"/>.
    /// </summary>
    /// <param name="size">The container's size along the splitter's direction now, in pixels.</param>
    /// <param name="dpi">The DPI now.</param>
    public static int? RestoredDistance(SplitterSettings stored, int size, int dpi, SplitterFixedPanel fixedPanel,
        int splitterWidth, int panel1MinSize, int panel2MinSize, int? defaultDistance = null)
    {
        int prevDpi = stored.Dpi ?? dpi;
        int prevSize = stored.Size ?? 0;
        int prevDistance = stored.Distance ?? 0;
        float distance;
        if (prevSize > 0 && prevDistance > 0)
        {
            if (size == prevSize && dpi == prevDpi)
            {
                distance = fixedPanel == SplitterFixedPanel.Panel2 ? prevDistance + splitterWidth : prevDistance;
            }
            else
            {
                distance = fixedPanel switch
                {
                    SplitterFixedPanel.Panel1 => Scale(prevDistance, prevDpi, dpi),
                    SplitterFixedPanel.Panel2 => size - (Scale(prevSize, prevDpi, dpi) - Scale(prevDistance, prevDpi, dpi))
                                                 - PaddingOffset + splitterWidth,
                    _ => (float)size * prevDistance / prevSize,
                };
            }
        }
        else if (defaultDistance.HasValue && stored.Size is null && stored.Distance is null)
        {
            distance = defaultDistance.Value;
        }
        else
        {
            return null;
        }

        int intDistance = Convert.ToInt32(distance);
        return IsValid(intDistance) ? intDistance
            : defaultDistance.HasValue && IsValid(defaultDistance.Value) ? defaultDistance.Value
            : null;

        bool IsValid(int d) => d > panel1MinSize && d < size - panel2MinSize;
    }

    // Upstream's DpiUtil.Scale(int, originalDpi), with the DPI now given.
    private static int Scale(int value, int originalDpi, int dpi) => (int)(value * ((float)dpi / originalDpi));
}

using System.Globalization;
using Avalonia.Controls;
using Avalonia.Media;

namespace GitExtensions.Xplat.App;

/// <summary>
///  The widths of the revision grid's date and SHA-1 columns as upstream's column providers start them. Upstream's grid
///  has no header row, so its widths cannot be changed and are not kept; these are its initial widths.
/// </summary>
internal static class RevisionGridColumns
{
    // Upstream's DateColumnProvider: 130 px for relative dates.
    private const double RelativeDateWidth = 130;

    // Upstream's CommitIdColumnProvider is 60 px wide and shows the characters that fit, eight in its default font; here
    // the column fits the eight characters of the short hash in the monospace font.
    private const string ObjectIdSample = "88888888";

    /// <summary>
    ///  Sets the <c>DateColumnWidth</c> and <c>ObjectIdColumnWidth</c> resources <c>RevisionGrid.axaml</c> reads on the
    ///  window that shows the grid: the date 130 px for relative dates, else the width of the current time in the general
    ///  format in the application font (upstream's DateColumnProvider); the SHA-1 the width of a short hash.
    /// </summary>
    public static void SetWidths(Control window, bool relativeDate)
    {
        window.Resources["DateColumnWidth"] = relativeDate
            ? RelativeDateWidth
            : Measure(window, DateTime.Now.ToString("G", CultureInfo.CurrentCulture), FontApplier.ApplicationFamilyKey,
                FontApplier.ApplicationSizeKey, weightKey: null, styleKey: null);
        window.Resources["ObjectIdColumnWidth"] = Measure(window, ObjectIdSample, "MonospaceFontFamily",
            "MonospaceFontSize", "MonospaceFontWeight", "MonospaceFontStyle");
    }

    private static double Measure(Control window, string text, string familyKey, string sizeKey, string? weightKey,
        string? styleKey)
    {
        FontFamily family = Find(window, familyKey, FontFamily.Default);
        double size = Find(window, sizeKey, 12.0);
        FontWeight weight = weightKey is null ? FontWeight.Normal : Find(window, weightKey, FontWeight.Normal);
        FontStyle style = styleKey is null ? FontStyle.Normal : Find(window, styleKey, FontStyle.Normal);
        FormattedText formatted = new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(family, style, weight), size, foreground: null);
        return Math.Ceiling(formatted.WidthIncludingTrailingWhitespace);
    }

    private static T Find<T>(Control window, string key, T fallback)
        => window.TryFindResource(key, out object? value) && value is T found ? found : fallback;
}

using System.Globalization;

namespace GitExtensions.Xplat.Core.Settings;

/// <summary>
///  The fonts of upstream's Fonts settings page, by upstream's setting key.
/// </summary>
public enum AppFont
{
    /// <summary>Upstream <c>font</c>: "Application font", the font of the whole UI.</summary>
    Application,

    /// <summary>Upstream <c>commitfont</c>: "Commit font", the commit message editor and the commit details.</summary>
    Commit,

    /// <summary>Upstream <c>difffont</c> (<c>FixedWidthFont</c>): "Code font", diffs, files and git output.</summary>
    Code,

    /// <summary>Upstream <c>monospacefont</c>: "Monospace font", commit hashes and lists of them.</summary>
    Monospace,
}

/// <summary>
///  A font as upstream stores it: family, size in points, bold and italic. Upstream reads and writes it through
///  <c>System.Drawing.Font</c> (<c>FontParser</c>), which works only on Windows, so the stored text is handled here.
/// </summary>
public sealed record FontSetting(string Family, float Size, bool Bold = false, bool Italic = false)
{
    private const string InvariantCultureId = "_IC_";
    private const double PixelsPerPoint = 96.0 / 72.0;

    /// <summary>
    ///  Upstream's setting key of a font.
    /// </summary>
    public static string KeyOf(AppFont font)
        => font switch
        {
            AppFont.Application => "font",
            AppFont.Commit => "commitfont",
            AppFont.Code => "difffont",
            _ => "monospacefont",
        };

    /// <summary>
    ///  The size in device-independent pixels, as Avalonia measures fonts.
    /// </summary>
    public double SizeInPixels => Size * PixelsPerPoint;

    /// <summary>
    ///  Upstream's <c>FontParser.Parse</c>: "family;size[;_IC_][;bold;italic]", with a size written in the invariant culture
    ///  or, in older values, with ',' or '.' as the decimal separator. Null for an empty or unreadable value, where upstream
    ///  uses its default font.
    /// </summary>
    public static FontSetting? Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string[] parts = value.Split(';');
        if (parts.Length < 2)
        {
            return null;
        }

        string size = parts.Length == 3 && parts[2] == InvariantCultureId
            ? parts[1]
            : parts[1].Replace(",", ".");
        if (!float.TryParse(size, NumberStyles.Float, CultureInfo.InvariantCulture, out float points) || points <= 0 ||
            parts[0].Length == 0)
        {
            return null;
        }

        return new FontSetting(parts[0], points, Bold: parts.Length > 3 && parts[3] == "1",
            Italic: parts.Length > 4 && parts[4] == "1");
    }

    /// <summary>
    ///  Upstream's <c>FontParser.AsString</c>.
    /// </summary>
    public string ToSettingString()
        => string.Format(CultureInfo.InvariantCulture, "{0};{1};{2};{3};{4}", Family, Size, InvariantCultureId,
            Bold ? 1 : 0, Italic ? 1 : 0);

    /// <summary>
    ///  As upstream's Fonts page names a font on its button: "family, size" with the size rounded.
    /// </summary>
    public override string ToString() => $"{Family}, {(int)(Size + 0.5f)}";
}

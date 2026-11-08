using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using GitExtensions.Xplat.Core.Settings;
using GitExtUtils.GitUI.Theming;
using DrawingColor = System.Drawing.Color;
using KnownColor = System.Drawing.KnownColor;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>AdaptLightness</c> for a picture: unchanged in upstream's default theme, adapted to the text and background
///  colors in any other.
/// </summary>
public static class ThemedImages
{
    private const int BytesPerPixel = 4;

    // By the theme's colors too, so a theme changed while the app runs gets its own copy.
    private static readonly Dictionary<(Bitmap Source, DrawingColor Text, DrawingColor Background), Bitmap> _adapted = [];

    /// <summary>
    ///  Gets <paramref name="original"/> as the active theme shows it.
    /// </summary>
    public static Bitmap? AdaptLightness(Bitmap? original)
    {
        AppThemeColors colors = ThemeBrushes.Current.Colors;
        return original is null || colors.Id == ThemeId.DefaultLight
            ? original
            : Adapted(original, colors.GetNonEmpty(KnownColor.WindowText), colors.GetNonEmpty(KnownColor.Window));
    }

    /// <summary>
    ///  Gets <paramref name="original"/> adapted to <paramref name="text"/> and <paramref name="background"/>.
    /// </summary>
    public static Bitmap Adapted(Bitmap original, DrawingColor text, DrawingColor background)
    {
        if (_adapted.TryGetValue((original, text, background), out Bitmap? known))
        {
            return known;
        }

        ImageLightness lightness = new(text, background);
        PixelSize size = original.PixelSize;
        PixelFormat format = original.Format ?? PixelFormat.Bgra8888;
        AlphaFormat alphaFormat = original.AlphaFormat ?? AlphaFormat.Premul;
        WriteableBitmap adapted = new(size, original.Dpi, format, alphaFormat);
        using (ILockedFramebuffer target = adapted.Lock())
        {
            original.CopyPixels(new PixelRect(size), target.Address, target.RowBytes * size.Height, target.RowBytes);
            byte[] row = new byte[size.Width * BytesPerPixel];
            for (int y = 0; y < size.Height; y++)
            {
                IntPtr address = target.Address + (y * target.RowBytes);
                Marshal.Copy(address, row, 0, row.Length);
                lightness.TransformRow(row, redFirst: format == PixelFormat.Rgba8888, premultiplied: alphaFormat == AlphaFormat.Premul);
                Marshal.Copy(row, 0, address, row.Length);
            }
        }

        _adapted[(original, text, background)] = adapted;
        return adapted;
    }
}

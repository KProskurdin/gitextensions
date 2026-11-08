using GitExtUtils.GitUI.Theming;

namespace GitExtensions.Xplat.Core.Settings;

/// <summary>
///  Upstream's <c>LightnessCorrection</c>, one color at a time: upstream's class needs a GDI+ bitmap, which works only on
///  Windows. A picture drawn for a white page maps its black to the theme's text and its white to the theme's background.
/// </summary>
public sealed class ImageLightness(Color text, Color background)
{
    // Near black can have a high saturation mathematically, while its hue cannot be seen.
    private const double VisibleSaturationLightness = 0.1;
    private const int BytesPerPixel = 4;
    private const int GreenOffset = 1;
    private const int AlphaOffset = 3;
    private const int Opaque = 255;

    private readonly HslColor _text = new(text);
    private readonly HslColor _background = new(background);

    /// <summary>
    ///  Transforms a row of 32 bit pixels in place, leaving their opacity and the fully transparent ones alone.
    /// </summary>
    /// <param name="row">The pixels' bytes.</param>
    /// <param name="redFirst">True when the bytes run red, green, blue, alpha; else blue, green, red, alpha.</param>
    /// <param name="premultiplied">True when the color bytes are scaled by the opacity, as a decoder's usually are.</param>
    public void TransformRow(Span<byte> row, bool redFirst, bool premultiplied)
    {
        (int red, int blue) = redFirst ? (0, 2) : (2, 0);
        for (int offset = 0; offset < row.Length; offset += BytesPerPixel)
        {
            byte alpha = row[offset + AlphaOffset];
            if (alpha == 0)
            {
                continue;
            }

            int scale = premultiplied ? alpha : Opaque;
            Color color = Transform(Color.FromArgb(
                Math.Min(Opaque, row[offset + red] * Opaque / scale),
                Math.Min(Opaque, row[offset + GreenOffset] * Opaque / scale),
                Math.Min(Opaque, row[offset + blue] * Opaque / scale)));
            row[offset + red] = (byte)(color.R * scale / Opaque);
            row[offset + GreenOffset] = (byte)(color.G * scale / Opaque);
            row[offset + blue] = (byte)(color.B * scale / Opaque);
        }
    }

    /// <summary>
    ///  Gets the color <paramref name="original"/> becomes; its opacity is the caller's.
    /// </summary>
    public Color Transform(Color original)
    {
        HslColor hsl = original.ToPerceptedHsl();
        double saturation = hsl.L > VisibleSaturationLightness ? hsl.S : hsl.S * hsl.L / VisibleSaturationLightness;
        HslColor transformed = new(hsl.H, saturation, _text.L + (hsl.L * (_background.L - _text.L)));
        return transformed.ToActualHsl(original).ToColor();
    }
}

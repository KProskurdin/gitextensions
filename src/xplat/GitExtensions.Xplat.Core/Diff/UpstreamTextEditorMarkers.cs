using System.Drawing;

// Stand-ins for what upstream's linked AnsiEscapeUtilities and DiffLineNumAnalyzer take from the WinForms text editor and its
// theme module: they only carry a colored range of the text, and the theme's colors.
namespace ICSharpCode.TextEditor.Document
{
    /// <summary>
    ///  A range of a document (the text editor's <c>ISegment</c>).
    /// </summary>
    public interface ISegment
    {
        int Offset { get; set; }

        int Length { get; set; }
    }

    /// <summary>
    ///  What upstream's linked <c>LinesMatcher</c> takes from the text editor's <c>TextUtilities</c>.
    /// </summary>
    public static class TextUtilities
    {
        public static bool IsLetterDigitOrUnderscore(char c) => char.IsLetterOrDigit(c) || c == '_';
    }

    /// <summary>
    ///  The text editor's kinds of markers; upstream's parser uses the one.
    /// </summary>
    public enum TextMarkerType
    {
        SolidBlock,
    }

    /// <summary>
    ///  A colored range of a document (the text editor's <c>TextMarker</c>).
    /// </summary>
    public class TextMarker : ISegment
    {
        public TextMarker(int offset, int length, TextMarkerType textMarkerType, Color color)
        {
            Offset = offset;
            Length = length;
            TextMarkerType = textMarkerType;
            Color = color;
        }

        public TextMarker(int offset, int length, TextMarkerType textMarkerType, Color color, Color foreColor)
            : this(offset, length, textMarkerType, color)
        {
            ForeColor = foreColor;
        }

        public int Offset { get; set; }

        public int Length { get; set; }

        public int EndOffset => Offset + Length - 1;

        public TextMarkerType TextMarkerType { get; }

        public Color Color { get; }

        public Color? ForeColor { get; }
    }
}

namespace GitUI.Theming
{
    using GitExtUtils.GitUI.Theming;

    /// <summary>
    ///  Upstream's <c>AppColorExtension</c>, over the theme the app applied instead of upstream's <c>ThemeModule</c>.
    /// </summary>
    public static class AppColorExtension
    {
        /// <summary>
        ///  The applied theme's colors, set when a theme is applied.
        /// </summary>
        public static Func<AppColor, Color> ThemeColors { get; set; } = _ => Color.Empty;

        /// <summary>
        ///  Whether the applied theme is dark, as upstream's <c>Application.IsDarkModeEnabled</c>.
        /// </summary>
        public static bool IsDark { get; set; }

        public static Color GetThemeColor(this AppColor name) => ThemeColors(name);
    }
}

namespace GitUI.Editor.Diff
{
    using GitUI.Theming;

    /// <summary>
    ///  What upstream's linked <c>AnsiEscapeUtilities</c> takes from WinForms' <c>Application</c>.
    /// </summary>
    internal static class Application
    {
        public static bool IsDarkModeEnabled => AppColorExtension.IsDark;
    }
}

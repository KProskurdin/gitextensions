using System.Drawing;
using GitExtUtils.GitUI.Theming;

namespace GitExtensions.Xplat.Core.Avatars;

/// <summary>
///  Upstream's <c>InitialsAvatarProvider</c> without its drawing: the initials of an author and the colors of the avatar
///  (a background from upstream's palette chosen by the e-mail, and black or white text, whichever contrasts).
/// </summary>
public sealed class AuthorInitials
{
    private static readonly char[] _emailInitialSeparator = ['.', '-', '_'];

    private readonly IReadOnlyList<(Color Background, Color Foreground)> _colors;
    private int _unknownCounter;

    /// <param name="palette">Upstream's <c>Appearance.AvatarAuthorInitialsPalette</c>: color names or #AARRGGBB codes,
    ///  separated by ','.</param>
    /// <param name="luminanceThreshold">Upstream's <c>AvatarAuthorInitialsLuminanceThreshold</c>.</param>
    public AuthorInitials(string palette, float luminanceThreshold)
    {
        _colors =
        [
            .. palette.Split(',').Select(ToColor)
                .Select(background => (background, background.GetContrastColor(luminanceThreshold)))
        ];
    }

    /// <summary>
    ///  The initials and colors upstream draws for the author.
    /// </summary>
    public (string Initials, Color Background, Color Foreground) For(string email, string? name)
    {
        (string? selectedName, char[]? separator) = NameSelector(name, email);
        int colorIndex;
        string initials;
        if (selectedName is null)
        {
            initials = "?";
            colorIndex = _unknownCounter++;
        }
        else
        {
            initials = GetInitialsFromNames(selectedName.Split(separator));
            colorIndex = GetDeterministicHashCode(email);
        }

        (Color background, Color foreground) = _colors[colorIndex % _colors.Count];
        return (initials, background, foreground);
    }

    // Upstream's ConvertToColor: an unknown color is black.
    private static Color ToColor(string code)
    {
        try
        {
            return ColorTranslator.FromHtml(code.Trim());
        }
        catch (Exception)
        {
            return Color.Black;
        }
    }

    // Upstream's GetDeterministicHashCode, the same in every run (string.GetHashCode is not).
    private static int GetDeterministicHashCode(string text)
    {
        unchecked
        {
            int hash = 23;
            foreach (char c in text)
            {
                hash = (hash * 31) + c;
            }

            return Math.Abs(hash);
        }
    }

    private static (string? Name, char[]? Separator) NameSelector(string? name, string? email)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            return (name.Trim(), null);
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            return (email.Split('@')[0].TrimStart(), _emailInitialSeparator);
        }

        return (null, null);
    }

    // Upstream's GetInitialsFromNames, unchanged in its rules.
    private static string GetInitialsFromNames(string[] possibleNames)
    {
        possibleNames = [.. possibleNames.Where(s => !string.IsNullOrWhiteSpace(s))];
        if (possibleNames.Length == 0)
        {
            return "?";
        }

        string[] names = [.. possibleNames.Where(s => char.IsLetter(s[0]) || char.IsDigit(s[0]))];
        if (names.Length == 0)
        {
            return possibleNames.Length > 1 ? $"{possibleNames[0][0]}{possibleNames[1][0]}" : $"{possibleNames[0][0]}";
        }

        string name = names[0];
        if (names.Length > 1)
        {
            return $"{name[0]}{names[^1][0]}".ToUpper();
        }

        if (name.Length == 1)
        {
            return name.ToUpper();
        }

        if (char.IsUpper(name[1]))
        {
            return $"{char.ToUpper(name[0])}{name[1]}";
        }

        string[] splitNames = name.Split(_emailInitialSeparator);
        if (splitNames.Length > 1)
        {
            return GetInitialsFromNames(splitNames);
        }

        char[] upperChars = [.. name.Where(char.IsUpper)];
        return upperChars.Length > 1 ? $"{upperChars[0]}{upperChars[^1]}" : $"{char.ToUpper(name[0])}{name[1]}";
    }
}

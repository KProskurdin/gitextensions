using System.Text.RegularExpressions;

namespace GitExtensions.Xplat.Core.Platform;

/// <summary>
///  Upstream's contributor lists (<c>GitUI/Properties/Resources.resx</c>: Team, Coders, Translators, Designers) as
///  <c>FormAbout</c> thanks them one at a time and <c>FormContributors</c> shows them on its tabs.
/// </summary>
public static partial class Contributors
{
    // Upstream's FormAbout texts.
    public const string ThanksTo = "Thanks to over {0:#,##0} contributors: ";
    public const string CopyTooltip = "Copy environment info";
    public const string OpenSource = "Git Extensions is open source. Get involved!";
    public const string IconsBy = "Some icons by Yusuke Kamiyamane (CCA3)";
    public const string ProductDescription = "Visual Studio and Shell Explorer Extensions for Git";
    public const string Copyright = "Proudly presented by Git Extensions team";
    public const string Warranty = "This program is distributed in the hope that it will be useful, but WITHOUT ANY "
                                   + "WARRANTY; without even the implied warranty of MERCHANTABILITY of FITNESS FOR A "
                                   + "PARTICULAR PURPOSE.";

    // Upstream's FormAbout links.
    public const string ProjectUrl = "https://github.com/gitextensions/gitextensions";
    public const string IconsUrl = "http://p.yusukekamiyamane.com/";

    // Upstream's FormContributors texts.
    public const string Developers = "Developers";
    public const string Translators = "Translators";
    public const string Designers = "Designers";
    public const string Team = "Team";
    public const string ContributorsCaption = "Contributors";
    public const string Caption = "The application would not be possible without...";

    /// <summary>
    ///  How often upstream's thanks line moves to another contributor.
    /// </summary>
    public static readonly TimeSpan ThanksInterval = TimeSpan.FromSeconds(1);

    [GeneratedRegex(@"\r\n?|\n", RegexOptions.ExplicitCapture)]
    private static partial Regex NewlineRegex { get; }

    /// <summary>
    ///  Upstream's <c>GetContributorList</c>: every name of the four lists, which are comma separated over several lines.
    /// </summary>
    public static IReadOnlyList<string> List(string team, string coders, string translators, string designers)
        => [.. new[] { team, coders, translators, designers }
            .Select(names => names.Replace(Environment.NewLine, "").Replace("\n", ""))
            .SelectMany(names => names.Split(',', StringSplitOptions.RemoveEmptyEntries))
            .Select(name => name.Trim())];

    /// <summary>
    ///  Upstream's <c>FormContributors</c> Developers tab: the team, a blank line and the contributors, each list on one
    ///  line.
    /// </summary>
    public static string DevelopersText(string team, string coders, string teamCaption, string contributorsCaption)
        => $"{teamCaption}:\r\n{OneLine(team)}\r\n\r\n{contributorsCaption}:\r\n{OneLine(coders)}";

    /// <summary>
    ///  Upstream's <c>NewlineRegex.Replace(list, " ")</c>: a list's lines joined with spaces.
    /// </summary>
    public static string OneLine(string names) => NewlineRegex.Replace(names, " ");
}

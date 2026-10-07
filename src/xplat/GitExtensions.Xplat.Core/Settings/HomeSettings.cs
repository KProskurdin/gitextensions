using GitCommands;

namespace GitExtensions.Xplat.Core.Settings;

/// <summary>
///  The HOME choices of upstream's <c>FormFixHome</c>: upstream's default, <c>USERPROFILE</c>, or another folder
///  (upstream's <c>userprofilehomedir</c> and <c>customhomedir</c>).
/// </summary>
public enum HomeChoice
{
    /// <summary>Upstream's <see cref="EnvironmentConfiguration.GetDefaultHomeDir"/>.</summary>
    Default,

    /// <summary>The <c>USERPROFILE</c> folder (Windows).</summary>
    UserProfile,

    /// <summary>A folder the user typed.</summary>
    Other,
}

/// <summary>
///  The texts and rules of the HOME part of upstream's Git page and of its <c>FormFixHome</c>.
/// </summary>
public static class HomeSettings
{
    /// <summary>
    ///  The variable that tells git where the global config is, which upstream's Git page reports first.
    /// </summary>
    public const string GitConfigGlobalVariable = "GIT_CONFIG_GLOBAL";

    /// <summary>
    ///  Upstream's message when no HOME folder is typed.
    /// </summary>
    public const string NoHomeDirectorySpecified = "Please enter a HOME directory.";

    /// <summary>
    ///  Upstream's Git page line: "%GIT_CONFIG_GLOBAL% is set to: ..." when the variable is set, otherwise HOME's value and
    ///  that the variable is not set. Variables are written as the OS writes them (%NAME% on Windows, $NAME elsewhere).
    /// </summary>
    public static string Describe(string? gitConfigGlobal, string home, bool windows)
    {
        string name = GitConfigGlobalVariable;
        string value = gitConfigGlobal ?? home;
        string additional = "";
        if (gitConfigGlobal is null)
        {
            additional = $"    ({string.Format("{0} is not set.", Variable(name, windows))})";
            name = "HOME";
        }

        return string.Format("{0} is set to: {1}", Variable(name, windows), value) + additional;
    }

    /// <summary>
    ///  The choice the stored settings mean, as upstream's <c>FormFixHome.LoadSettings</c> reads them: a custom folder wins
    ///  over the <c>USERPROFILE</c> flag.
    /// </summary>
    public static HomeChoice ChoiceOf(string customHomeDir, bool userProfileHomeDir)
        => !string.IsNullOrEmpty(customHomeDir) ? HomeChoice.Other
            : userProfileHomeDir ? HomeChoice.UserProfile
            : HomeChoice.Default;

    /// <summary>
    ///  Upstream's checks before a choice is kept: another folder must be typed and HOME must then be a folder that exists.
    ///  Returns upstream's message, or null when the choice can be stored.
    /// </summary>
    public static string? Validate(HomeChoice choice, string otherFolder, string? resultingHome,
        Func<string, bool> directoryExists)
    {
        if (choice == HomeChoice.Other && string.IsNullOrEmpty(otherFolder))
        {
            return NoHomeDirectorySpecified;
        }

        return string.IsNullOrEmpty(resultingHome) || !directoryExists(resultingHome)
            ? "The environment variable HOME points to a directory that is not accessible:" + Environment.NewLine +
              $"\"{resultingHome}\""
            : null;
    }

    /// <summary>
    ///  The HOME a choice gives, as upstream's <c>EnvironmentConfiguration</c> computes it.
    /// </summary>
    public static string? HomeFor(HomeChoice choice, string otherFolder, Func<string, string?> environment)
        => choice switch
        {
            HomeChoice.Other => otherFolder,
            HomeChoice.UserProfile => environment("USERPROFILE"),
            _ => EnvironmentConfiguration.GetDefaultHomeDir(),
        };

    private static string Variable(string name, bool windows) => windows ? $"%{name}%" : "$" + name;
}

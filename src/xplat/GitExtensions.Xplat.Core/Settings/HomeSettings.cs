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
///  Where upstream's <c>FormFixHome</c> found a folder with a global git config, in the order it looks
///  (<c>LoadSettings</c>).
/// </summary>
public enum HomeFoundIn
{
    /// <summary>The user's own HOME variable, which upstream's default then gives.</summary>
    HomeVariable,

    /// <summary>HOMEDRIVE and HOMEPATH, which upstream's default then gives.</summary>
    HomeDriveAndPath,

    /// <summary>The <c>USERPROFILE</c> folder.</summary>
    UserProfile,

    /// <summary>The personal (documents) folder, chosen as another folder.</summary>
    PersonalFolder,
}

/// <summary>
///  A folder upstream's <c>FormFixHome</c> found with a global git config, and the choice it then makes.
/// </summary>
public sealed record HomeLocation(HomeFoundIn FoundIn, string Folder)
{
    /// <summary>
    ///  The HOME choice upstream checks for the folder.
    /// </summary>
    public HomeChoice Choice => FoundIn switch
    {
        HomeFoundIn.UserProfile => HomeChoice.UserProfile,
        HomeFoundIn.PersonalFolder => HomeChoice.Other,
        _ => HomeChoice.Default,
    };

    /// <summary>
    ///  Upstream's field and English text of the message that says where the file was found, {0} for the folder.
    /// </summary>
    public (string Name, string English) Message => FoundIn switch
    {
        HomeFoundIn.HomeVariable => ("_gitconfigFoundHome",
            "Located .gitconfig in %HOME% ({0}). This setting has been chosen automatically."),
        HomeFoundIn.HomeDriveAndPath => ("_gitconfigFoundHomedrive",
            "Located .gitconfig in %HOMEDRIVE%%HOMEPATH% ({0}). This setting has been chosen automatically."),
        HomeFoundIn.UserProfile => ("_gitconfigFoundUserprofile",
            "Located .gitconfig in %USERPROFILE% ({0}). This setting has been chosen automatically."),
        _ => ("_gitconfigFoundPersonalFolder",
            "Located .gitconfig in personal folder ({0}). This setting has been chosen automatically."),
    };
}

/// <summary>
///  What upstream's <c>FormFixHome</c> reads to judge HOME and to look for a better one: the process's and the user's
///  variables, the personal folder and the file system. <see cref="System"/> reads the real ones; tests pass their own.
/// </summary>
public sealed record HomeLookup(
    Func<string, string?> Variable,
    Func<string, string?> UserVariable,
    Func<string?> PersonalFolder,
    Func<string, bool> DirectoryExists,
    Func<string, bool> CanReadFile)
{
    /// <summary>
    ///  The process's environment, the user's stored variables (Windows), the documents folder and the disk.
    /// </summary>
    public static HomeLookup System { get; } = new(
        Environment.GetEnvironmentVariable,
        name => Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User),
        () => Environment.GetFolderPath(Environment.SpecialFolder.Personal),
        Directory.Exists,
        CanRead);

    // Upstream's CanReadFile: the file exists and can be opened for reading.
    private static bool CanRead(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return false;
            }

            File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite).Dispose();
            return true;
        }
        catch
        {
            return false;
        }
    }
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
    ///  Upstream FormFixHome's caption of its question (<c>_gitGlobalConfigNotFoundCaption</c>).
    /// </summary>
    public const string GlobalConfigNotFoundCaption = "Global config";

    /// <summary>
    ///  Upstream FormFixHome's question (<c>_gitGlobalConfigNotFound</c>), {0} for HOME.
    /// </summary>
    public static string GlobalConfigNotFoundText { get; } =
        "The environment variable HOME does not point to a directory that contains the global git config file:"
        + Environment.NewLine + "\" {0} \"" + Environment.NewLine + Environment.NewLine
        + "Do you want Git Extensions to help locate the correct folder?";

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

    /// <summary>
    ///  Upstream FormFixHome's <c>HasGlobalGitConfig</c>: <paramref name="path"/> is a folder with a readable
    ///  <c>.gitconfig</c>, or with git's XDG file (<c>.config/git/config</c>) when <c>XDG_CONFIG_HOME</c> is unset or is
    ///  that <c>.config</c> folder.
    /// </summary>
    public static bool HasGlobalGitConfig(string? path, HomeLookup lookup)
    {
        if (string.IsNullOrEmpty(path) || !lookup.DirectoryExists(path))
        {
            return false;
        }

        if (lookup.CanReadFile(Path.Join(path, ".gitconfig")))
        {
            return true;
        }

        string xdgConfigDir = Path.Join(path, ".config");
        if (!lookup.DirectoryExists(xdgConfigDir))
        {
            return false;
        }

        string? xdgConfigHome = lookup.Variable("XDG_CONFIG_HOME");
        return (string.IsNullOrEmpty(xdgConfigHome) || xdgConfigHome == xdgConfigDir)
               && lookup.CanReadFile(Path.Join(xdgConfigDir, "git", "config"));
    }

    /// <summary>
    ///  Upstream FormFixHome's <c>IsFixHome</c>, which its <c>CheckHomePath</c> asks after the settings are saved on
    ///  Windows: HOME is not a folder, or it has no global git config while a folder FormFixHome looks in has one.
    /// </summary>
    public static bool NeedsFix(HomeLookup lookup)
    {
        try
        {
            string? home = lookup.Variable("HOME");
            if (string.IsNullOrEmpty(home) || !lookup.DirectoryExists(home))
            {
                return true;
            }

            if (HasGlobalGitConfig(home, lookup))
            {
                return false;
            }

            string?[] candidates =
            [
                lookup.UserVariable("HOME"),
                lookup.Variable("HOMEDRIVE") + lookup.Variable("HOMEPATH"),
                lookup.Variable("USERPROFILE"),
                lookup.PersonalFolder(),
            ];
            return candidates.Any(candidate => HasGlobalGitConfig(candidate, lookup));
        }
        catch
        {
            // As upstream: when HOME cannot be checked (a security issue, say), the user fixes it by hand.
            return true;
        }
    }

    /// <summary>
    ///  Upstream FormFixHome's search when it opens (<c>LoadSettings</c>): the first of the user's own HOME, HOMEDRIVE
    ///  and HOMEPATH, USERPROFILE and the personal folder with a global git config, or null when none has one.
    /// </summary>
    public static HomeLocation? Locate(HomeLookup lookup)
    {
        (HomeFoundIn FoundIn, Func<string?> Folder)[] places =
        [
            (HomeFoundIn.HomeVariable, () => lookup.UserVariable("HOME")),
            (HomeFoundIn.HomeDriveAndPath, () => lookup.Variable("HOMEDRIVE") + lookup.Variable("HOMEPATH")),
            (HomeFoundIn.UserProfile, () => lookup.Variable("USERPROFILE")),
            (HomeFoundIn.PersonalFolder, lookup.PersonalFolder),
        ];
        foreach ((HomeFoundIn foundIn, Func<string?> folder) in places)
        {
            try
            {
                if (folder() is { } path && HasGlobalGitConfig(path, lookup))
                {
                    return new HomeLocation(foundIn, path);
                }
            }
            catch
            {
                // As upstream: a folder that cannot be checked is passed over, and the user chooses.
            }
        }

        return null;
    }

    private static string Variable(string name, bool windows) => windows ? $"%{name}%" : "$" + name;
}

using System.Diagnostics;
using GitCommands;
using GitExtensions.Extensibility;
using GitExtensions.Xplat.Core.Operations;

namespace GitExtensions.Xplat.Core.Settings;

/// <summary>
///  The values upstream's Git config settings page (<c>GitConfigSettingsPage</c>) offers for <c>credential.helper</c> and
///  <c>core.editor</c>, found per OS.
/// </summary>
public static class GitConfigChoices
{
    private const string CredentialHelperPrefix = "git-credential-";

    // Upstream leaves out Git for Windows' helper selector, which is a chooser rather than a helper.
    private const string HelperSelector = "git-credential-helper-selector";

    // A helper's own background process (git-credential-cache--daemon) is not a helper.
    private const string HelperPartSeparator = "--";

    /// <summary>
    ///  The credential helpers git can use here, by the name <c>credential.helper</c> takes. As upstream on Windows: the
    ///  helpers shipped with Git for Windows (<c>manager</c>, ...), then <c>store</c> and <c>cache</c>. Upstream offers
    ///  only <c>oauth</c>, <c>store</c> and <c>cache</c> on other OSes; the new shell also lists the helpers installed in
    ///  git's own folder (<c>osxkeychain</c> on macOS, <c>libsecret</c> where the distribution ships it) and on the PATH
    ///  (<c>manager</c> when Git Credential Manager is installed), so the keychain helpers can be chosen (PLAN.md, section 9,
    ///  question 6).
    /// </summary>
    public static IReadOnlyList<string> CredentialHelpers()
        => CredentialHelpers(OperatingSystem.IsWindows(), AppSettings.GitCommand, GitExecPath(),
            Environment.GetEnvironmentVariable("PATH"), ListFiles);

    /// <summary>
    ///  <see cref="CredentialHelpers()"/> with the file listing given, so each OS's rules can be tested anywhere.
    ///  <paramref name="listFiles"/> returns the files in a folder (and its subfolders when asked), or none when it is missing.
    /// </summary>
    public static IReadOnlyList<string> CredentialHelpers(bool windows, string gitCommand, string? execPath, string? path,
        Func<string, bool, IEnumerable<string>> listFiles)
    {
        List<string> helpers = [];
        if (windows)
        {
            // Upstream's FindGitCredentialHelpers: every git-credential-*.exe in the Git installation.
            string? gitDir = Path.GetDirectoryName(gitCommand);
            if (gitDir?.EndsWith("bin", StringComparison.Ordinal) is true)
            {
                gitDir = Path.GetDirectoryName(gitDir);
            }

            if (gitDir is not null)
            {
                helpers.AddRange(listFiles(gitDir, true)
                    .Where(file => Path.GetFileName(file).StartsWith(CredentialHelperPrefix, StringComparison.OrdinalIgnoreCase)
                                   && file.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                                   && !file.Contains(HelperSelector, StringComparison.OrdinalIgnoreCase))
                    .Select(HelperName));
            }
        }
        else
        {
            IEnumerable<string> folders = [execPath ?? "", .. (path ?? "").Split(Path.PathSeparator)];
            helpers.AddRange(folders
                .Where(folder => folder.Length > 0)
                .SelectMany(folder => listFiles(folder, false))
                .Select(HelperName)
                .Where(name => name.Length > 0 && !name.Contains(HelperPartSeparator, StringComparison.Ordinal)
                               && name != HelperSelector[CredentialHelperPrefix.Length..]));
            helpers.Add("oauth");
        }

        helpers.AddRange(["store", "cache"]);
        return [.. helpers.Distinct(StringComparer.Ordinal)];

        static string HelperName(string file)
        {
            string name = Path.GetFileNameWithoutExtension(file);
            return name.StartsWith(CredentialHelperPrefix, StringComparison.OrdinalIgnoreCase)
                ? name[CredentialHelperPrefix.Length..]
                : "";
        }
    }

    /// <summary>
    ///  The editors upstream's page offers for <c>core.editor</c> (upstream's <c>EditorHelper</c>): this app's own editor
    ///  first, then the common ones. Windows programs are looked for where upstream looks; on other OSes the commands are
    ///  their usual names on the PATH.
    /// </summary>
    public static IReadOnlyList<string> Editors()
    {
        string app = GitEditorCommand.Build(Environment.ProcessPath ?? "GitExtensions",
            System.Reflection.Assembly.GetEntryAssembly()?.Location);
        if (OperatingSystem.IsWindows())
        {
            return
            [
                app,
                "vi",
                "notepad",
                WindowsEditor("notepad++.exe", "-multiInst -nosession", "notepad++"),
                WindowsEditor("sublime_text.exe", "--new-window --wait", "Sublime Text"),
                WindowsEditor("code.exe", "--new-window --wait", "Microsoft VS Code"),
                WindowsEditor("zed.exe", "--wait", "Zed.dev"),
            ];
        }

        return [app, "vi", "nano", "subl --new-window --wait", "code --new-window --wait", "zed --wait"];
    }

    // Upstream's GetEditorCommandLine: the full path when the program is installed in one of its folders, otherwise its name
    // in the hope that it is on the PATH.
    private static string WindowsEditor(string executableName, string arguments, string installFolder)
    {
        string found = executableName.FindInFolders([installFolder]);
        string command = string.IsNullOrEmpty(found) ? Path.GetFileNameWithoutExtension(executableName) : $"\"{found}\"";
        return $"{command} {arguments}";
    }

    private static string? GitExecPath()
    {
        try
        {
            ExecutionResult result = new Executable(AppSettings.GitCommand,
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)).Execute("--exec-path", throwOnErrorExit: false);
            return result.ExitedSuccessfully ? result.StandardOutput.Trim() : null;
        }
        catch (Exception exception)
        {
            Trace.Write(exception);
            return null;
        }
    }

    private static IEnumerable<string> ListFiles(string folder, bool recursive)
    {
        try
        {
            return Directory.Exists(folder)
                ? Directory.GetFiles(folder, $"{CredentialHelperPrefix}*",
                    recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
                : [];
        }
        catch (Exception exception)
        {
            // As upstream: a folder that cannot be read gives no helpers.
            Trace.Write(exception);
            return [];
        }
    }
}

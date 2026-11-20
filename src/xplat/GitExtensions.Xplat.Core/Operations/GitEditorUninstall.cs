using GitCommands;
using GitExtensions.Xplat.Core.Settings;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  Upstream's <c>GitUICommands.UninstallEditor</c>, the command line's <c>uninstall</c> verb, which its Windows installer
///  runs: git's global <c>core.editor</c> stops naming the app once the app is removed.
/// </summary>
public static class GitEditorUninstall
{
    private const string EditorKey = "core.editor";

    /// <summary>
    ///  Removes the global <c>core.editor</c> when it contains <paramref name="installDir"/> (as git names a path), and
    ///  returns whether it did.
    /// </summary>
    public static async Task<bool> RunAsync(IGitConfigService config, string? installDir)
    {
        string? path = installDir?.ToPosixPath();
        if (string.IsNullOrEmpty(path)
            || !(await config.GetAsync(ConfigScope.Global, EditorKey)).Contains(path,
                StringComparison.InvariantCultureIgnoreCase))
        {
            return false;
        }

        await config.SetAsync(ConfigScope.Global, EditorKey, "");
        return true;
    }
}

using GitCommands;
using GitCommands.Settings;
using GitExtensions.Extensibility.Settings;
using GitExtensions.Xplat.Core.Repository;

namespace GitExtensions.Xplat.Core.Spelling;

/// <summary>
///  The spelling dictionary of a repository as upstream's commit message box reads and changes it (<c>EditNetSpell</c>):
///  upstream's <c>Detached().Dictionary</c> setting, used as is.
/// </summary>
public static class SpellingDictionarySetting
{
    /// <summary>
    ///  Upstream's <c>Settings.Detached().Dictionary</c>: the repository's own setting (local or distributed), else the
    ///  global one, whose default is "en-US".
    /// </summary>
    public static Task<string> LoadAsync(string repositoryPath)
        => Task.Run(() => GitModules.Open(repositoryPath).GetEffectiveSettings().Detached().Dictionary);

    /// <summary>
    ///  Upstream's <c>DicToolStripMenuItemClick</c>: a dictionary chosen in the message box's menu is the repository's local
    ///  setting, so it applies to this repository only.
    /// </summary>
    public static Task SaveLocalAsync(string repositoryPath, string dictionary)
        => Task.Run(() =>
        {
            GitModule module = GitModules.Open(repositoryPath);
            SettingsSource settings = module.GetLocalSettings() as DistributedSettings ?? module.GetEffectiveSettings();
            settings.Detached().Dictionary = dictionary;
        });
}

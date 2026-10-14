namespace GitExtensions.Xplat.Core.Settings;

/// <summary>
///  One entry of upstream's settings tree, as <c>FormSettings.OnRuntimeLoad</c> adds it.
/// </summary>
/// <param name="Page">The upstream page's type name.</param>
/// <param name="Title">The page's title (its designer <c>Text</c>, or the group's name).</param>
/// <param name="Parent">The type name of the page it is added under; null for the root groups.</param>
/// <param name="Image">The name of its image in upstream's <c>Images</c>; null when upstream gives none.</param>
/// <param name="AdaptLightness">True when upstream adapts the image to dark themes.</param>
/// <param name="AsRoot">True when the page is shown on its parent group's node instead of under it.</param>
public sealed record SettingsPageEntry(
    string Page,
    string Title,
    string? Parent,
    string? Image,
    bool AdaptLightness = false,
    bool AsRoot = false);

/// <summary>
///  Upstream's settings tree (<c>FormSettings</c> and <c>SettingsTreeViewUserControl</c>): the pages in upstream's order
///  under its three groups, and its "Type to find" rule.
/// </summary>
public static class SettingsPageTree
{
    public const string GitExtensionsGroup = "GitExtensionsSettingsGroup";
    public const string GitGroup = "GitSettingsGroup";
    public const string PluginsGroup = "PluginsSettingsGroup";

    /// <summary>
    ///  The pages in the order <c>FormSettings.OnRuntimeLoad</c> adds them, including the ones the new shell has no page
    ///  for. The plugins' pages follow under <see cref="PluginsGroup"/>, by title.
    /// </summary>
    public static IReadOnlyList<SettingsPageEntry> Entries { get; } =
    [
        new(GitExtensionsGroup, "Git Extensions", null, "GitExtensionsLogo16"),
        new("ChecklistSettingsPage", "Checklist", GitExtensionsGroup, null, AsRoot: true),
        new("GeneralSettingsPage", "General", GitExtensionsGroup, "GeneralSettings"),
        new("AppearanceSettingsPage", "Appearance", GitExtensionsGroup, "Appearance"),
        new("SortingSettingsPage", "Sorting", "AppearanceSettingsPage", "SortBy"),
        new("ColorsSettingsPage", "Colors", "AppearanceSettingsPage", "Colors"),
        new("AppearanceFontsSettingsPage", "Fonts", "AppearanceSettingsPage", "Font", AdaptLightness: true),
        new("ConsoleStyleSettingsPage", "Console style", "AppearanceSettingsPage", "Console"),
        new("RevisionLinksSettingsPage", "Revision links", GitExtensionsGroup, "Link", AdaptLightness: true),
        new("BuildServerIntegrationSettingsPage", "Build server integration", GitExtensionsGroup, "Integration"),
        new("ScriptsSettingsPage", "Scripts", GitExtensionsGroup, "Console"),
        new("HotkeysSettingsPage", "Hotkeys", GitExtensionsGroup, "Hotkey"),
        new("ShellExtensionSettingsPage", "Shell extension", GitExtensionsGroup, "ShellExtensions"),
        new("AdvancedSettingsPage", "Advanced", GitExtensionsGroup, "AdvancedSettings"),
        new("ConfirmationsSettingsPage", "Confirmations", "AdvancedSettingsPage", "BisectGood"),
        new("DetailedSettingsPage", "Detailed", GitExtensionsGroup, "Settings"),
        new("FormBrowseRepoSettingsPage", "Browse repository window", "DetailedSettingsPage", "BranchFolder"),
        new("CommitDialogSettingsPage", "Commit dialog", "DetailedSettingsPage", "CommitSummary"),
        new("DiffViewerSettingsPage", "Diff viewer", "DetailedSettingsPage", "Diff"),
        new("BlameViewerSettingsPage", "Blame viewer", "DetailedSettingsPage", "Blame"),
        new("SshSettingsPage", "SSH", GitExtensionsGroup, "Key"),
        new(GitGroup, "Git", null, "GitLogo16"),
        new("GitSettingsPage", "Paths", GitGroup, "FolderOpen"),
        new("GitConfigSettingsPage", "Config", GitGroup, "GeneralSettings"),
        new("GitConfigAdvancedSettingsPage", "Advanced", GitGroup, "AdvancedSettings"),
        new("GitRootIntroductionPage", "Git Settings", GitGroup, null, AsRoot: true),
        new(PluginsGroup, "Plugins", null, "Plugin"),
        new("PluginRootIntroductionPage", "Plugins Settings", PluginsGroup, null, AsRoot: true),
    ];

    /// <summary>
    ///  Upstream's search box placeholder (<c>TranslatedStrings.SettingsTypeToFind</c>).
    /// </summary>
    public const string TypeToFind = "Type to find";

    /// <summary>
    ///  True when upstream's search box finds a page for <paramref name="search"/>: the title contains the whole text, or
    ///  each space-separated word is part of one of the page's texts (<paramref name="keywords"/>, the texts of its
    ///  controls). A blank search finds nothing.
    /// </summary>
    public static bool Matches(string search, string title, IEnumerable<string> keywords)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return false;
        }

        string searchFor = search.ToLowerInvariant();
        if (title.Contains(searchFor, StringComparison.InvariantCultureIgnoreCase))
        {
            return true;
        }

        IReadOnlyList<string> pageTexts = [.. keywords];

        // Upstream splits on every space, so a double space adds an empty word, which every text contains.
        return searchFor.Split(' ').All(word =>
            pageTexts.Any(text => text.Contains(word, StringComparison.InvariantCultureIgnoreCase)));
    }
}

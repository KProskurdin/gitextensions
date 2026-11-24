using CommunityToolkit.Mvvm.ComponentModel;
using GitExtensions.Extensibility.Settings;

namespace GitExtensions.Xplat.Core.Settings;

/// <summary>
///  One row of upstream's <c>GitConfigAdvancedSettingsPage</c>: a git config key, its value in an editable list of the
///  values git knows for it (empty for not set), and the link to its documentation.
/// </summary>
public sealed partial class GitConfigAdvancedSetting(string key, string text, IReadOnlyList<string> values) : ObservableObject
{
    // Upstream's _gitConfigDocsBaseUrl.
    private const string DocsBaseUrl = "https://git-scm.com/docs/git-config#Documentation/git-config.txt-";

    public string Key { get; } = key;

    /// <summary>
    ///  Upstream's text with the key after it, as upstream's page adds it when it loads.
    /// </summary>
    public string Text { get; } = text;

    /// <summary>
    ///  Upstream's list: empty (not set), then the values git knows for the key; any other can be typed.
    /// </summary>
    public IReadOnlyList<string> Values { get; } = ["", .. values];

    /// <summary>
    ///  Upstream's help icon: git's documentation of the key.
    /// </summary>
    public string HelpUrl { get; } = DocsBaseUrl + key.Replace(".", "", StringComparison.Ordinal).ToLowerInvariant();

    /// <summary>
    ///  The value at the level shown; empty for not set.
    /// </summary>
    [ObservableProperty]
    public partial string Value { get; set; } = "";

    [ObservableProperty]
    public partial bool IsVisible { get; set; } = true;
}

/// <summary>
///  The view model of upstream's <c>GitConfigAdvancedSettingsPage</c> (Git > Advanced): git config settings at the level the
///  page header chooses (<c>GitConfigBaseSettingsPage</c>), the effective and system ones read only, each in an editable
///  list of its values. As the Config page's (<see cref="GitConfigPageViewModel"/>), the values edited at each level are
///  written there on OK, only those that changed: upstream writes every value shown, which can rewrite a key git spells
///  another way ("yes" for "true").
/// </summary>
public sealed class GitConfigAdvancedPageViewModel
{
    private const string RebaseUpdateRefsKey = "rebase.updaterefs";

    private static readonly string[] _boolValues = ["false", "true"];

    // Upstream's page's settings, texts and values, in its order on the page.
    private static readonly (string Key, string Text, string[] Values)[] _settings =
    [
        ("pull.ff", "Fast-forward behavior when pulling", [.. _boolValues, "only"]),
        ("pull.rebase", "Rebase local branch when pulling (instead of merge)", [.. _boolValues, "merges", "interactive"]),
        ("fetch.prune", "Prune remote branches during fetch", _boolValues),
        ("merge.autostash", "Automatically stash before doing a merge", _boolValues),
        ("rebase.autostash", "Automatically stash before doing a rebase", _boolValues),
        ("rebase.autosquash", "Automatically squash commits when doing an interactive rebase", _boolValues),
        (RebaseUpdateRefsKey, "Rebase also dependent branches", _boolValues),
        ("rerere.enabled", "Reuse recorded resolution of conflicted merges", _boolValues),
        ("rerere.autoupdate", "Automatically apply recorded resolution of conflicted merges", _boolValues),
        ("status.showuntrackedfiles", "Show untracked files", ["no", "normal", "all"]),
        ("core.untrackedcache", "Cache the untracked files in the index", [.. _boolValues, "keep"]),
        ("core.fsmonitor", "Run a file system monitor daemon per working directory", _boolValues),
    ];

    private readonly IGitConfigService _gitConfig;
    private readonly string? _repositoryPath;

    // Each level's values: as read, and as edited on the page.
    private readonly Dictionary<ConfigScope, Dictionary<string, string>> _loaded = [];
    private readonly Dictionary<ConfigScope, Dictionary<string, string>> _edited = [];
    private ConfigScope _scope;

    /// <param name="supportsUpdateRefs">Upstream shows <c>rebase.updateRefs</c> only for a git that has it (2.38).</param>
    /// <param name="translate">Upstream's translation of an English text.</param>
    public GitConfigAdvancedPageViewModel(IGitConfigService gitConfig, string? repositoryPath, bool supportsUpdateRefs,
        Func<string, string>? translate = null)
    {
        _gitConfig = gitConfig;
        _repositoryPath = repositoryPath;
        Settings = [.. _settings.Select(setting => new GitConfigAdvancedSetting(setting.Key,
            $"{(translate ?? (text => text))(setting.Text)} [{setting.Key}]", setting.Values))];
        Setting(RebaseUpdateRefsKey).IsVisible = supportsUpdateRefs;
    }

    public IReadOnlyList<GitConfigAdvancedSetting> Settings { get; }

    /// <summary>
    ///  True once the values of a level are on show; nothing is written before.
    /// </summary>
    public bool IsLoaded { get; private set; }

    /// <summary>
    ///  The row of <paramref name="key"/>.
    /// </summary>
    public GitConfigAdvancedSetting Setting(string key) => Settings.Single(setting => setting.Key == key);

    /// <summary>
    ///  Reads and shows the values at <paramref name="level"/>. The window calls it when it opens, once git can be run.
    /// </summary>
    public async Task LoadAsync(SettingLevel level)
    {
        _scope = ScopeOf(level);
        await ShowAsync();
        IsLoaded = true;
    }

    /// <summary>
    ///  As upstream's <c>SetCurrentSettings</c>: the values stay with the level they were edited at, and the page shows the
    ///  chosen level.
    /// </summary>
    public async Task ChangeLevelAsync(SettingLevel level)
    {
        if (!IsLoaded)
        {
            return;
        }

        KeepEdits();
        _scope = ScopeOf(level);
        await ShowAsync();
    }

    /// <summary>
    ///  Upstream's <c>PageToSettings</c>: writes the values edited at each level that differ from what was read, an empty
    ///  one removing the key so the next level applies; nothing before the values were read.
    /// </summary>
    public async Task SaveAsync()
    {
        if (!IsLoaded)
        {
            return;
        }

        KeepEdits();
        foreach ((ConfigScope scope, Dictionary<string, string> values) in _edited)
        {
            if (scope is ConfigScope.Effective or ConfigScope.System)
            {
                continue;
            }

            foreach ((string key, string value) in values)
            {
                if (!string.Equals(_loaded[scope][key], value, StringComparison.Ordinal))
                {
                    await _gitConfig.SetAsync(scope, key, value, RepositoryOf(scope));

                    // After Apply, the next save compares with what was written.
                    _loaded[scope][key] = value;
                }
            }
        }
    }

    private static ConfigScope ScopeOf(SettingLevel level) => level switch
    {
        SettingLevel.Effective => ConfigScope.Effective,
        SettingLevel.Local => ConfigScope.Local,
        SettingLevel.SystemWide => ConfigScope.System,
        _ => ConfigScope.Global,
    };

    private async Task ShowAsync()
    {
        ConfigScope scope = _scope;
        if (!_edited.TryGetValue(scope, out Dictionary<string, string>? values))
        {
            Dictionary<string, string> loaded = [];
            foreach (GitConfigAdvancedSetting setting in Settings)
            {
                loaded[setting.Key] = await _gitConfig.GetAsync(scope, setting.Key, RepositoryOf(scope));
            }

            _loaded[scope] = loaded;
            values = new Dictionary<string, string>(loaded);
            _edited[scope] = values;
        }

        // A slower read of a level no longer chosen must not overwrite the page.
        if (scope != _scope)
        {
            return;
        }

        foreach (GitConfigAdvancedSetting setting in Settings)
        {
            setting.Value = values[setting.Key];
        }
    }

    // The values on the page, kept for the level they were edited at; the read-only levels keep nothing.
    private void KeepEdits()
    {
        if (_scope is ConfigScope.Effective or ConfigScope.System
            || !_edited.TryGetValue(_scope, out Dictionary<string, string>? values))
        {
            return;
        }

        foreach (GitConfigAdvancedSetting setting in Settings)
        {
            values[setting.Key] = setting.Value ?? "";
        }
    }

    // The repository's own and effective values are read in the repository; the others anywhere.
    private string? RepositoryOf(ConfigScope scope) =>
        scope is ConfigScope.Local or ConfigScope.Effective ? _repositoryPath : null;
}

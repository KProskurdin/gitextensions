using CommunityToolkit.Mvvm.ComponentModel;
using GitExtensions.Extensibility.Settings;

namespace GitExtensions.Xplat.Core.Settings;

/// <summary>
///  One of upstream's three-state check boxes of <c>GitConfigAdvancedSettingsPage</c>: a boolean git config key, checked
///  when git reads it as true, unchecked when false or not set, and neither when it holds something else.
/// </summary>
public sealed partial class GitConfigAdvancedSetting(string key, string text) : ObservableObject
{
    public string Key { get; } = key;

    /// <summary>
    ///  Upstream's text with the key after it, as upstream's page adds it when it loads.
    /// </summary>
    public string Text { get; } = text;

    [ObservableProperty]
    public partial bool? IsChecked { get; set; } = false;

    [ObservableProperty]
    public partial bool IsVisible { get; set; } = true;
}

/// <summary>
///  The view model of upstream's <c>GitConfigAdvancedSettingsPage</c> (Git > Advanced): eight boolean git config settings at
///  the level the page header chooses (<c>GitConfigBaseSettingsPage</c>), the effective and system ones read only. As the
///  Config page's (<see cref="GitConfigPageViewModel"/>), the values edited at each level are written there on OK, only those
///  that changed: upstream writes "false" for every unchecked box, which would add the keys git did not have.
/// </summary>
public sealed class GitConfigAdvancedPageViewModel
{
    // Upstream's page's settings and texts, in its order on the page.
    private static readonly (string Key, string Text)[] _settings =
    [
        ("pull.rebase", "Rebase local branch when pulling (instead of merge)"),
        ("fetch.prune", "Prune remote branches during fetch"),
        ("merge.autostash", "Automatically stash before doing a merge"),
        ("rebase.autostash", "Automatically stash before doing a rebase"),
        ("rebase.autosquash", "Automatically squash commits when doing an interactive rebase"),
        ("rebase.updaterefs", "Rebase also dependent branches"),
        ("rerere.enabled", "Reuse recorded resolution of conflicted merges"),
        ("rerere.autoupdate", "Automatically apply recorded resolution of conflicted merges"),
    ];

    private readonly IGitConfigService _gitConfig;
    private readonly string? _repositoryPath;

    // Each level's states: as read, and as edited on the page.
    private readonly Dictionary<ConfigScope, Dictionary<string, bool?>> _loaded = [];
    private readonly Dictionary<ConfigScope, Dictionary<string, bool?>> _edited = [];
    private ConfigScope _scope;

    /// <param name="supportsUpdateRefs">Upstream shows <c>rebase.updateRefs</c> only for a git that has it (2.38).</param>
    /// <param name="translate">Upstream's translation of an English text.</param>
    public GitConfigAdvancedPageViewModel(IGitConfigService gitConfig, string? repositoryPath, bool supportsUpdateRefs,
        Func<string, string>? translate = null)
    {
        _gitConfig = gitConfig;
        _repositoryPath = repositoryPath;
        Settings = [.. _settings.Select(setting =>
            new GitConfigAdvancedSetting(setting.Key, $"{(translate ?? (text => text))(setting.Text)} [{setting.Key}]"))];
        RebaseUpdateRefs.IsVisible = supportsUpdateRefs;
    }

    public IReadOnlyList<GitConfigAdvancedSetting> Settings { get; }

    public GitConfigAdvancedSetting PullRebase => Settings[0];

    public GitConfigAdvancedSetting FetchPrune => Settings[1];

    public GitConfigAdvancedSetting MergeAutoStash => Settings[2];

    public GitConfigAdvancedSetting RebaseAutoStash => Settings[3];

    public GitConfigAdvancedSetting RebaseAutoSquash => Settings[4];

    public GitConfigAdvancedSetting RebaseUpdateRefs => Settings[5];

    public GitConfigAdvancedSetting RerereEnabled => Settings[6];

    public GitConfigAdvancedSetting RerereAutoUpdate => Settings[7];

    /// <summary>
    ///  True once the values of a level are on show; nothing is written before.
    /// </summary>
    public bool IsLoaded { get; private set; }

    /// <summary>
    ///  Upstream's <c>SettingsToPage</c>: git's value as a check box state.
    /// </summary>
    public static bool? StateOf(string value) => value.Trim().ToLowerInvariant() switch
    {
        "true" or "yes" or "on" or "1" => true,
        "false" or "no" or "off" or "0" or "" => false,
        _ => null,
    };

    /// <summary>
    ///  Upstream's <c>PageToSettings</c>: a state as git's value; "" removes the key, so the next level applies.
    /// </summary>
    public static string ValueOf(bool? state) => state switch
    {
        true => "true",
        false => "false",
        null => "",
    };

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
    ///  As upstream's <c>SetCurrentSettings</c>: the states stay with the level they were edited at, and the page shows the
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
    ///  Writes the states edited at each level that differ from what was read; nothing before the values were read.
    /// </summary>
    public async Task SaveAsync()
    {
        if (!IsLoaded)
        {
            return;
        }

        KeepEdits();
        foreach ((ConfigScope scope, Dictionary<string, bool?> states) in _edited)
        {
            if (scope is ConfigScope.Effective or ConfigScope.System)
            {
                continue;
            }

            foreach ((string key, bool? state) in states)
            {
                if (_loaded[scope][key] != state)
                {
                    await _gitConfig.SetAsync(scope, key, ValueOf(state), RepositoryOf(scope));

                    // After Apply, the next save compares with what was written.
                    _loaded[scope][key] = state;
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
        if (!_edited.TryGetValue(scope, out Dictionary<string, bool?>? states))
        {
            Dictionary<string, bool?> loaded = [];
            foreach (GitConfigAdvancedSetting setting in Settings)
            {
                loaded[setting.Key] = StateOf(await _gitConfig.GetAsync(scope, setting.Key, RepositoryOf(scope)));
            }

            _loaded[scope] = loaded;
            states = new Dictionary<string, bool?>(loaded);
            _edited[scope] = states;
        }

        // A slower read of a level no longer chosen must not overwrite the page.
        if (scope != _scope)
        {
            return;
        }

        foreach (GitConfigAdvancedSetting setting in Settings)
        {
            setting.IsChecked = states[setting.Key];
        }
    }

    // The states on the page, kept for the level they were edited at; the read-only levels keep nothing.
    private void KeepEdits()
    {
        if (_scope is ConfigScope.Effective or ConfigScope.System
            || !_edited.TryGetValue(_scope, out Dictionary<string, bool?>? states))
        {
            return;
        }

        foreach (GitConfigAdvancedSetting setting in Settings)
        {
            states[setting.Key] = setting.IsChecked;
        }
    }

    // The repository's own and effective values are read in the repository; the others anywhere.
    private string? RepositoryOf(ConfigScope scope) =>
        scope is ConfigScope.Local or ConfigScope.Effective ? _repositoryPath : null;
}

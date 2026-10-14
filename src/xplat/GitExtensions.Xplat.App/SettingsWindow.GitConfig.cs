using Avalonia.Controls;
using GitExtensions.Extensibility.Settings;
using GitExtensions.Xplat.Core.Settings;

namespace GitExtensions.Xplat.App;

// Upstream's GitConfigSettingsPage: git's own settings at the level its header chooses (GitConfigBaseSettingsPage), the
// effective and system values read only. The values edited at each level are written there on OK, only the changed ones,
// so a setting made elsewhere since the window opened is kept.
public partial class SettingsWindow
{
    private const string CredentialHelperKey = "credential.helper";
    private const string AutoCrlfKey = "core.autocrlf";

    private const string MergeToolKey = "merge.guitool";
    private const string DiffToolKey = "diff.guitool";

    private static readonly string[] _gitConfigTextKeys =
        ["user.name", "user.email", CredentialHelperKey, "core.editor", MergeToolKey, DiffToolKey, "commit.template"];

    // Upstream's ConfiguredMergeTool: the GUI tool, else the plain one; the diff tool the same way, as the browse window
    // starts it. A new choice is written to the GUI tool's key, as upstream's page does.
    private static readonly Dictionary<string, string> _toolFallbackKeys =
        new() { [MergeToolKey] = "merge.tool", [DiffToolKey] = "diff.tool" };

    // Upstream's line ending choices, with their texts; "Not set" removes the key.
    private static readonly AutoCrlfOption[] _autoCrlfOptions =
    [
        new("true", "Checkout Windows-style, commit Unix-style line endings (\"core.autocrlf\"  is set to \"true\")"),
        new("input", "Checkout as-is, commit Unix-style line endings (\"core.autocrlf\"  is set to \"input\")"),
        new("false", "Checkout as-is, commit as-is (\"core.autocrlf\"  is set to \"false\")"),
        new("", "Not set"),
    ];

    // The page's values per level: read from git when the level is first shown, then as edited.
    private readonly Dictionary<ConfigScope, Dictionary<string, string>> _gitConfigValues = [];

    // The levels whose credential.helper has several values: shown, not edited, as upstream.
    private readonly HashSet<ConfigScope> _multipleCredentialHelpers = [];
    private ConfigScope _gitConfigScope;

    /// <summary>
    ///  True once the git config values are on show; saving before that would write blanks over them.
    /// </summary>
    public bool IsGitConfigLoaded { get; private set; }

    private Dictionary<string, Control> GitConfigBoxes => new()
    {
        ["user.name"] = UserNameBox,
        ["user.email"] = UserEmailBox,
        [CredentialHelperKey] = CredentialHelperBox,
        ["core.editor"] = EditorBox,
        [MergeToolKey] = MergeToolBox,
        [DiffToolKey] = DiffToolBox,
        ["commit.template"] = CommitTemplateBox,
    };

    private RadioButton[] AutoCrlfRadios =>
        [AutoCrlfTrueRadio, AutoCrlfInputRadio, AutoCrlfFalseRadio, AutoCrlfNotSetRadio];

    private void ShowGitConfigPage()
    {
        foreach ((RadioButton radio, AutoCrlfOption option) in AutoCrlfRadios.Zip(_autoCrlfOptions))
        {
            radio.Content = option.Text;
        }

        BrowseCommitTemplateButton.Click +=
            (_, _) => Browse(() => BrowseFileAsync(CommitTemplateBox, "Path to commit template"));
        Opened += (_, _) => UiActions.Run(LoadGitConfigAsync, ex => ErrorText.Text = ex.Message);
    }

    private static ConfigScope ScopeOf(SettingLevel level) => level switch
    {
        SettingLevel.Effective => ConfigScope.Effective,
        SettingLevel.Local => ConfigScope.Local,
        SettingLevel.SystemWide => ConfigScope.System,
        _ => ConfigScope.Global,
    };

    private async Task LoadGitConfigAsync()
    {
        SaveButton.IsEnabled = ApplyButton.IsEnabled = false;
        _gitConfigScope = ScopeOf(SourceHeaderOf(GitConfigTab).Level);
        await ShowGitConfigAsync();
        IsGitConfigLoaded = true;
        SaveButton.IsEnabled = ApplyButton.IsEnabled = true;

        // Read after the values, so a slow git does not hold back the rest of the page.
        MergeToolBox.ItemsSource = await AppServices.DiffMergeTools.GetAvailableAsync(diff: false);
        DiffToolBox.ItemsSource = await AppServices.DiffMergeTools.GetAvailableAsync(diff: true);
        CredentialHelperBox.ItemsSource = await Task.Run(GitConfigChoices.CredentialHelpers);
        EditorBox.ItemsSource = await Task.Run(GitConfigChoices.Editors);
    }

    // As upstream's SetCurrentSettings: the page's values stay with the level they were edited at, and the page shows the
    // chosen level.
    private void ChangeGitConfigLevel(SettingLevel level)
    {
        if (!IsGitConfigLoaded)
        {
            return;
        }

        KeepGitConfigEdits();
        _gitConfigScope = ScopeOf(level);
        UiActions.Run(ShowGitConfigAsync, ex => ErrorText.Text = ex.Message);
    }

    private async Task ShowGitConfigAsync()
    {
        ConfigScope scope = _gitConfigScope;
        if (!_gitConfigValues.TryGetValue(scope, out Dictionary<string, string>? values))
        {
            values = [];
            foreach (string key in _gitConfigTextKeys.Append(AutoCrlfKey))
            {
                values[key] = await LoadAsync(scope, key);
                if (values[key].Length == 0 && _toolFallbackKeys.TryGetValue(key, out string? fallbackKey))
                {
                    // Shown as the tool in use; written to the GUI tool's key only when changed.
                    values[key] = _loaded[(scope, key)] = await LoadAsync(scope, fallbackKey);
                }
            }

            // As upstream, a credential.helper with several values (a list git runs in turn) is shown but not edited: git
            // config cannot set one value over several.
            if (scope is not ConfigScope.Effective &&
                await _gitConfig.GetAllAsync(scope, CredentialHelperKey, RepositoryOf(scope)) is { Count: > 1 } helpers)
            {
                values[CredentialHelperKey] = string.Join(", ", helpers);
                _multipleCredentialHelpers.Add(scope);
            }

            _gitConfigValues[scope] = values;
        }

        if (scope != _gitConfigScope)
        {
            return;
        }

        foreach ((string key, Control box) in GitConfigBoxes)
        {
            SetText(box, values[key]);
        }

        AutoCrlfOption autoCrlf = _autoCrlfOptions.FirstOrDefault(option =>
            option.Value.Equals(values[AutoCrlfKey], StringComparison.OrdinalIgnoreCase)) ?? _autoCrlfOptions[^1];
        AutoCrlfRadios[Array.IndexOf(_autoCrlfOptions, autoCrlf)].IsChecked = true;

        // Upstream hides the credential helper of the effective settings, which can only give its last value.
        lblCredentialHelper.IsVisible = CredentialHelperBox.IsVisible = scope != ConfigScope.Effective;
        CredentialHelperBox.IsEnabled = !_multipleCredentialHelpers.Contains(scope);
    }

    // The values on the page, kept for the level they were edited at; the read-only levels keep nothing.
    private void KeepGitConfigEdits()
    {
        if (_gitConfigScope is ConfigScope.Effective or ConfigScope.System ||
            !_gitConfigValues.TryGetValue(_gitConfigScope, out Dictionary<string, string>? values))
        {
            return;
        }

        foreach ((string key, Control box) in GitConfigBoxes)
        {
            if (key != CredentialHelperKey || !_multipleCredentialHelpers.Contains(_gitConfigScope))
            {
                values[key] = (TextOf(box) ?? "").Trim();
            }
        }

        values[AutoCrlfKey] = _autoCrlfOptions[
            Array.FindIndex(AutoCrlfRadios, radio => radio.IsChecked == true) is var index and >= 0
                ? index
                : _autoCrlfOptions.Length - 1].Value;
    }

    private async Task SaveGitConfigAsync()
    {
        if (!IsGitConfigLoaded)
        {
            return;
        }

        KeepGitConfigEdits();
        foreach ((ConfigScope scope, Dictionary<string, string> values) in _gitConfigValues)
        {
            if (scope is ConfigScope.Effective or ConfigScope.System)
            {
                continue;
            }

            foreach ((string key, string value) in values)
            {
                if (key != CredentialHelperKey || !_multipleCredentialHelpers.Contains(scope))
                {
                    await SaveIfChangedAsync(scope, key, value);
                }
            }
        }
    }

    private async Task<string> LoadAsync(ConfigScope scope, string key)
    {
        string value = await _gitConfig.GetAsync(scope, key, RepositoryOf(scope));
        _loaded[(scope, key)] = value;
        return value;
    }

    private async Task SaveIfChangedAsync(ConfigScope scope, string key, string value)
    {
        if (_loaded.TryGetValue((scope, key), out string? loaded) && loaded == value)
        {
            return;
        }

        await _gitConfig.SetAsync(scope, key, value, RepositoryOf(scope));

        // After Apply, the next save compares with what was written.
        _loaded[(scope, key)] = value;
    }

    // The repository's own and effective values are read in the repository; the others anywhere.
    private string? RepositoryOf(ConfigScope scope) =>
        scope is ConfigScope.Local or ConfigScope.Effective ? _repositoryPath : null;

    private static string? TextOf(Control box) => box switch
    {
        TextBox text => text.Text,
        ComboBox combo => combo.Text,
        _ => null,
    };

    private static void SetText(Control box, string value)
    {
        switch (box)
        {
            case TextBox text:
                text.Text = value;
                break;
            case ComboBox combo:
                combo.Text = value;
                break;
        }
    }
}

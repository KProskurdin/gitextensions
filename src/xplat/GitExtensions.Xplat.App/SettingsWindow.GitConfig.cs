using System.Text;
using Avalonia;
using Avalonia.Controls;
using GitCommands;
using GitCommands.Config;
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

    // The pause after the last key before the git command in the Paths page's box is tried.
    private static readonly TimeSpan _gitCommandCheckDelay = TimeSpan.FromMilliseconds(400);

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

    // What each level's tool rows hold, by level and tool kind.
    private readonly Dictionary<(ConfigScope Scope, bool Diff), ToolRowEdit> _toolRows = [];
    private bool _showingToolRows;
    private int _gitCommandChecks;
    private readonly TaskCompletionSource _gitConfigListsLoaded = new();

    /// <summary>
    ///  True once the git config values are on show; saving before that would write blanks over them.
    /// </summary>
    public bool IsGitConfigLoaded { get; private set; }

    /// <summary>
    ///  Completes when the page's drop-down lists are filled, for tests.
    /// </summary>
    public Task GitConfigListsLoaded => _gitConfigListsLoaded.Task;

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
        ConfigureEncodingButton.Click += (_, _) => Browse(ConfigureEncodingsAsync);
        ShowEncodings();
        WireToolRow(diff: false);
        WireToolRow(diff: true);
        EnableToolRow(diff: false);
        EnableToolRow(diff: true);

        // Upstream's GitPath_TextChanged: what is typed is tried, and the Git config page follows it.
        GitCommandBox.TextChanged += (_, _) => ScheduleGitCommandCheck();
        Opened += (_, _) => UiActions.Run(LoadGitConfigAsync, ex =>
        {
            ErrorText.Text = ex.Message;

            // The git command can be corrected only if the window can still be saved.
            SaveButton.IsEnabled = ApplyButton.IsEnabled = true;
        });
    }

    private void WireToolRow(bool diff)
    {
        ToolRowControls row = RowOf(diff);
        row.Tool.GetObservable(ComboBox.TextProperty).Subscribe(new ToolNameObserver(() => ShowToolChosen(diff)));
        row.Browse.Click += (_, _) => Browse(() => BrowseToolAsync(diff));
        row.Suggest.Click += (_, _) => SuggestToolCommand(diff);

        // Upstream's LostFocus and TextChanged of the path boxes.
        row.Path.LostFocus += (_, _) => row.Path.Text = row.Path.Text?.ToPosixPath();
        row.Path.TextChanged += (_, _) =>
        {
            string tool = (row.Tool.Text ?? "").Trim();
            if (!_showingToolRows && row.Path.IsFocused && DiffMergeToolSettings.IsRegistered(tool, diff))
            {
                SuggestToolCommand(diff);
            }
        };
    }

    private ToolRowControls RowOf(bool diff) => diff
        ? new ToolRowControls(DiffToolBox, DiffToolPathBox, BrowseDiffToolButton, DiffToolCommandBox, SuggestDiffToolButton)
        : new ToolRowControls(MergeToolBox, MergeToolPathBox, BrowseMergeToolButton, MergeToolCommandBox, SuggestMergeToolButton);

    // Upstream enables a tool's rows only when a tool is named.
    private void EnableToolRow(bool diff)
    {
        ToolRowControls row = RowOf(diff);
        row.Path.IsEnabled = row.Browse.IsEnabled = row.Command.IsEnabled = row.Suggest.IsEnabled =
            !string.IsNullOrWhiteSpace(row.Tool.Text);
    }

    // Upstream's cboDiffTool_TextChanged and cboMergeTool_TextChanged: the rows follow the tool.
    private void ShowToolChosen(bool diff)
    {
        EnableToolRow(diff);
        if (!_showingToolRows && IsGitConfigLoaded)
        {
            ToolRowControls row = RowOf(diff);
            string tool = (row.Tool.Text ?? "").Trim();
            UiActions.Run(() => ShowToolRowAsync(diff, tool), ex => ErrorText.Text = ex.Message);
        }
    }

    private async Task ShowToolRowAsync(bool diff, string tool)
    {
        ConfigScope scope = _gitConfigScope;
        DiffMergeToolRow read = await DiffMergeToolSettings.ReadAsync(_gitConfig, scope, tool, diff, RepositoryOf(scope));
        ToolRowControls row = RowOf(diff);

        // A slower read of a tool that is no longer chosen, or of another level, must not overwrite the rows.
        if (scope == _gitConfigScope && (row.Tool.Text ?? "").Trim() == tool)
        {
            _showingToolRows = true;
            row.Path.Text = read.Path;
            row.Command.Text = read.Command;
            _showingToolRows = false;
        }
    }

    private void SuggestToolCommand(bool diff)
    {
        ToolRowControls row = RowOf(diff);
        row.Command.Text = DiffMergeToolSettings.SuggestCommand((row.Tool.Text ?? "").Trim(), diff, row.Path.Text);
    }

    // Upstream's btnDiffToolBrowse_Click and btnMergeToolBrowse_Click, which also suggest the command for the new path.
    private async Task BrowseToolAsync(bool diff)
    {
        ToolRowControls row = RowOf(diff);
        await BrowseFileAsync(row.Path, row.Tool.Text ?? "");
        SuggestToolCommand(diff);
    }

    private void ShowEncodings()
        => FilesEncodingBox.ItemsSource = AppSettings.AvailableEncodings.Values.Select(encoding => encoding.EncodingName).ToList();

    // Upstream's ConfigureEncoding_Click.
    private async Task ConfigureEncodingsAsync()
    {
        string? selected = FilesEncodingBox.SelectedItem as string;
        if (await new AvailableEncodingsWindow().ShowDialog<bool>(this))
        {
            ShowEncodings();
            FilesEncodingBox.SelectedItem = selected;
        }
    }

    private static string? EncodingNameOf(string webName)
        => AppSettings.AvailableEncodings.TryGetValue(webName.ToLowerInvariant(), out Encoding? encoding) ? encoding.EncodingName : null;

    private void ScheduleGitCommandCheck()
    {
        int check = ++_gitCommandChecks;
        _ = Task.Delay(_gitCommandCheckDelay).ContinueWith(
            _ => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (check == _gitCommandChecks)
                {
                    UiActions.Run(async () => await CheckGitCommandAsync(), ex => ErrorText.Text = ex.Message);
                }
            }),
            TaskScheduler.Default);
    }

    private async Task<bool> CheckGitCommandAsync()
    {
        int check = _gitCommandChecks;
        bool found = await AppServices.CanFindGit(GitCommandBox.Text);
        if (check == _gitCommandChecks)
        {
            ShowGitFound(found);
        }

        return found;
    }

    // Upstream's OnPageShown: without git the page says so and its git settings cannot be edited.
    private void ShowGitFound(bool found)
    {
        InvalidGitPathText.IsVisible = !found;
        UserNameBox.IsEnabled = UserEmailBox.IsEnabled = EditorBox.IsEnabled = CommitTemplateBox.IsEnabled =
            BrowseCommitTemplateButton.IsEnabled = MergeToolBox.IsEnabled = found;
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
        if (!await CheckGitCommandAsync())
        {
            // Nothing to read, and upstream saves none of it: the rest of the settings, and git's path, can still be saved.
            SaveButton.IsEnabled = ApplyButton.IsEnabled = true;
            return;
        }

        _gitConfigScope = ScopeOf(SourceHeaderOf(GitConfigTab).Level);
        await ShowGitConfigAsync();
        IsGitConfigLoaded = true;
        SaveButton.IsEnabled = ApplyButton.IsEnabled = true;

        // Read after the values, so a slow git does not hold back the rest of the page.
        MergeToolBox.ItemsSource = await AppServices.DiffMergeTools.GetAvailableAsync(diff: false);
        DiffToolBox.ItemsSource = await AppServices.DiffMergeTools.GetAvailableAsync(diff: true);
        CredentialHelperBox.ItemsSource = await Task.Run(GitConfigChoices.CredentialHelpers);
        EditorBox.ItemsSource = await Task.Run(GitConfigChoices.Editors);
        _gitConfigListsLoaded.TrySetResult();
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
            foreach (string key in _gitConfigTextKeys.Append(AutoCrlfKey).Append(SettingKeyString.FilesEncoding))
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

        _showingToolRows = true;
        foreach ((string key, Control box) in GitConfigBoxes)
        {
            SetText(box, values[key]);
        }

        _showingToolRows = false;
        FilesEncodingBox.SelectedItem = EncodingNameOf(values[SettingKeyString.FilesEncoding]);
        await ShowToolRowsAsync(scope, values, diff: false);
        await ShowToolRowsAsync(scope, values, diff: true);

        AutoCrlfOption autoCrlf = _autoCrlfOptions.FirstOrDefault(option =>
            option.Value.Equals(values[AutoCrlfKey], StringComparison.OrdinalIgnoreCase)) ?? _autoCrlfOptions[^1];
        AutoCrlfRadios[Array.IndexOf(_autoCrlfOptions, autoCrlf)].IsChecked = true;

        // Upstream hides the credential helper of the effective settings, which can only give its last value.
        lblCredentialHelper.IsVisible = CredentialHelperBox.IsVisible = scope != ConfigScope.Effective;
        CredentialHelperBox.IsEnabled = !_multipleCredentialHelpers.Contains(scope);
    }

    private async Task ShowToolRowsAsync(ConfigScope scope, Dictionary<string, string> values, bool diff)
    {
        string tool = values[diff ? DiffToolKey : MergeToolKey];
        if (!_toolRows.TryGetValue((scope, diff), out ToolRowEdit? edit))
        {
            DiffMergeToolRow read = await DiffMergeToolSettings.ReadAsync(_gitConfig, scope, tool, diff, RepositoryOf(scope));
            edit = new ToolRowEdit(tool, read.Path, read.Command);
            _toolRows[(scope, diff)] = edit;
        }

        if (scope == _gitConfigScope)
        {
            ToolRowControls row = RowOf(diff);
            _showingToolRows = true;
            row.Path.Text = edit.Path;
            row.Command.Text = edit.Command;
            _showingToolRows = false;
            EnableToolRow(diff);
        }
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

        // A stored encoding that is not on the list shows nothing selected, and stays as it is.
        if (FilesEncodingBox.SelectedItem is string encodingName
            && AppSettings.AvailableEncodings.Values.FirstOrDefault(encoding => encoding.EncodingName == encodingName) is { } chosen)
        {
            values[SettingKeyString.FilesEncoding] = chosen.WebName;
        }

        KeepToolRow(diff: false);
        KeepToolRow(diff: true);
    }

    private void KeepToolRow(bool diff)
    {
        if (_toolRows.TryGetValue((_gitConfigScope, diff), out ToolRowEdit? edit))
        {
            ToolRowControls row = RowOf(diff);
            edit.Tool = (row.Tool.Text ?? "").Trim();
            edit.Path = (row.Path.Text ?? "").Trim();
            edit.Command = (row.Command.Text ?? "").Trim();
        }
    }

    // Upstream writes both on every save, which would turn what it found on this computer into the user's setting. So a tool
    // that stayed writes only what was edited; a newly chosen tool writes both, as upstream, since git may not know it.
    private async Task SaveToolRowsAsync()
    {
        foreach (((ConfigScope scope, bool diff), ToolRowEdit edit) in _toolRows)
        {
            if (scope is ConfigScope.Effective or ConfigScope.System || edit.Tool.Length == 0)
            {
                continue;
            }

            bool newTool = edit.Tool != edit.SavedTool;
            if (newTool || edit.Path != edit.SavedPath)
            {
                await _gitConfig.SetAsync(scope, DiffMergeToolSettings.PathKey(diff, edit.Tool),
                    DiffMergeToolSettings.ToGitSetting(edit.Path), RepositoryOf(scope));
            }

            if (newTool || edit.Command != edit.SavedCommand)
            {
                await _gitConfig.SetAsync(scope, DiffMergeToolSettings.CommandKey(diff, edit.Tool),
                    DiffMergeToolSettings.ToGitSetting(edit.Command), RepositoryOf(scope));
            }

            edit.MarkSaved();
        }
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

        await SaveToolRowsAsync();
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

/// <summary>
///  A level's values of a tool's rows, and what was last read or saved, to write only what changed.
/// </summary>
internal sealed class ToolRowEdit(string tool, string path, string command)
{
    public string Tool { get; set; } = tool;

    public string Path { get; set; } = path;

    public string Command { get; set; } = command;

    public string SavedTool { get; private set; } = tool;

    public string SavedPath { get; private set; } = path;

    public string SavedCommand { get; private set; } = command;

    public void MarkSaved()
    {
        SavedTool = Tool;
        SavedPath = Path;
        SavedCommand = Command;
    }
}

/// <summary>
///  The controls of one tool's rows on the Git config page.
/// </summary>
internal sealed record ToolRowControls(ComboBox Tool, TextBox Path, Button Browse, TextBox Command, Button Suggest);

/// <summary>
///  Runs an action each time a tool box's text changes.
/// </summary>
internal sealed class ToolNameObserver(Action changed) : IObserver<string?>
{
    public void OnCompleted()
    {
    }

    public void OnError(Exception error)
    {
    }

    public void OnNext(string? value) => changed();
}

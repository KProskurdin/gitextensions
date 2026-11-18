using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitCommands;
using GitCommands.Config;
using GitExtensions.Extensibility.Settings;

namespace GitExtensions.Xplat.Core.Settings;

/// <summary>
///  One of upstream's <c>core.autocrlf</c> choices; an empty value is "Not set".
/// </summary>
public sealed record AutoCrlfOption(string Value, string Text)
{
    public override string ToString() => Text;
}

/// <summary>
///  A level's values of a tool's rows, and what was last read or saved, to write only what changed.
/// </summary>
public sealed class ToolRowEdit(string tool, string path, string command)
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
///  The view model of upstream's <c>GitConfigSettingsPage</c>: git's own settings at the level its header chooses
///  (<c>GitConfigBaseSettingsPage</c>), the effective and system values read only. The values edited at each level are
///  written there on OK, only the changed ones, so a setting made elsewhere since the window opened is kept.
/// </summary>
public sealed partial class GitConfigPageViewModel : ObservableObject
{
    public const string CredentialHelperKey = "credential.helper";
    public const string AutoCrlfKey = "core.autocrlf";
    public const string MergeToolKey = "merge.guitool";
    public const string DiffToolKey = "diff.guitool";
    private const string UserNameKey = "user.name";
    private const string UserEmailKey = "user.email";
    private const string EditorKey = "core.editor";
    private const string CommitTemplateKey = "commit.template";

    // Upstream's core.autocrlf values; "Not set" removes the key.
    public const string AutoCrlfTrue = "true";
    public const string AutoCrlfInput = "input";
    public const string AutoCrlfFalse = "false";
    public const string AutoCrlfNotSet = "";

    // Upstream's line ending choices, with their texts.
    public static readonly IReadOnlyList<AutoCrlfOption> AutoCrlfOptions =
    [
        new(AutoCrlfTrue, "Checkout Windows-style, commit Unix-style line endings (\"core.autocrlf\"  is set to \"true\")"),
        new(AutoCrlfInput, "Checkout as-is, commit Unix-style line endings (\"core.autocrlf\"  is set to \"input\")"),
        new(AutoCrlfFalse, "Checkout as-is, commit as-is (\"core.autocrlf\"  is set to \"false\")"),
        new(AutoCrlfNotSet, "Not set"),
    ];

    // The pause after the last key before the git command in the Paths page's box is tried.
    private static readonly TimeSpan _gitCommandCheckDelay = TimeSpan.FromMilliseconds(400);

    private static readonly string[] _textKeys =
        [UserNameKey, UserEmailKey, CredentialHelperKey, EditorKey, MergeToolKey, DiffToolKey, CommitTemplateKey];

    // Upstream's ConfiguredMergeTool: the GUI tool, else the plain one; the diff tool the same way, as the browse window
    // starts it. A new choice is written to the GUI tool's key, as upstream's page does.
    private static readonly Dictionary<string, string> _toolFallbackKeys =
        new() { [MergeToolKey] = "merge.tool", [DiffToolKey] = "diff.tool" };

    private readonly IGitConfigService _gitConfig;
    private readonly string? _repositoryPath;
    private readonly Func<string?> _gitCommand;
    private readonly Func<string?, Task<bool>> _canFindGit;
    private readonly IDiffMergeToolCatalog _tools;
    private readonly Action<string> _reportError;

    // The page's values per level: read from git when the level is first shown, then as edited.
    private readonly Dictionary<ConfigScope, Dictionary<string, string>> _values = [];

    // The values as read or last written, to write only what changed.
    private readonly Dictionary<(ConfigScope, string), string> _loaded = [];

    // The levels whose credential.helper has several values: shown, not edited, as upstream.
    private readonly HashSet<ConfigScope> _multipleCredentialHelpers = [];

    // What each level's tool rows hold, by level and tool kind.
    private readonly Dictionary<(ConfigScope Scope, bool Diff), ToolRowEdit> _toolRows = [];
    private readonly TaskCompletionSource _listsLoaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private ConfigScope _scope;
    private bool _showing;
    private int _gitCommandChecks;

    /// <param name="gitCommand">The git command the Paths page's box holds.</param>
    /// <param name="canFindGit">Whether a git command can be run.</param>
    /// <param name="reportError">Shows an error of a read that the page started on its own.</param>
    public GitConfigPageViewModel(IGitConfigService gitConfig, string? repositoryPath, Func<string?> gitCommand,
        Func<string?, Task<bool>> canFindGit, IDiffMergeToolCatalog tools, Action<string> reportError)
    {
        _gitConfig = gitConfig;
        _repositoryPath = repositoryPath;
        _gitCommand = gitCommand;
        _canFindGit = canFindGit;
        _tools = tools;
        _reportError = reportError;
        Encodings = EncodingNames();
    }

    [ObservableProperty]
    public partial string? UserName { get; set; }

    [ObservableProperty]
    public partial string? UserEmail { get; set; }

    [ObservableProperty]
    public partial string? CredentialHelper { get; set; }

    [ObservableProperty]
    public partial string? Editor { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMergeTool))]
    public partial string? MergeTool { get; set; }

    [ObservableProperty]
    public partial string? MergeToolPath { get; set; }

    [ObservableProperty]
    public partial string? MergeToolCommand { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDiffTool))]
    public partial string? DiffTool { get; set; }

    [ObservableProperty]
    public partial string? DiffToolPath { get; set; }

    [ObservableProperty]
    public partial string? DiffToolCommand { get; set; }

    [ObservableProperty]
    public partial string? CommitTemplate { get; set; }

    /// <summary>
    ///  The files encoding by its name in the list; a stored encoding that is not on the list shows nothing selected.
    /// </summary>
    [ObservableProperty]
    public partial string? FilesEncoding { get; set; }

    /// <summary>
    ///  The <c>core.autocrlf</c> value chosen (<see cref="AutoCrlfOptions"/>).
    /// </summary>
    [ObservableProperty]
    public partial string AutoCrlf { get; set; } = "";

    [ObservableProperty]
    public partial IReadOnlyList<string> Encodings { get; private set; }

    [ObservableProperty]
    public partial IReadOnlyList<string> MergeTools { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<string> DiffTools { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<string> CredentialHelpers { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<string> Editors { get; private set; } = [];

    /// <summary>
    ///  Upstream's OnPageShown: without git the page says so and its git settings cannot be edited.
    /// </summary>
    [ObservableProperty]
    public partial bool IsGitFound { get; private set; } = true;

    /// <summary>
    ///  True while the values are read; the window cannot be saved then, which would write blanks over them.
    /// </summary>
    [ObservableProperty]
    public partial bool IsLoading { get; private set; }

    /// <summary>
    ///  True once the git config values are on show.
    /// </summary>
    [ObservableProperty]
    public partial bool IsLoaded { get; private set; }

    /// <summary>
    ///  Upstream hides the credential helper of the effective settings, which can only give its last value.
    /// </summary>
    [ObservableProperty]
    public partial bool ShowsCredentialHelper { get; private set; } = true;

    /// <summary>
    ///  A credential.helper with several values is shown but not edited.
    /// </summary>
    [ObservableProperty]
    public partial bool CanEditCredentialHelper { get; private set; } = true;

    /// <summary>
    ///  Upstream enables a tool's rows only when a tool is named.
    /// </summary>
    public bool HasMergeTool => !string.IsNullOrWhiteSpace(MergeTool);

    public bool HasDiffTool => !string.IsNullOrWhiteSpace(DiffTool);

    /// <summary>
    ///  Completes when the page's drop-down lists are filled, for tests.
    /// </summary>
    public Task ListsLoaded => _listsLoaded.Task;

    /// <summary>
    ///  Reads the values at <paramref name="level"/> when git can be run, then the lists. The window calls it when it opens,
    ///  and again after Apply when it could not read them.
    /// </summary>
    public async Task LoadAsync(SettingLevel level)
    {
        IsLoading = true;
        try
        {
            if (!await CheckGitCommandAsync())
            {
                // Nothing to read, and upstream saves none of it: the rest of the settings, and git's path, can still be
                // saved.
                return;
            }

            _scope = ScopeOf(level);
            await ShowAsync();
            IsLoaded = true;
        }
        finally
        {
            IsLoading = false;
        }

        // Read after the values, so a slow git does not hold back the rest of the page.
        MergeTools = await _tools.GetAvailableAsync(diff: false);
        DiffTools = await _tools.GetAvailableAsync(diff: true);
        CredentialHelpers = await Task.Run(GitConfigChoices.CredentialHelpers);
        Editors = await Task.Run(GitConfigChoices.Editors);
        _listsLoaded.TrySetResult();
    }

    /// <summary>
    ///  As upstream's SetCurrentSettings: the page's values stay with the level they were edited at, and the page shows the
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
    ///  Upstream's GitPath_TextChanged: the git command typed is tried after a pause, and the page follows it.
    /// </summary>
    public async Task GitCommandChangedAsync()
    {
        int check = ++_gitCommandChecks;
        await Task.Delay(_gitCommandCheckDelay);
        if (check == _gitCommandChecks)
        {
            await CheckGitCommandAsync();
        }
    }

    // The Suggest buttons.
    [RelayCommand]
    private void Suggest(bool diff) => SuggestToolCommand(diff);

    /// <summary>
    ///  Upstream's suggested command line for the tool and its path.
    /// </summary>
    public void SuggestToolCommand(bool diff)
    {
        if (diff)
        {
            DiffToolCommand = DiffMergeToolSettings.SuggestCommand((DiffTool ?? "").Trim(), diff, DiffToolPath);
        }
        else
        {
            MergeToolCommand = DiffMergeToolSettings.SuggestCommand((MergeTool ?? "").Trim(), diff, MergeToolPath);
        }
    }

    /// <summary>
    ///  Upstream's path box TextChanged: a path typed for a tool upstream knows suggests its command line.
    /// </summary>
    public void ToolPathTyped(bool diff)
    {
        if (!_showing && DiffMergeToolSettings.IsRegistered(((diff ? DiffTool : MergeTool) ?? "").Trim(), diff))
        {
            SuggestToolCommand(diff);
        }
    }

    /// <summary>
    ///  Upstream's path box LostFocus: the path in git's form.
    /// </summary>
    public void NormaliseToolPath(bool diff)
    {
        if (diff)
        {
            DiffToolPath = DiffToolPath?.ToPosixPath();
        }
        else
        {
            MergeToolPath = MergeToolPath?.ToPosixPath();
        }
    }

    /// <summary>
    ///  The encodings list read again after the available encodings were configured; the choice stays.
    /// </summary>
    public void RefreshEncodings()
    {
        string? selected = FilesEncoding;
        Encodings = EncodingNames();
        FilesEncoding = selected;
    }

    /// <summary>
    ///  Writes the values edited at each level that changed; nothing before the values were read.
    /// </summary>
    public async Task SaveAsync()
    {
        if (!IsLoaded)
        {
            return;
        }

        KeepEdits();
        foreach ((ConfigScope scope, Dictionary<string, string> values) in _values)
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

    partial void OnMergeToolChanged(string? value) => ToolChosen(diff: false, value);

    partial void OnDiffToolChanged(string? value) => ToolChosen(diff: true, value);

    private static ConfigScope ScopeOf(SettingLevel level) => level switch
    {
        SettingLevel.Effective => ConfigScope.Effective,
        SettingLevel.Local => ConfigScope.Local,
        SettingLevel.SystemWide => ConfigScope.System,
        _ => ConfigScope.Global,
    };

    private static IReadOnlyList<string> EncodingNames()
        => [.. AppSettings.AvailableEncodings.Values.Select(encoding => encoding.EncodingName)];

    private static string? EncodingNameOf(string webName)
        => AppSettings.AvailableEncodings.TryGetValue(webName.ToLowerInvariant(), out Encoding? encoding)
            ? encoding.EncodingName
            : null;

    private async Task<bool> CheckGitCommandAsync()
    {
        int check = _gitCommandChecks;
        bool found = await _canFindGit(_gitCommand());
        if (check == _gitCommandChecks)
        {
            IsGitFound = found;
        }

        return found;
    }

    // Upstream's cboDiffTool_TextChanged and cboMergeTool_TextChanged: the rows follow the tool.
    private void ToolChosen(bool diff, string? value)
    {
        if (_showing || !IsLoaded)
        {
            return;
        }

        string tool = (value ?? "").Trim();
        _ = ShowToolRowAsync(diff, tool);
    }

    private async Task ShowToolRowAsync(bool diff, string tool)
    {
        try
        {
            ConfigScope scope = _scope;
            DiffMergeToolRow read =
                await DiffMergeToolSettings.ReadAsync(_gitConfig, scope, tool, diff, RepositoryOf(scope));

            // A slower read of a tool that is no longer chosen, or of another level, must not overwrite the rows.
            if (scope == _scope && ((diff ? DiffTool : MergeTool) ?? "").Trim() == tool)
            {
                SetToolRow(diff, read.Path, read.Command);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _reportError(ex.Message);
        }
    }

    private void SetToolRow(bool diff, string path, string command)
    {
        _showing = true;
        if (diff)
        {
            (DiffToolPath, DiffToolCommand) = (path, command);
        }
        else
        {
            (MergeToolPath, MergeToolCommand) = (path, command);
        }

        _showing = false;
    }

    private async Task ShowAsync()
    {
        ConfigScope scope = _scope;
        if (!_values.TryGetValue(scope, out Dictionary<string, string>? values))
        {
            values = [];
            foreach (string key in _textKeys.Append(AutoCrlfKey).Append(SettingKeyString.FilesEncoding))
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

            _values[scope] = values;
        }

        if (scope != _scope)
        {
            return;
        }

        _showing = true;
        UserName = values[UserNameKey];
        UserEmail = values[UserEmailKey];
        CredentialHelper = values[CredentialHelperKey];
        Editor = values[EditorKey];
        MergeTool = values[MergeToolKey];
        DiffTool = values[DiffToolKey];
        CommitTemplate = values[CommitTemplateKey];
        _showing = false;
        FilesEncoding = EncodingNameOf(values[SettingKeyString.FilesEncoding]);
        await ShowToolRowsAsync(scope, values, diff: false);
        await ShowToolRowsAsync(scope, values, diff: true);
        AutoCrlf = (AutoCrlfOptions.FirstOrDefault(option =>
            option.Value.Equals(values[AutoCrlfKey], StringComparison.OrdinalIgnoreCase)) ?? AutoCrlfOptions[^1]).Value;
        ShowsCredentialHelper = scope != ConfigScope.Effective;
        CanEditCredentialHelper = !_multipleCredentialHelpers.Contains(scope);
    }

    private async Task ShowToolRowsAsync(ConfigScope scope, Dictionary<string, string> values, bool diff)
    {
        string tool = values[diff ? DiffToolKey : MergeToolKey];
        if (!_toolRows.TryGetValue((scope, diff), out ToolRowEdit? edit))
        {
            DiffMergeToolRow read =
                await DiffMergeToolSettings.ReadAsync(_gitConfig, scope, tool, diff, RepositoryOf(scope));
            edit = new ToolRowEdit(tool, read.Path, read.Command);
            _toolRows[(scope, diff)] = edit;
        }

        if (scope == _scope)
        {
            SetToolRow(diff, edit.Path, edit.Command);
        }
    }

    // The values on the page, kept for the level they were edited at; the read-only levels keep nothing.
    private void KeepEdits()
    {
        if (_scope is ConfigScope.Effective or ConfigScope.System ||
            !_values.TryGetValue(_scope, out Dictionary<string, string>? values))
        {
            return;
        }

        values[UserNameKey] = Trimmed(UserName);
        values[UserEmailKey] = Trimmed(UserEmail);
        if (!_multipleCredentialHelpers.Contains(_scope))
        {
            values[CredentialHelperKey] = Trimmed(CredentialHelper);
        }

        values[EditorKey] = Trimmed(Editor);
        values[MergeToolKey] = Trimmed(MergeTool);
        values[DiffToolKey] = Trimmed(DiffTool);
        values[CommitTemplateKey] = Trimmed(CommitTemplate);
        values[AutoCrlfKey] = AutoCrlf;

        // A stored encoding that is not on the list shows nothing selected, and stays as it is.
        if (FilesEncoding is { } encodingName
            && AppSettings.AvailableEncodings.Values.FirstOrDefault(encoding => encoding.EncodingName == encodingName)
                is { } chosen)
        {
            values[SettingKeyString.FilesEncoding] = chosen.WebName;
        }

        KeepToolRow(diff: false, MergeTool, MergeToolPath, MergeToolCommand);
        KeepToolRow(diff: true, DiffTool, DiffToolPath, DiffToolCommand);
        return;

        static string Trimmed(string? text) => (text ?? "").Trim();
    }

    private void KeepToolRow(bool diff, string? tool, string? path, string? command)
    {
        if (_toolRows.TryGetValue((_scope, diff), out ToolRowEdit? edit))
        {
            edit.Tool = (tool ?? "").Trim();
            edit.Path = (path ?? "").Trim();
            edit.Command = (command ?? "").Trim();
        }
    }

    // Upstream writes both on every save, which would turn what it found on this computer into the user's setting. So a
    // tool that stayed writes only what was edited; a newly chosen tool writes both, as upstream, since git may not know it.
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
}

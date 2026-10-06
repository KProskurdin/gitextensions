using Avalonia.Controls;
using GitExtensions.Xplat.Core.Settings;
using GitExtUtils.GitUI.Theming;

namespace GitExtensions.Xplat.App;

/// <summary>
///  App preferences and git config: the new shell's version of the upstream <c>FormSettings</c> pages it needs. Closes with
///  true when the values were saved.
/// </summary>
public partial class SettingsWindow : Window
{
    private static readonly (string Key, Func<SettingsWindow, TextBox> Global, Func<SettingsWindow, TextBox> Local)[]
        _gitConfigFields =
        [
            ("user.name", window => window.GlobalUserNameBox, window => window.LocalUserNameBox),
            ("user.email", window => window.GlobalUserEmailBox, window => window.LocalUserEmailBox),
            ("merge.tool", window => window.GlobalMergeToolBox, window => window.LocalMergeToolBox),
            ("diff.tool", window => window.GlobalDiffToolBox, window => window.LocalDiffToolBox),
        ];

    private readonly IAppPreferences _preferences;
    private readonly IGitConfigService _gitConfig;
    private readonly string? _repositoryPath;
    private readonly Dictionary<(ConfigScope, string), string> _loaded = [];
    private readonly HotkeyEditor<BrowseCommand> _browseHotkeys = new(Hotkeys.Browse);
    private readonly HotkeyEditor<CommitCommand> _commitHotkeys = new(Hotkeys.Commit);
    private readonly HotkeyEditor<GridCommand> _gridHotkeys = new(Hotkeys.Grid);
    private readonly HotkeyEditor<DiffCommand> _diffHotkeys = new(Hotkeys.Diff);
    private readonly HotkeyEditor<LeftPanelCommand> _leftPanelHotkeys = new(Hotkeys.LeftPanel);
    private readonly HotkeyEditor<ConflictsCommand> _conflictsHotkeys = new(Hotkeys.Conflicts);

    public SettingsWindow(IAppPreferences preferences, string? repositoryPath)
    {
        _preferences = preferences;
        _gitConfig = AppServices.GitConfig;
        _repositoryPath = repositoryPath;
        InitializeComponent();

        ShowThemes(preferences.Theme);
        ColorblindCheck.IsChecked = preferences.ThemeVariations.Contains(ThemeVariations.Colorblind);
        CloseAfterCommitCheck.IsChecked = preferences.CloseCommitDialogAfterCommit;
        CloseProcessDialogCheck.IsChecked = preferences.CloseProcessDialog;
        RecentSizeBox.Value = preferences.RecentRepositoriesHistorySize;
        GitCommandBox.Text = preferences.GitCommand;
        LocalHeaderText.Text = repositoryPath is null ? "This repository (none open)" : "This repository";
        foreach ((_, _, Func<SettingsWindow, TextBox> local) in _gitConfigFields)
        {
            local(this).IsEnabled = repositoryPath is not null;
        }

        ShowHotkeyRows();
        MergeToolChoices.SelectionChanged += (_, _) => FillFromChoice(MergeToolChoices, GlobalMergeToolBox);
        DiffToolChoices.SelectionChanged += (_, _) => FillFromChoice(DiffToolChoices, GlobalDiffToolBox);
        CancelButton.Click += (_, _) => Close(false);
        SaveButton.Click += (_, _) => UiActions.Run(SaveAsync, ex => ErrorText.Text = ex.Message);
        Opened += (_, _) => UiActions.Run(LoadGitConfigAsync, ex => ErrorText.Text = ex.Message);
    }

    /// <summary>
    ///  True once the git config values are on show; saving before that would write blanks over them.
    /// </summary>
    public bool IsGitConfigLoaded { get; private set; }

    /// <summary>
    ///  The browse window's hotkey boxes by command, for tests and for the key capture.
    /// </summary>
    public IReadOnlyDictionary<BrowseCommand, TextBox> HotkeyBoxes => _browseHotkeys.Boxes;

    /// <summary>
    ///  The commit window's hotkey boxes by command.
    /// </summary>
    public IReadOnlyDictionary<CommitCommand, TextBox> CommitHotkeyBoxes => _commitHotkeys.Boxes;

    // The current theme is listed even when its file is gone, so opening and saving the page does not change it.
    private void ShowThemes(ThemeId current)
    {
        List<ThemeOption> options = [.. AppServices.Themes.GetThemeIds().Select(id => new ThemeOption(id))];
        if (!options.Any(option => option.Id == current))
        {
            options.Add(new ThemeOption(current));
        }

        ThemeBox.ItemsSource = options;
        ThemeBox.SelectedItem = options.First(option => option.Id == current);
    }

    private void ShowHotkeyRows()
    {
        _browseHotkeys.AddRows(HotkeysPanel, "Browse window");
        _commitHotkeys.AddRows(HotkeysPanel, "Commit window");
        _gridHotkeys.AddRows(HotkeysPanel, "Revision grid");
        _diffHotkeys.AddRows(HotkeysPanel, "Diff viewer");
        _leftPanelHotkeys.AddRows(HotkeysPanel, "Left panel");
        _conflictsHotkeys.AddRows(HotkeysPanel, "Resolve conflicts");
    }

    private async Task LoadGitConfigAsync()
    {
        SaveButton.IsEnabled = false;
        foreach ((string key, Func<SettingsWindow, TextBox> global, Func<SettingsWindow, TextBox> local) in
                 _gitConfigFields)
        {
            global(this).Text = await LoadAsync(ConfigScope.Global, key);
            if (_repositoryPath is not null)
            {
                local(this).Text = await LoadAsync(ConfigScope.Local, key);
            }
        }

        IsGitConfigLoaded = true;
        SaveButton.IsEnabled = true;

        // Read after the values, so a slow git does not hold back the rest of the page.
        MergeToolChoices.ItemsSource = await AppServices.DiffMergeTools.GetAvailableAsync(diff: false);
        DiffToolChoices.ItemsSource = await AppServices.DiffMergeTools.GetAvailableAsync(diff: true);
    }

    private static void FillFromChoice(ComboBox choices, TextBox target)
    {
        if (choices.SelectedItem is string tool)
        {
            target.Text = tool;
        }
    }

    private async Task<string> LoadAsync(ConfigScope scope, string key)
    {
        string value = await _gitConfig.GetAsync(scope, key, scope == ConfigScope.Local ? _repositoryPath : null);
        _loaded[(scope, key)] = value;
        return value;
    }

    private async Task SaveAsync()
    {
        ErrorText.Text = "";
        if (ThemeBox.SelectedItem is ThemeOption theme)
        {
            _preferences.Theme = theme.Id;
        }

        _preferences.ThemeVariations = ColorblindCheck.IsChecked == true ? [ThemeVariations.Colorblind] : [];
        _preferences.CloseCommitDialogAfterCommit = CloseAfterCommitCheck.IsChecked == true;
        _preferences.CloseProcessDialog = CloseProcessDialogCheck.IsChecked == true;
        _preferences.RecentRepositoriesHistorySize =
            (int)(RecentSizeBox.Value ?? _preferences.RecentRepositoriesHistorySize);
        _preferences.GitCommand = GitCommandBox.Text?.Trim() ?? "";
        string? hotkeys = _preferences.SerializedHotkeys;
        hotkeys = _browseHotkeys.Apply(hotkeys);
        hotkeys = _commitHotkeys.Apply(hotkeys);
        hotkeys = _gridHotkeys.Apply(hotkeys);
        hotkeys = _diffHotkeys.Apply(hotkeys);
        hotkeys = _leftPanelHotkeys.Apply(hotkeys);
        hotkeys = _conflictsHotkeys.Apply(hotkeys);
        if (hotkeys != _preferences.SerializedHotkeys)
        {
            _preferences.SerializedHotkeys = hotkeys;
            Hotkeys.Load(hotkeys);
        }

        _preferences.Save();

        // Only values the user changed are written, so a setting made elsewhere since the window opened is kept.
        foreach ((string key, Func<SettingsWindow, TextBox> global, Func<SettingsWindow, TextBox> local) in
                 _gitConfigFields)
        {
            await SaveIfChangedAsync(ConfigScope.Global, key, global(this).Text);
            if (_repositoryPath is not null)
            {
                await SaveIfChangedAsync(ConfigScope.Local, key, local(this).Text);
            }
        }

        Close(true);
    }

    private async Task SaveIfChangedAsync(ConfigScope scope, string key, string? text)
    {
        string value = text?.Trim() ?? "";
        if (_loaded.TryGetValue((scope, key), out string? loaded) && loaded == value)
        {
            return;
        }

        await _gitConfig.SetAsync(scope, key, value, scope == ConfigScope.Local ? _repositoryPath : null);
    }
}

/// <summary>
///  A theme in the list, named as upstream's appearance page names them.
/// </summary>
public sealed record ThemeOption(ThemeId Id)
{
    public override string ToString()
        => Id == ThemeId.WindowsAppColorModeId ? "Follow the operating system"
            : Id == ThemeId.DefaultLight ? "light (default)"
            : Id.IsBuiltin ? Id.Name
            : $"{Id.Name} (user)";
}

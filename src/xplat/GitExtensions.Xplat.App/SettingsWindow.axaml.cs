using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using GitExtensions.Extensibility.Plugins;
using GitExtensions.Extensibility.Settings;
using GitExtensions.Xplat.Core.Plugins;
using GitExtensions.Xplat.Core.Scripts;
using GitExtensions.Xplat.Core.Settings;
using GitExtUtils.GitUI.Theming;
using GitUI.ScriptsEngine;

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
    private readonly HotkeyEditor<int> _scriptHotkeys = new(Hotkeys.Scripts);
    private readonly IReadOnlyList<ScriptDefinition> _loadedScripts;
    private readonly ScriptListEditor _scripts;
    private HelpWindow? _scriptHelp;
    private readonly IReadOnlyList<IGitPlugin> _plugins;
    private readonly Dictionary<IGitPlugin, PluginSettingsEditor> _pluginEditors = [];
    private SettingsSource? _pluginSettings;
    private readonly Dictionary<Confirmation, CheckBox> _confirmationChecks = [];

    public SettingsWindow(IAppPreferences preferences, string? repositoryPath,
        IReadOnlyList<IGitPlugin>? plugins = null, IGitPlugin? selectedPlugin = null, bool pluginsPage = false)
    {
        _preferences = preferences;
        _gitConfig = AppServices.GitConfig;
        _repositoryPath = repositoryPath;
        _loadedScripts = AppServices.Scripts.Load();
        _scripts = new ScriptListEditor(_loadedScripts);
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
        ShowConfirmations();
        ShowScripts();
        _plugins = plugins ?? [];
        ShowPlugins(selectedPlugin);
        if (pluginsPage)
        {
            Tabs.SelectedItem = PluginsTab;
        }

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

    /// <summary>
    ///  The scripts' hotkey boxes by the scripts' hotkey identifiers.
    /// </summary>
    public IReadOnlyDictionary<int, TextBox> ScriptHotkeyBoxes => _scriptHotkeys.Boxes;

    /// <summary>
    ///  The script list being edited; stored on OK.
    /// </summary>
    public ScriptListEditor ScriptEditor => _scripts;

    /// <summary>
    ///  The Confirmations tab's check boxes; checked means the app asks first.
    /// </summary>
    public IReadOnlyDictionary<Confirmation, CheckBox> ConfirmationChecks => _confirmationChecks;

    // Upstream's ConfirmationsSettingsPage, with the questions whose action the new shell has, in upstream's groups.
    private void ShowConfirmations()
    {
        foreach (IGrouping<string, ConfirmationOption> group in Confirmations.Options.GroupBy(option => option.Group))
        {
            ConfirmationsPanel.Children.Add(new TextBlock
            {
                Text = group.Key + ":", Opacity = 0.7, Margin = new Avalonia.Thickness(0, 8, 0, 2),
            });
            foreach (ConfirmationOption option in group)
            {
                CheckBox check = new()
                {
                    Content = option.Label,
                    IsChecked = _preferences.Asks(option.Confirmation),
                    Margin = new Avalonia.Thickness(12, 0, 0, 0),
                };
                _confirmationChecks[option.Confirmation] = check;
                ConfirmationsPanel.Children.Add(check);
            }
        }
    }

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
        _scriptHotkeys.AddRows(HotkeysPanel, "Scripts");
    }

    private void ShowScripts()
    {
        ScriptsList.ItemsSource = _scripts.Items;
        ScriptEventBox.ItemsSource = Enum.GetValues<ScriptEvent>();
        ScriptsList.SelectionChanged += (_, _) => ShowSelectedScript();
        AddScriptButton.Click += (_, _) =>
        {
            ScriptsList.SelectedItem = _scripts.Add();
            ScriptNameBox.Focus();
            ScriptNameBox.SelectAll();
        };
        DeleteScriptButton.Click += (_, _) => ChangeSelectedScript(item =>
        {
            int index = _scripts.Items.IndexOf(item);
            _scripts.Remove(item);
            ScriptsList.SelectedIndex = Math.Min(index, _scripts.Items.Count - 1);
        });
        MoveScriptUpButton.Click += (_, _) => ChangeSelectedScript(item => Reselect(item, _scripts.MoveUp));
        MoveScriptDownButton.Click += (_, _) => ChangeSelectedScript(item => Reselect(item, _scripts.MoveDown));
        ScriptHelpButton.Click += (_, _) => ShowScriptHelp();
        Closed += (_, _) => _scriptHelp?.Close();
        ScriptsList.SelectedIndex = _scripts.Items.Count > 0 ? 0 : -1;
        ShowSelectedScript();
    }

    private void ShowSelectedScript()
    {
        ScriptListItem? item = ScriptsList.SelectedItem as ScriptListItem;
        ScriptDetails.DataContext = item;
        ScriptDetails.IsEnabled = item is not null;
        DeleteScriptButton.IsEnabled = item is not null;
        MoveScriptUpButton.IsEnabled = item is not null && _scripts.CanMoveUp(item);
        MoveScriptDownButton.IsEnabled = item is not null && _scripts.CanMoveDown(item);
    }

    private void ChangeSelectedScript(Action<ScriptListItem> change)
    {
        if (ScriptsList.SelectedItem is ScriptListItem item)
        {
            change(item);
        }
    }

    // Moving an item in the list can drop the list's selection.
    private void Reselect(ScriptListItem item, Action<ScriptListItem> move)
    {
        move(item);
        ScriptsList.SelectedItem = item;
        ShowSelectedScript();
    }

    // Not modal, as upstream: the help stays open beside the page while the arguments are typed.
    private void ShowScriptHelp()
    {
        if (_scriptHelp is { IsVisible: true })
        {
            _scriptHelp.Activate();
            return;
        }

        _scriptHelp = new HelpWindow(ScriptHelp.ArgumentsHelpTitle, ScriptHelp.ArgumentsHelp);
        _scriptHelp.Show(this);
    }

    /// <summary>
    ///  The settings of the plugin selected on the Plugins tab, or null.
    /// </summary>
    public PluginSettingsEditor? SelectedPluginEditor =>
        PluginsList.SelectedItem is PluginOption option ? _pluginEditors.GetValueOrDefault(option.Plugin) : null;

    // Upstream's settings tree lists every loaded plugin under Plugins; one with no settings says so.
    private void ShowPlugins(IGitPlugin? selected)
    {
        List<PluginOption> options = [.. _plugins.Select(plugin => new PluginOption(plugin))];
        PluginsList.ItemsSource = options;
        PluginsList.SelectionChanged += (_, _) => ShowSelectedPlugin();
        PluginLevelText.Text = _repositoryPath is null
            ? "Your user settings, shared with Git Extensions for Windows."
            : "The settings in effect for this repository. As in Git Extensions for Windows, a value the repository sets is " +
              "changed there; any other value is stored in your user settings.";
        PluginsList.SelectedItem =
            options.FirstOrDefault(option => option.Plugin == selected) ?? options.FirstOrDefault();
        ShowSelectedPlugin();
    }

    private void ShowSelectedPlugin()
    {
        PluginSettingsPanel.Children.Clear();
        if (PluginsList.SelectedItem is not PluginOption { Plugin: var plugin })
        {
            PluginTitleText.Text = _plugins.Count == 0 ? "No plugins are loaded." : "";
            return;
        }

        if (!_pluginEditors.TryGetValue(plugin, out PluginSettingsEditor? editor))
        {
            _pluginSettings ??= AppServices.PluginSettings.Open(_repositoryPath);
            editor = new PluginSettingsEditor(plugin, _pluginSettings);
            _pluginEditors[plugin] = editor;
        }

        PluginTitleText.Text = editor.Title;
        if (editor.Rows.Count == 0)
        {
            PluginSettingsPanel.Children.Add(
                new TextBlock { Text = "There are no settings available for this plugin." });
        }

        foreach (PluginSettingRow row in editor.Rows)
        {
            PluginSettingsPanel.Children.Add(CreateSettingControl(row));
        }
    }

    // One editor per upstream setting type, as upstream's SettingControlBindingsProvider chooses them.
    private static Control CreateSettingControl(PluginSettingRow row)
    {
        switch (row.Kind)
        {
            case PluginSettingKind.Bool:
                CheckBox check = new() { Content = row.Caption, IsThreeState = true, IsChecked = row.Checked };
                check.IsCheckedChanged += (_, _) => row.Checked = check.IsChecked;
                return check;
            case PluginSettingKind.Choice:
                ComboBox choice = new() { ItemsSource = row.Choices, SelectedItem = row.Text, MinWidth = 200 };
                choice.SelectionChanged += (_, _) => row.Text = choice.SelectedItem as string;
                return Labeled(row.Caption, choice);
            case PluginSettingKind.Text or PluginSettingKind.Password or PluginSettingKind.Number:
                TextBox text = new() { Text = row.Text, PlaceholderText = row.Placeholder };
                if (row.Kind == PluginSettingKind.Password)
                {
                    text.PasswordChar = '●';
                }

                // Not TextChanged: it is raised later, so OK pressed right after typing would save the old text.
                text.PropertyChanged += (_, e) =>
                {
                    if (e.Property != TextBox.TextProperty)
                    {
                        return;
                    }

                    row.Text = text.Text;

                    // Upstream colors a number box red while its text is not a number.
                    text.Foreground = row.IsValid ? null : Avalonia.Media.Brushes.Firebrick;
                };
                return Labeled(row.Caption, text);
            case PluginSettingKind.Note:
                return new TextBlock
                {
                    Text = row.Text, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Opacity = 0.8
                };
            default:
                return Labeled(row.Caption,
                    new TextBlock
                    {
                        Text = "This setting cannot be edited in this version yet.",
                        Opacity = 0.7,
                        VerticalAlignment = VerticalAlignment.Center,
                    });
        }
    }

    private static Control Labeled(string caption, Control editor)
    {
        Grid grid = new() { ColumnDefinitions = new ColumnDefinitions("220,*") };
        TextBlock label = new()
        {
            Text = caption,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Avalonia.Thickness(0, 0, 8, 0),
        };
        Grid.SetColumn(editor, 1);
        grid.Children.Add(label);
        grid.Children.Add(editor);
        return grid;
    }

    private void SavePluginSettings()
    {
        if (_pluginSettings is null)
        {
            return;
        }

        foreach (PluginSettingsEditor editor in _pluginEditors.Values)
        {
            editor.Save();
        }

        AppServices.PluginSettings.Save(_pluginSettings);
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
        foreach ((Confirmation confirmation, CheckBox check) in _confirmationChecks)
        {
            _preferences.SetAsks(confirmation, check.IsChecked == true);
        }

        string? hotkeys = _preferences.SerializedHotkeys;
        hotkeys = _browseHotkeys.Apply(hotkeys);
        hotkeys = _commitHotkeys.Apply(hotkeys);
        hotkeys = _gridHotkeys.Apply(hotkeys);
        hotkeys = _diffHotkeys.Apply(hotkeys);
        hotkeys = _leftPanelHotkeys.Apply(hotkeys);
        hotkeys = _conflictsHotkeys.Apply(hotkeys);
        hotkeys = _scriptHotkeys.Apply(hotkeys);
        _preferences.SerializedHotkeys = hotkeys;

        // Upstream stores the page's scripts on every save; this one only when they changed, to keep the file untouched.
        IReadOnlyList<ScriptDefinition> scripts = _scripts.Scripts;
        if (ScriptsXml.Write(scripts) != ScriptsXml.Write(_loadedScripts))
        {
            AppServices.Scripts.Save(scripts);
        }

        // After the scripts, so the scripts' hotkeys follow a renamed, added or deleted script.
        Hotkeys.Load(hotkeys);

        _preferences.Save();
        SavePluginSettings();

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
///  A plugin in the Plugins tab's list, by name.
/// </summary>
public sealed record PluginOption(IGitPlugin Plugin)
{
    public override string ToString() => Plugin.Name ?? "";
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

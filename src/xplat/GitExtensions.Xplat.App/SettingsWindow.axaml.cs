using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using GitCommands;
using GitExtensions.Extensibility.Plugins;
using GitExtensions.Extensibility.Settings;
using GitExtensions.Xplat.Core.Plugins;
using GitExtensions.Xplat.Core.Repository;
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
        StartWithRecentWorkingDirCheck.IsChecked = preferences.StartWithRecentWorkingDir;
        ShowCommitCountCheck.IsChecked = preferences.ShowGitStatusInBrowseToolbar;
        DefaultCloneDestinationBox.Text = preferences.DefaultCloneDestinationPath;
        RelativeDateCheck.IsChecked = preferences.RelativeDate;
        AlwaysShowCheckoutDlgCheck.IsChecked = preferences.AlwaysShowCheckoutBranchDlg;
        UseLocalChangesActionCheck.IsChecked = preferences.UseDefaultCheckoutBranchAction;
        AutoNormaliseCheck.IsChecked = preferences.AutoNormaliseBranchName;
        ShowNormaliseSymbols(preferences.AutoNormaliseSymbol);
        CommitAndPushForcedCheck.IsChecked = preferences.CommitAndPushForcedWhenAmend;
        SecondLineEmptyCheck.IsChecked = preferences.EnsureCommitMessageSecondLineEmpty;
        RememberAmendCheck.IsChecked = preferences.RememberAmendCommitState;
        ShowCommitAndPushCheck.IsChecked = preferences.ShowCommitAndPush;
        RememberIgnoreWhitespaceCheck.IsChecked = preferences.RememberIgnoreWhiteSpacePreference;
        RememberEntireFileCheck.IsChecked = preferences.RememberShowEntireFilePreference;
        RememberContextLinesCheck.IsChecked = preferences.RememberNumberOfContextLines;

        // As upstream, the button saves at once, not with OK.
        SaveDiffDefaultsButton.Click += (_, _) => preferences.SaveDiffOptionsAsDefault();
        RecentSizeBox.Value = preferences.RecentRepositoriesHistorySize;
        ShowGitPage();
        ShowSshPage();
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

    // Upstream's Git page. Its "Change HOME" choices (FormFixHome) are offered on Windows only, where upstream checks HOME:
    // elsewhere HOME is always set, and changing it in the app would also move the app's own settings folders. The Linux
    // tools folder is Git for Windows' sh; upstream clears it on other OSes.
    private void ShowGitPage()
    {
        bool windows = OperatingSystem.IsWindows();
        GitCommandBox.Text = _preferences.GitCommand;
        LinuxToolsPanel.IsVisible = windows;
        LinuxToolsDirBox.Text = _preferences.LinuxToolsDir;

        EnvironmentConfiguration.SetEnvironmentVariables();
        HomeText.Text = HomeSettings.Describe(
            EnvironmentConfiguration.GetEnvironmentVariable(HomeSettings.GitConfigGlobalVariable),
            EnvironmentConfiguration.GetHomeDir(), windows);
        HomeChoicePanel.IsVisible = windows;
        DefaultHomeRadio.Content = $"Use default for HOME ({EnvironmentConfiguration.GetDefaultHomeDir()})";
        UserProfileHomeRadio.Content =
            $"Set HOME to USERPROFILE ({Environment.GetEnvironmentVariable("USERPROFILE")})";
        HomeChoice home = HomeSettings.ChoiceOf(_preferences.CustomHomeDir, _preferences.UserProfileHomeDir);
        DefaultHomeRadio.IsChecked = home == HomeChoice.Default;
        UserProfileHomeRadio.IsChecked = home == HomeChoice.UserProfile;
        OtherHomeRadio.IsChecked = home == HomeChoice.Other;
        OtherHomeBox.Text = _preferences.CustomHomeDir;
        OtherHomeRadio.IsCheckedChanged += (_, _) => EnableHomeOptions();
        EnableHomeOptions();

        BrowseGitCommandButton.Click +=
            (_, _) => Browse(() => BrowseFileAsync(GitCommandBox, "Command used to run git"));
        BrowseLinuxToolsButton.Click +=
            (_, _) => Browse(() => BrowseFolderAsync(LinuxToolsDirBox, "Path to linux tools"));
        BrowseHomeButton.Click += (_, _) => Browse(() => BrowseFolderAsync(OtherHomeBox, "HOME"));
    }

    private void EnableHomeOptions()
    {
        OtherHomeBox.IsEnabled = OtherHomeRadio.IsChecked == true;
        BrowseHomeButton.IsEnabled = OtherHomeRadio.IsChecked == true;
    }

    /// <summary>
    ///  The HOME choice on the Git tab.
    /// </summary>
    public HomeChoice SelectedHomeChoice
        => OtherHomeRadio.IsChecked == true ? HomeChoice.Other
            : UserProfileHomeRadio.IsChecked == true ? HomeChoice.UserProfile
            : HomeChoice.Default;

    // Upstream's SSH page. PuTTY is offered on Windows, where upstream looks for it and recognises plink.exe; elsewhere
    // plink is an "other ssh client" (upstream's rule would read a stored "/usr/bin/plink" back as one anyway).
    private void ShowSshPage()
    {
        PlinkBox.Text = _preferences.Plink;
        PuttygenBox.Text = _preferences.Puttygen;
        PageantBox.Text = _preferences.Pageant;
        AutoStartPageantCheck.IsChecked = _preferences.AutoStartPageant;
        SshClientKind kind = SshClients.KindOf(_preferences.SshPath);
        PuttyRadio.IsVisible = OperatingSystem.IsWindows() || kind == SshClientKind.Putty;
        OpenSshRadio.IsChecked = kind == SshClientKind.OpenSsh;
        PuttyRadio.IsChecked = kind == SshClientKind.Putty;
        OtherSshRadio.IsChecked = kind == SshClientKind.Other;
        if (kind == SshClientKind.Other)
        {
            OtherSshBox.Text = _preferences.SshPath;
        }

        PuttyRadio.IsCheckedChanged += (_, _) =>
        {
            if (PuttyRadio.IsChecked == true)
            {
                FindPutty();
            }

            EnableSshOptions();
        };
        OtherSshRadio.IsCheckedChanged += (_, _) => EnableSshOptions();
        EnableSshOptions();

        BrowseOtherSshButton.Click += (_, _) => Browse(() => BrowseFileAsync(OtherSshBox, "Other ssh client"));
        BrowsePlinkButton.Click += (_, _) => Browse(() => BrowseFileAsync(PlinkBox, "Path to plink"));
        BrowsePuttygenButton.Click += (_, _) => Browse(() => BrowseFileAsync(PuttygenBox, "Path to puttygen"));
        BrowsePageantButton.Click += (_, _) => Browse(() => BrowseFileAsync(PageantBox, "Path to pageant"));
    }

    private void EnableSshOptions()
    {
        bool other = OtherSshRadio.IsChecked == true;
        OtherSshBox.IsEnabled = other;
        BrowseOtherSshButton.IsEnabled = other;
        PuttyPanel.IsVisible = PuttyRadio.IsChecked == true;
    }

    private void FindPutty()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        PuttyPaths found = SshClients.FindPutty(
            new PuttyPaths(PlinkBox.Text ?? "", PuttygenBox.Text ?? "", PageantBox.Text ?? ""),
            SshClients.PuttyLocations(Environment.GetEnvironmentVariable, Environment.Is64BitProcess), File.Exists);
        PlinkBox.Text = found.Plink;
        PuttygenBox.Text = found.Puttygen;
        PageantBox.Text = found.Pageant;
    }

    /// <summary>
    ///  The ssh client choice on the SSH tab.
    /// </summary>
    public SshClientKind SelectedSshClient
        => PuttyRadio.IsChecked == true ? SshClientKind.Putty
            : OtherSshRadio.IsChecked == true ? SshClientKind.Other
            : SshClientKind.OpenSsh;

    private void Browse(Func<Task> browse) => UiActions.Run(browse, ex => ErrorText.Text = ex.Message);

    private async Task BrowseFileAsync(TextBox target, string title)
    {
        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions { Title = title, AllowMultiple = false });
        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path)
        {
            target.Text = path;
        }
    }

    private async Task BrowseFolderAsync(TextBox target, string title)
    {
        IReadOnlyList<IStorageFolder> folders = await StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions { Title = title, AllowMultiple = false });
        if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } path)
        {
            target.Text = path;
        }
    }

    // Upstream's FormFixHome checks before it closes: another folder must be typed, and HOME must then exist.
    private string? ValidateHome()
    {
        if (!HomeChoicePanel.IsVisible)
        {
            return null;
        }

        string other = OtherHomeBox.Text?.Trim() ?? "";
        HomeChoice choice = SelectedHomeChoice;
        return HomeSettings.Validate(choice, other,
            HomeSettings.HomeFor(choice, other, Environment.GetEnvironmentVariable),
            Directory.Exists);
    }

    private void SaveGitAndSshPages()
    {
        _preferences.GitCommand = GitCommandBox.Text?.Trim() ?? "";
        if (LinuxToolsPanel.IsVisible)
        {
            _preferences.LinuxToolsDir = LinuxToolsDirBox.Text?.Trim() ?? "";
        }

        if (HomeChoicePanel.IsVisible)
        {
            HomeChoice choice = SelectedHomeChoice;
            _preferences.CustomHomeDir = choice == HomeChoice.Other ? OtherHomeBox.Text?.Trim() ?? "" : "";
            _preferences.UserProfileHomeDir = choice == HomeChoice.UserProfile;
        }

        _preferences.Plink = PlinkBox.Text?.Trim() ?? "";
        _preferences.Puttygen = PuttygenBox.Text?.Trim() ?? "";
        _preferences.Pageant = PageantBox.Text?.Trim() ?? "";
        _preferences.AutoStartPageant = AutoStartPageantCheck.IsChecked == true;

        // As upstream's page: the setting and the variable git reads, so the next git started uses the client.
        string sshPath = SshClients.PathFor(SelectedSshClient, _preferences.Plink, OtherSshBox.Text?.Trim() ?? "");
        _preferences.SshPath = sshPath;
        SshClients.Apply(sshPath);
    }

    // Upstream's Advanced page lists "_", "-" and "(none)"; a symbol stored otherwise is shown too, so saving keeps it.
    private void ShowNormaliseSymbols(string current)
    {
        List<NormaliseSymbolOption> options =
            [.. BranchNames.Symbols.Select(symbol => new NormaliseSymbolOption(symbol.Label, symbol.Symbol))];
        if (!options.Any(option => option.Symbol == current))
        {
            options.Add(new NormaliseSymbolOption(current, current));
        }

        NormaliseSymbolBox.ItemsSource = options;
        NormaliseSymbolBox.SelectedItem = options.First(option => option.Symbol == current);
    }

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
        if (ValidateHome() is { } homeError)
        {
            Tabs.SelectedItem = GitTab;
            ErrorText.Text = homeError;
            return;
        }

        if (ThemeBox.SelectedItem is ThemeOption theme)
        {
            _preferences.Theme = theme.Id;
        }

        _preferences.ThemeVariations = ColorblindCheck.IsChecked == true ? [ThemeVariations.Colorblind] : [];
        _preferences.CloseCommitDialogAfterCommit = CloseAfterCommitCheck.IsChecked == true;
        _preferences.CloseProcessDialog = CloseProcessDialogCheck.IsChecked == true;
        _preferences.StartWithRecentWorkingDir = StartWithRecentWorkingDirCheck.IsChecked == true;
        _preferences.ShowGitStatusInBrowseToolbar = ShowCommitCountCheck.IsChecked == true;
        _preferences.DefaultCloneDestinationPath = DefaultCloneDestinationBox.Text?.Trim() ?? "";
        _preferences.RelativeDate = RelativeDateCheck.IsChecked == true;
        _preferences.AlwaysShowCheckoutBranchDlg = AlwaysShowCheckoutDlgCheck.IsChecked == true;
        _preferences.UseDefaultCheckoutBranchAction = UseLocalChangesActionCheck.IsChecked == true;
        _preferences.AutoNormaliseBranchName = AutoNormaliseCheck.IsChecked == true;
        if (NormaliseSymbolBox.SelectedItem is NormaliseSymbolOption symbol)
        {
            _preferences.AutoNormaliseSymbol = symbol.Symbol;
        }

        _preferences.CommitAndPushForcedWhenAmend = CommitAndPushForcedCheck.IsChecked == true;
        _preferences.EnsureCommitMessageSecondLineEmpty = SecondLineEmptyCheck.IsChecked == true;
        _preferences.RememberAmendCommitState = RememberAmendCheck.IsChecked == true;
        _preferences.ShowCommitAndPush = ShowCommitAndPushCheck.IsChecked == true;
        _preferences.RememberIgnoreWhiteSpacePreference = RememberIgnoreWhitespaceCheck.IsChecked == true;
        _preferences.RememberShowEntireFilePreference = RememberEntireFileCheck.IsChecked == true;
        _preferences.RememberNumberOfContextLines = RememberContextLinesCheck.IsChecked == true;
        _preferences.RecentRepositoriesHistorySize =
            (int)(RecentSizeBox.Value ?? _preferences.RecentRepositoriesHistorySize);
        SaveGitAndSshPages();
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

        // HOME and the Linux tools folder as the next git gets them; upstream sets them before every git start too.
        EnvironmentConfiguration.SetEnvironmentVariables();
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
///  A branch name normaliser symbol, named as upstream's Advanced page names it.
/// </summary>
public sealed record NormaliseSymbolOption(string Label, string Symbol)
{
    public override string ToString() => Label;
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

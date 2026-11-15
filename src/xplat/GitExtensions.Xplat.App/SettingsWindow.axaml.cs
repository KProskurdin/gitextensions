using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using GitCommands;
using GitCommands.Settings;
using GitCommands.Utils;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Plugins;
using GitExtensions.Extensibility.Settings;
using GitExtensions.Extensibility.Translations;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Plugins;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Scripts;
using GitExtensions.Xplat.Core.Settings;
using GitExtensions.Xplat.Core.Spelling;
using GitExtensions.Xplat.Ui;
using GitExtUtils.GitUI.Theming;
using GitUI.CommandsDialogs.SettingsDialog.RevisionLinks;
using GitUI.ScriptsEngine;

namespace GitExtensions.Xplat.App;

/// <summary>
///  App preferences and git config: the new shell's version of the upstream <c>FormSettings</c> pages it needs. Closes with
///  true when the values were saved.
/// </summary>
public partial class SettingsWindow : Window
{
    // Upstream's _outputHistoryTooltip.
    private const string OutputHistoryTooltip = """
        The output displayed in the process dialog and some trace output is retained and shown in the output history.

        - With this set, the output history is displayed in a tab in the lower pane of the Browse Repository window.
        - With this unset, the output history is displayed in a panel docked to the lower left corner of the Browse Repository window.

        Focus the output history and (when displayed as panel) toggle its visibility using the hotkey {0}.
        """;

    private readonly IAppPreferences _preferences;
    private readonly string? _repositoryPath;
    private readonly HotkeyEditor<BrowseCommand> _browseHotkeys = new(Hotkeys.Browse);
    private readonly HotkeyEditor<CommitCommand> _commitHotkeys = new(Hotkeys.Commit);
    private readonly HotkeyEditor<GridCommand> _gridHotkeys = new(Hotkeys.Grid);
    private readonly HotkeyEditor<DiffCommand> _diffHotkeys = new(Hotkeys.Diff);
    private readonly HotkeyEditor<LeftPanelCommand> _leftPanelHotkeys = new(Hotkeys.LeftPanel);
    private readonly HotkeyEditor<ConflictsCommand> _conflictsHotkeys = new(Hotkeys.Conflicts);
    private readonly HotkeyEditor<BrowseDiffCommand> _browseDiffHotkeys = new(Hotkeys.BrowseDiff);
    private readonly HotkeyEditor<StashCommand> _stashHotkeys = new(Hotkeys.Stash);
    private readonly HotkeyEditor<int> _scriptHotkeys = new(Hotkeys.Scripts);
    private bool _hotkeysReset;
    private HelpWindow? _scriptHelp;

    private readonly DistributedSettings _revisionLinkSettings;

    // The definitions edited at each settings level so far, all stored on OK.
    private readonly Dictionary<SettingLevel, (DistributedSettings Settings, RevisionLinkEditor Editor)>
        _revisionLinkLevels =
            [];

    /// <summary>
    ///  The design-time constructor (<see cref="DesignTime"/>): the window's XAML only.
    /// </summary>
    [Obsolete(DesignTime.ConstructorOnly)]
#pragma warning disable CS8618 // The fields the window's data fills stay unset: no code of the window runs.
    public SettingsWindow() => InitializeComponent();
#pragma warning restore CS8618

    public SettingsWindow(IAppPreferences preferences, string? repositoryPath,
        IReadOnlyList<IGitPlugin>? plugins = null, IGitPlugin? selectedPlugin = null, bool pluginsPage = false)
    {
        _preferences = preferences;
        _repositoryPath = repositoryPath;
        _revisionLinkSettings = AppServices.RevisionLinks.Open(repositoryPath);
        _revisionLinkLevels[SettingLevel.Effective] = (_revisionLinkSettings, new RevisionLinkEditor(_revisionLinkSettings));
        ViewModel = new SettingsViewModel(new WindowDialogHost(this), preferences, new SettingsViewModelServices
        {
            Themes = AppServices.Themes.GetThemeIds(),
            Dictionaries = CommitSpellChecker.Dictionaries(AppServices.DictionaryFolder),
            FontFamilies =
            [
                .. FontManager.Current.SystemFonts.Select(family => family.Name).Distinct()
                    .Order(StringComparer.CurrentCultureIgnoreCase),
            ],
            Scripts = AppServices.Scripts.Load(),
            RevisionLinkEditorAt = level => DistributedSettingsAt(level).Editor,
            LoadRemotes = () => RevisionLinkTemplates.LoadRemotesAsync(repositoryPath),
            SettingsAt = level => DistributedSettingsAt(level).Settings,
            Plugins = plugins ?? [],
            SkippedPlugins = AppServices.Plugins.Skipped,
            PluginSettingsAt = level => AppServices.PluginSettings.Open(repositoryPath, level),
            BuildServerTypes = () => Task.Run(() => AppServices.BuildServers.Types),
            HasRepository = repositoryPath is not null,
            RepositoryPath = repositoryPath,
            GitConfig = AppServices.GitConfig,
            CanFindGit = AppServices.CanFindGit,
            DiffMergeTools = AppServices.DiffMergeTools,
            Translations = () => Translator.GetAllTranslations(),
            SavePagesBeforeStore = SavePagesBeforeStore,
            SavePagesAfterStore = SavePagesAfterStoreAsync,
        }, OperatingSystem.IsWindows());
        ViewModel.GitPageRequested += (_, _) => Tabs.SelectedItem = GitTab;
        ViewModel.PluginsPage.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PluginsPageViewModel.IsSkipped) && _headers.ContainsKey(PluginsTab))
            {
                SourceHeaderOf(PluginsTab).IsVisible = !ViewModel.PluginsPage.IsSkipped;
            }
        };
        InitializeComponent();
        DataContext = ViewModel;
        AddPageHeaders();

        // Upstream's SortingSettingsPage help pictures.
        RevisionSortOrderHelp.Click += (_, _) => DialogHelp.Open(DialogHelp.Manual("settings", "sorting-sort-author-date")());
        PrioBranchNamesHelp.Click += (_, _) => DialogHelp.Open(DialogHelp.Manual("settings", "sorting-sort-prioritized-branches")());
        PrioRemoteNamesHelp.Click += (_, _) => DialogHelp.Open(DialogHelp.Manual("settings", "sorting-sort-prioritized-remotes")());

        // Upstream's ShowAppThemesDirectory and ShowUserThemesDirectory; the user folder is made first, as upstream makes it.
        // A portable app has no user folder, where upstream's item does nothing; here it is off.
        tsmiApplicationFolder.Click += (_, _) => AppServices.FileManager.OpenFolder(AppServices.ThemePaths.AppThemesDirectory);
        tsmiUserFolder.IsEnabled = AppServices.ThemePaths.UserThemesDirectory is not null;
        tsmiUserFolder.Click += (_, _) =>
        {
            if (AppServices.ThemePaths.UserThemesDirectory is { } folder)
            {
                Directory.CreateDirectory(folder);
                AppServices.FileManager.OpenFolder(folder);
            }
        };

        WireAppearancePage();

        ToolTip.SetTip(chkShowOutputHistoryAsTab, string.Format(CultureInfo.CurrentCulture, OutputHistoryTooltip,
            Hotkeys.GestureFor(BrowseCommand.FocusOutputHistoryAndToggleIfPanel)?.ToString() ?? ""));

        // Upstream's DiffViewerSettingsPage sets its runtime tooltip.
        ToolTip.SetTip(ShowDiffForAllParentsCheck, UpstreamTranslation.ByText(ShowDiffForAllParentsTooltip));

        // As upstream, the button saves at once, not with OK.
        SaveDiffDefaultsButton.Click += (_, _) => preferences.SaveDiffOptionsAsDefault();
        ShowBlameTooltips();
        ShowSortingPage();
        ShowGitPage();
        ShowSshPage();
        ShowGitConfigPage();
        ShowHotkeyRows();
        ShowScripts();
        ShowRevisionLinks();
        ShowBuildServerPage();
        ShowChecklistPage();
        ShowPageTree(pluginsPage ? selectedPlugin ?? (object)PluginsTab : null);
    }

    public SettingsViewModel ViewModel { get; }

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
    public ScriptListEditor ScriptEditor => ViewModel.ScriptsPage.Editor;

    /// <summary>
    ///  The Confirmations tab's check boxes; checked means the app asks first.
    /// </summary>
    public IReadOnlyDictionary<Confirmation, ConfirmationChoice> ConfirmationChecks
        => ViewModel.ConfirmationChoices.ToDictionary(choice => choice.Confirmation);

    /// <summary>
    ///  The text of a font list's first choice: no font stored, so the app's own applies.
    /// </summary>
    public const string DefaultFontChoice = FontChoice.DefaultChoice;

    // Upstream's TranslatedStrings.ShowDiffForAllParentsTooltip.
    internal const string ShowDiffForAllParentsTooltip =
        "Show all differences between the selected commits, not limiting to only one difference.\n\n"
        + "- For a single selected commit, show the difference with its parent commit.\n"
        + "- For a single selected merge commit, show the difference with all parents.\n"
        + "- For two selected commits with a common ancestor (BASE), show the difference\n"
        + "between the commits as well as the difference from BASE to the selected commits.\n"
        + "See documentation for more details about icons and range diffs.\n"
        + "- For multiple selected commits (up to four), show the difference for\n"
        + "all the first selected with the last selected commit.\n"
        + "- For more than four selected commits, show the difference from the first to\n"
        + "the last selected commit.";

    /// <summary>
    ///  The Revision links tab's definitions; stored on OK.
    /// </summary>
    public RevisionLinkEditor RevisionLinkEditor => ViewModel.RevisionLinksPage.Editor;

    // Upstream's Revision links page: the categories on the left, the selected one's fields on the right. Upstream's "Add"
    // button drops down the templates; here they are buttons of their own.
    private void ShowRevisionLinks()
    {
        foreach (ICloudProviderExternalLinkDefinitionExtractor template in RevisionLinkTemplates.All)
        {
            Button button = new()
            {
                Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 6,
                    Children =
                    {
                        new Image
                        {
                            Source = ScriptIcons.Named(TemplateIconName(template)),
                            Width = 16,
                            Height = 16
                        },
                        new TextBlock { Text = RevisionLinkTemplates.MenuText(template) },
                    },
                },
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            button.Click += (_, _) => UiActions.Run(
                () => ViewModel.RevisionLinksPage.AddTemplateCommand.ExecuteAsync(template),
                ex => ViewModel.ErrorText = ex.Message);
            RevisionLinkTemplateButtons.Children.Add(button);
        }

        AddRevisionLinkFormatButton.Command = ViewModel.RevisionLinksPage.AddFormatCommand;
        RevisionLinkFormatsList.AddHandler(Button.ClickEvent, (_, e) =>
        {
            if (e.Source is Button { Name: "RemoveRevisionLinkFormatButton", DataContext: RevisionLinkFormatItem format })
            {
                ViewModel.RevisionLinksPage.RemoveFormat(format);
            }
        });
        ViewModel.RevisionLinksPage.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(RevisionLinksPageViewModel.Selected))
            {
                ShowSelectedRevisionLink();
            }
        };
        ShowSelectedRevisionLink();
    }

    // The repository's settings at a level, shared by the pages that store there (upstream's DistributedSettingsSet), all
    // stored on OK. Without a repository the settings in effect are the user's, the one global level.
    private (DistributedSettings Settings, RevisionLinkEditor Editor) DistributedSettingsAt(SettingLevel level)
    {
        if (_repositoryPath is null)
        {
            level = SettingLevel.Effective;
        }

        if (!_revisionLinkLevels.TryGetValue(level,
                out (DistributedSettings Settings, RevisionLinkEditor Editor) source))
        {
            DistributedSettings settings = AppServices.RevisionLinks.Open(_repositoryPath, level);
            source = (settings, new RevisionLinkEditor(settings));
            _revisionLinkLevels[level] = source;
        }

        return source;
    }

    // Upstream's template icons are WinForms images of its Images resources; the app has the same pictures by name.
    private static string? TemplateIconName(ICloudProviderExternalLinkDefinitionExtractor template) => template switch
    {
        GitHubExternalLinkDefinitionExtractor => "GitHub",
        AzureDevopsExternalLinkDefinitionExtractor => "VisualStudioTeamServices",
        _ => null,
    };

    // The selected definition's fields; the details declare their own data type, so their data context is set here.
    private void ShowSelectedRevisionLink()
    {
        RevisionLinkDetails.DataContext = ViewModel.RevisionLinksPage.Selected;
        RevisionLinkDetails.IsEnabled = ViewModel.RevisionLinksPage.HasSelection;
    }

    // Upstream's Blame viewer page; as upstream, the moved-lines options carry a warning.
    private void ShowBlameTooltips()
    {
        ToolTip.SetTip(BlameDetectMoveInFileCheck, BlameMoveWarning);
        ToolTip.SetTip(BlameDetectMoveInAllFilesCheck, BlameMoveWarning);
    }

    /// <summary>
    ///  Upstream's tooltip of the moved-lines options.
    /// </summary>
    public const string BlameMoveWarning =
        "Could prevent blame to calculate the accurate line number when blaming previous revisions.";

    // Upstream's Sorting page: each list shows its choices by their description, as upstream fills them, and the help
    // upstream shows beside them is the tooltip.
    private void ShowSortingPage()
    {
        ToolTip.SetTip(RevisionSortBox, SortingTexts.RevisionSortWarning);
        ToolTip.SetTip(PrioritizedBranchesBox, SortingTexts.PrioritizedBranchNames);
        ToolTip.SetTip(PrioritizedRemotesBox, SortingTexts.PrioritizedRemoteNames);
    }

    // Upstream's Git page. Its "Change HOME" choices (FormFixHome) are offered on Windows only, where upstream checks HOME:
    // elsewhere HOME is always set, and changing it in the app would also move the app's own settings folders. The Linux
    // tools folder is Git for Windows' sh; upstream clears it on other OSes.
    private void ShowGitPage()
    {
        BrowseGitCommandButton.Click +=
            (_, _) => Browse(() => BrowseFileAsync(GitCommandBox, "Command used to run git"));
        BrowseLinuxToolsButton.Click +=
            (_, _) => Browse(() => BrowseFolderAsync(LinuxToolsDirBox, "Path to linux tools"));
        BrowseHomeButton.Click += (_, _) => Browse(() => BrowseFolderAsync(OtherHomeBox, "HOME"));
    }

    /// <summary>
    ///  The HOME choice on the Git tab.
    /// </summary>
    public HomeChoice SelectedHomeChoice => ViewModel.SelectedHomeChoice;

    // Upstream's SSH page. PuTTY is offered on Windows, where upstream looks for it and recognises plink.exe; elsewhere
    // plink is an "other ssh client" (upstream's rule would read a stored "/usr/bin/plink" back as one anyway).
    private void ShowSshPage()
    {
        BrowseOtherSshButton.Click += (_, _) => Browse(() => BrowseFileAsync(OtherSshBox, "Other ssh client"));
        BrowsePlinkButton.Click += (_, _) => Browse(() => BrowseFileAsync(PlinkBox, "Path to plink"));
        BrowsePuttygenButton.Click += (_, _) => Browse(() => BrowseFileAsync(PuttygenBox, "Path to puttygen"));
        BrowsePageantButton.Click += (_, _) => Browse(() => BrowseFileAsync(PageantBox, "Path to pageant"));
    }

    /// <summary>
    ///  The ssh client choice on the SSH tab.
    /// </summary>
    public SshClientKind SelectedSshClient => ViewModel.SelectedSshClient;

    private void Browse(Func<Task> browse) => UiActions.Run(browse, ex => ViewModel.ErrorText = ex.Message);

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

    private void ShowHotkeyRows()
    {
        _browseHotkeys.AddRows(HotkeysPanel, "Browse window");
        _commitHotkeys.AddRows(HotkeysPanel, "Commit window");
        _gridHotkeys.AddRows(HotkeysPanel, "Revision grid");
        _diffHotkeys.AddRows(HotkeysPanel, "Diff viewer");
        _leftPanelHotkeys.AddRows(HotkeysPanel, "Left panel");
        _conflictsHotkeys.AddRows(HotkeysPanel, "Resolve conflicts");
        _browseDiffHotkeys.AddRows(HotkeysPanel, "Diff tab files");
        _stashHotkeys.AddRows(HotkeysPanel, "Stashes");
        _scriptHotkeys.AddRows(HotkeysPanel, "Scripts");
        bResetToDefaults.Click += (_, _) => ResetHotkeysToDefaults();
    }

    // Upstream's bResetToDefaults_Click: nothing stored is kept, not even the hotkeys of what the shell does not have; only
    // Save stores it, as upstream's page.
    private void ResetHotkeysToDefaults()
    {
        _hotkeysReset = true;
        _browseHotkeys.ShowDefaults();
        _commitHotkeys.ShowDefaults();
        _gridHotkeys.ShowDefaults();
        _diffHotkeys.ShowDefaults();
        _leftPanelHotkeys.ShowDefaults();
        _conflictsHotkeys.ShowDefaults();
        _browseDiffHotkeys.ShowDefaults();
        _stashHotkeys.ShowDefaults();
        _scriptHotkeys.ShowDefaults();
    }

    private void ShowScripts()
    {
        ScriptEventBox.ItemsSource = Enum.GetValues<ScriptEvent>();

        // Upstream's icon list: no icon, then its images by name, each shown with its picture.
        IEnumerable<ScriptIconChoice> icons =
        [
            new ScriptIconChoice("", null),
            .. ScriptIcons.Names.Select(name => new ScriptIconChoice(name, ScriptIcons.Named(name)))
        ];
        ScriptIconBox.ItemsSource = icons;
        ScriptIconBox.SelectedValueBinding = CompiledBinding.Create<ScriptIconChoice, string>(choice => choice.Name);
        ScriptIconBox.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<ScriptIconChoice>((choice, _) =>
            new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                Children =
                {
                    new Image { Source = choice?.Picture, Width = 16, Height = 16 },
                    new TextBlock { Text = choice is { Name.Length: > 0 } ? choice.Name : "(none)" },
                },
            });
        ScriptIconFileButton.Click +=
            (_, _) => UiActions.Run(PickScriptIconFileAsync, ex => ViewModel.ErrorText = ex.Message);
        ScriptHelpButton.Click += (_, _) => ShowScriptHelp();
        Closed += (_, _) => _scriptHelp?.Close();
        ViewModel.ScriptsPage.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ScriptsPageViewModel.Selected))
            {
                ShowSelectedScript();
            }
        };
        ViewModel.ScriptsPage.ScriptAdded += (_, _) =>
        {
            ScriptNameBox.Focus();
            ScriptNameBox.SelectAll();
        };
        ShowSelectedScript();
    }

    private async Task PickScriptIconFileAsync()
    {
        if (ViewModel.ScriptsPage.Selected is not { } item)
        {
            return;
        }

        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Icon or associated file path",
            FileTypeFilter = [FilePickerFileTypes.ImageAll, FilePickerFileTypes.All],
        });
        if (files is [var file, ..] && file.TryGetLocalPath() is { } path)
        {
            item.IconFilePath = path;
        }
    }

    // The selected script's values; the details declare their own data type, so their data context is set here.
    private void ShowSelectedScript()
    {
        ScriptDetails.DataContext = ViewModel.ScriptsPage.Selected;
        ScriptDetails.IsEnabled = ViewModel.ScriptsPage.HasSelection;
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
    ///  The settings of the plugin whose page is shown, or null.
    /// </summary>
    public PluginSettingsEditor? SelectedPluginEditor => ViewModel.PluginsPage.Editor;

    private void SavePluginSettings() => ViewModel.PluginsPage.Save(AppServices.PluginSettings.Save);

    // OK and Apply, as the checklist's "Save and rescan" runs them.
    private Task SaveAsync(bool close) => ViewModel.SaveAsync(close);

    // The pages whose editors the window drives, stored before the preferences are written.
    private void SavePagesBeforeStore()
    {
        string? hotkeys = _hotkeysReset ? null : _preferences.SerializedHotkeys;
        hotkeys = _browseHotkeys.Apply(hotkeys);
        hotkeys = _commitHotkeys.Apply(hotkeys);
        hotkeys = _gridHotkeys.Apply(hotkeys);
        hotkeys = _diffHotkeys.Apply(hotkeys);
        hotkeys = _leftPanelHotkeys.Apply(hotkeys);
        hotkeys = _conflictsHotkeys.Apply(hotkeys);
        hotkeys = _browseDiffHotkeys.Apply(hotkeys);
        hotkeys = _stashHotkeys.Apply(hotkeys);
        hotkeys = _scriptHotkeys.Apply(hotkeys);
        _preferences.SerializedHotkeys = hotkeys;

        ViewModel.ScriptsPage.StoreIfChanged(AppServices.Scripts.Save);

        // After the scripts, so the scripts' hotkeys follow a renamed, added or deleted script.
        Hotkeys.Load(hotkeys);
    }

    // The pages with their own stores, after the preferences are written; Apply reads git config again.
    private async Task SavePagesAfterStoreAsync(bool close)
    {
        SavePluginSettings();
        foreach ((DistributedSettings settings, RevisionLinkEditor editor) in _revisionLinkLevels.Values)
        {
            editor.Save();
        }

        SaveBuildServerPage();
        ViewModel.DetailedPage.Save();
        foreach ((DistributedSettings settings, RevisionLinkEditor _) in _revisionLinkLevels.Values)
        {
            AppServices.RevisionLinks.Save(settings);
        }

        await SaveGitConfigAsync();
        if (!close && !IsGitConfigLoaded)
        {
            // Apply may have just stored a git command that works.
            await LoadGitConfigAsync();
        }
    }
}

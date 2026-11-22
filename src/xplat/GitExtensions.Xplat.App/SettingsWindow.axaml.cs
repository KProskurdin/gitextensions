using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.LogicalTree;
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
            SupportsUpdateRefs = GitCommands.Git.GitVersion.Current.SupportUpdateRefs,
            DiffMergeTools = AppServices.DiffMergeTools,
            WindowsInstallation = AppServices.WindowsInstallation,
            Translations = () => Translator.GetAllTranslations(),
            SavePagesBeforeStore = SavePagesBeforeStore,
            SavePagesAfterStore = SavePagesAfterStoreAsync,
            PageTree = PageTreeServices(PageTexts),
            Hotkeys =
            [
                HotkeyEditor.Group(Hotkeys.Browse, "Browse window"),
                HotkeyEditor.Group(Hotkeys.Commit, "Commit window"),
                HotkeyEditor.Group(Hotkeys.Grid, "Revision grid"),
                HotkeyEditor.Group(Hotkeys.Diff, "Diff viewer"),
                HotkeyEditor.Group(Hotkeys.LeftPanel, "Left panel"),
                HotkeyEditor.Group(Hotkeys.Conflicts, "Resolve conflicts"),
                HotkeyEditor.Group(Hotkeys.BrowseDiff, "Diff tab files"),
                HotkeyEditor.Group(Hotkeys.Stash, "Stashes"),
                HotkeyEditor.Group(Hotkeys.Scripts, "Scripts"),
            ],
            ChooseLanguage = ChooseLanguageAsync,
            OpenUri = uri => Launcher.LaunchUriAsync(uri),
            DarkBackground = DarkEditorBackground,
            PickFile = PickFileAsync,
            PickFolder = PickFolderAsync,
            OpenUrl = url => DialogHelp.Open(url),
            OpenFolder = folder => AppServices.FileManager.OpenFolder(folder),
            AppThemesFolder = AppServices.ThemePaths.AppThemesDirectory,
            UserThemesFolder = AppServices.ThemePaths.UserThemesDirectory,
            ClearAvatarCache = () => AppServices.Avatars.ClearCacheAsync(),
            ConfigureEncodings = () => new AvailableEncodingsWindow().ShowDialog<bool>(this),
            ShowScriptHelp = ShowScriptHelp,
        }, OperatingSystem.IsWindows());
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

        // The theme folders' menu is a flyout, which gives its items a data context only when it opens (MVVM.md, Flyouts).
        foreach (Control item in ((MenuFlyout)sbOpenThemeFolder.Flyout!).Items.OfType<Control>())
        {
            item.DataContext = ViewModel;
        }

        ToolTip.SetTip(chkShowOutputHistoryAsTab, string.Format(CultureInfo.CurrentCulture, OutputHistoryTooltip,
            Hotkeys.GestureFor(BrowseCommand.FocusOutputHistoryAndToggleIfPanel)?.ToString() ?? ""));

        // Upstream's DiffViewerSettingsPage sets its runtime tooltip.
        ToolTip.SetTip(ShowDiffForAllParentsCheck, UpstreamTranslation.ByText(ShowDiffForAllParentsTooltip));

        ShowBlameTooltips();
        ShowSortingPage();
        ShowGitConfigPage();

        ShowScripts();
        ShowRevisionLinks();
        ShowBuildServerPage();
        ShowPageTree(pluginsPage, selectedPlugin);
    }

    public SettingsViewModel ViewModel { get; }

    /// <summary>
    ///  The browse window's hotkey boxes by command, for tests; the Hotkeys page must be shown.
    /// </summary>
    public IReadOnlyDictionary<BrowseCommand, TextBox> HotkeyBoxes => HotkeyBoxesOf<BrowseCommand>();

    /// <summary>
    ///  The commit window's hotkey boxes by command; the Hotkeys page must be shown.
    /// </summary>
    public IReadOnlyDictionary<CommitCommand, TextBox> CommitHotkeyBoxes => HotkeyBoxesOf<CommitCommand>();

    /// <summary>
    ///  The scripts' hotkey boxes by the scripts' hotkey identifiers; the Hotkeys page must be shown.
    /// </summary>
    public IReadOnlyDictionary<int, TextBox> ScriptHotkeyBoxes => HotkeyBoxesOf<int>();

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
            button.Command = ViewModel.AddRevisionLinkTemplateCommand;
            button.CommandParameter = template;
            RevisionLinkTemplateButtons.Children.Add(button);
        }

        AddRevisionLinkFormatButton.Command = ViewModel.RevisionLinksPage.AddFormatCommand;
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

    /// <summary>
    ///  The HOME choice on the Git tab.
    /// </summary>
    public HomeChoice SelectedHomeChoice => ViewModel.SelectedHomeChoice;

    // Upstream's SSH page. PuTTY is offered on Windows, where upstream looks for it and recognises plink.exe; elsewhere
    // plink is an "other ssh client" (upstream's rule would read a stored "/usr/bin/plink" back as one anyway).

    /// <summary>
    ///  The ssh client choice on the SSH tab.
    /// </summary>
    public SshClientKind SelectedSshClient => ViewModel.SelectedSshClient;

    // The pickers the view model's Browse buttons ask for (SettingsViewModelServices).
    private async Task<string?> PickFileAsync(string title, bool images)
    {
        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = images ? [FilePickerFileTypes.ImageAll, FilePickerFileTypes.All] : null,
        });
        return files is [var file, ..] ? file.TryGetLocalPath() : null;
    }

    private async Task<string?> PickFolderAsync(string title)
    {
        IReadOnlyList<IStorageFolder> folders = await StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions { Title = title, AllowMultiple = false });
        return folders is [var folder, ..] ? folder.TryGetLocalPath() : null;
    }

    // The keys pressed in a hotkey row's box.
    private void HotkeyBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is TextBox { DataContext: HotkeyRow row })
        {
            HotkeyEditor.Capture(row, e);
        }
    }

    private Dictionary<TCommand, TextBox> HotkeyBoxesOf<TCommand>()
        where TCommand : struct
        => HotkeysPanel.GetLogicalDescendants().OfType<TextBox>()
            .Where(box => box.DataContext is HotkeyRow { Command: TCommand })
            .ToDictionary(box => (TCommand)((HotkeyRow)box.DataContext!).Command);

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
        string? hotkeys = ViewModel.Hotkeys.Apply(_preferences.SerializedHotkeys);
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

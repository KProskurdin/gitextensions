using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitCommands;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Settings;
using GitExtensions.Xplat.Core.Avatars;
using GitExtensions.Xplat.Core.BuildServer;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Plugins;
using GitExtensions.Xplat.Core.Presentation;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Scripts;
using GitExtUtils.GitUI.Theming;
using GitUIPluginInterfaces;

namespace GitExtensions.Xplat.Core.Settings;

/// <summary>
///  What <see cref="SettingsViewModel"/> asks of its window besides <see cref="IDialogHost"/>: the lists the app reads,
///  the stores of the pages that have their own, and the parts of the pages only the view can give (pictures, controls'
///  texts, key gestures, other windows).
/// </summary>
public sealed record SettingsViewModelServices
{
    /// <summary>
    ///  The themes the app has.
    /// </summary>
    public required IReadOnlyList<ThemeId> Themes { get; init; }

    /// <summary>
    ///  The spelling dictionaries next to the app, without "None".
    /// </summary>
    public required IReadOnlyList<string> Dictionaries { get; init; }

    /// <summary>
    ///  The installed font families, in the order the Fonts page lists them.
    /// </summary>
    public required IReadOnlyList<string> FontFamilies { get; init; }

    /// <summary>
    ///  The stored scripts, which the Scripts page edits.
    /// </summary>
    public required IReadOnlyList<ScriptDefinition> Scripts { get; init; }

    /// <summary>
    ///  The revision link definitions stored at a settings level, which the window keeps for OK.
    /// </summary>
    public required Func<SettingLevel, RevisionLinkEditor> RevisionLinkEditorAt { get; init; }

    /// <summary>
    ///  The repository's remotes, which the revision link templates read.
    /// </summary>
    public required Func<Task<IReadOnlyList<Remote>>> LoadRemotes { get; init; }

    /// <summary>
    ///  The repository's settings at a level (upstream's DistributedSettingsSet), which the window keeps for OK.
    /// </summary>
    public required Func<SettingLevel, SettingsSource> SettingsAt { get; init; }

    /// <summary>
    ///  The loaded plugins, the plugin files passed over, and their settings at a source.
    /// </summary>
    public IReadOnlyList<GitExtensions.Extensibility.Plugins.IGitPlugin> Plugins { get; init; } = [];

    public IReadOnlyList<SkippedPlugin> SkippedPlugins { get; init; } = [];

    public Func<SettingLevel, SettingsSource> PluginSettingsAt { get; init; } = level => new InMemorySettingsSource(level);

    /// <summary>
    ///  The plugins' build server types, read off the UI thread.
    /// </summary>
    public Func<Task<IReadOnlyList<BuildServerType>>> BuildServerTypes { get; init; } =
        () => Task.FromResult<IReadOnlyList<BuildServerType>>([]);

    /// <summary>
    ///  A repository is open (the build server page has nothing to set up without one).
    /// </summary>
    public bool HasRepository { get; init; }

    /// <summary>
    ///  The repository's path, or null; the git config page reads its local values there.
    /// </summary>
    public string? RepositoryPath { get; init; }

    /// <summary>
    ///  Git's settings, which the Config page edits.
    /// </summary>
    public IGitConfigService GitConfig { get; init; } = new GitConfigService();

    /// <summary>
    ///  Whether a git command can be run.
    /// </summary>
    public Func<string?, Task<bool>> CanFindGit { get; init; } = GitConfigChoices.CanFindGitAsync;

    /// <summary>
    ///  Whether git has <c>rebase.updateRefs</c> (2.38), which the Git > Advanced page shows only then, as upstream.
    /// </summary>
    public bool SupportsUpdateRefs { get; init; } = true;

    /// <summary>
    ///  Git's diff and merge tools, for the Config page's lists.
    /// </summary>
    public IDiffMergeToolCatalog? DiffMergeTools { get; init; }

    /// <summary>
    ///  The translations in the Translation folder, without English.
    /// </summary>
    public required Func<IReadOnlyList<string>> Translations { get; init; }

    /// <summary>
    ///  Stores the window's own pages before the preferences are written (hotkeys, scripts).
    /// </summary>
    public required Action SavePagesBeforeStore { get; init; }

    /// <summary>
    ///  Stores the window's pages that have their own stores (plugins, revision links, build server, detailed settings,
    ///  git config); the flag says whether the window closes after.
    /// </summary>
    public required Func<bool, Task> SavePagesAfterStore { get; init; }

    /// <summary>
    ///  Puts the next git's environment as the settings say (HOME, the Linux tools folder).
    /// </summary>
    public Action SetEnvironment { get; init; } = EnvironmentConfiguration.SetEnvironmentVariables;

    /// <summary>
    ///  Puts the ssh client in the variable git reads (upstream's <c>GIT_SSH</c>).
    /// </summary>
    public Action<string> ApplySshPath { get; init; } = SshClients.Apply;

    /// <summary>
    ///  What the HOME check after a save reads (upstream's <c>FormFixHome.CheckHomePath</c>, Windows only).
    /// </summary>
    public HomeLookup HomeLookup { get; init; } = HomeLookup.System;

    /// <summary>
    ///  The pages the window has and how the tree shows them.
    /// </summary>
    public SettingsPageTreeServices PageTree { get; init; } =
        new() { Pages = SettingsPageTree.Entries.Select(entry => entry.Page).ToHashSet() };

    /// <summary>
    ///  The windows' hotkeys the Hotkeys page changes.
    /// </summary>
    public IReadOnlyList<HotkeyGroup> Hotkeys { get; init; } = [];

    /// <summary>
    ///  What the Checklist page checks; by default read by <see cref="ChecklistFactsReader"/>.
    /// </summary>
    public Func<Task<ChecklistFacts>>? ReadChecklistFacts { get; init; }

    /// <summary>
    ///  What upstream's Windows installer registered (the install folder and the Explorer shell extension), for the Shell
    ///  extension page and the Checklist on Windows; nothing is installed by default.
    /// </summary>
    public Platform.IWindowsInstallation WindowsInstallation { get; init; } = new Platform.InMemoryWindowsInstallation();

    /// <summary>
    ///  Where the Checklist's repairs look for git, the linux tools and PuTTY on Windows (upstream's <c>SolveGitCommand</c>,
    ///  <c>SolveLinuxToolsDir</c> and <c>AutoFindPuttyPaths</c>); by default this Windows, and nothing elsewhere.
    /// </summary>
    public Platform.WindowsGitLookup? GitLookup { get; init; } =
        OperatingSystem.IsWindows() ? Platform.WindowsGitLookup.System : null;

    /// <summary>
    ///  Upstream's FormChooseTranslation, which the Checklist's language row opens.
    /// </summary>
    public Func<Task> ChooseLanguage { get; init; } = () => Task.CompletedTask;

    public Func<Uri, Task> OpenUri { get; init; } = _ => Task.CompletedTask;

    /// <summary>
    ///  The file picker: its title, and true to offer images first; the path chosen, or null.
    /// </summary>
    public Func<string, bool, Task<string?>> PickFile { get; init; } = (_, _) => Task.FromResult<string?>(null);

    /// <summary>
    ///  The folder picker with its title; the path chosen, or null.
    /// </summary>
    public Func<string, Task<string?>> PickFolder { get; init; } = _ => Task.FromResult<string?>(null);

    /// <summary>
    ///  Opens a web page (upstream's user manual) in the browser.
    /// </summary>
    public Action<string> OpenUrl { get; init; } = _ => { };

    /// <summary>
    ///  Upstream's <c>UserManual.UrlFor</c>: a section's address (subfolder, anchor), from the version's documentation
    ///  address the app sets at start-up.
    /// </summary>
    public Func<string, string, string> ManualUrl { get; init; } = GitUI.UserManual.UserManual.UrlFor;

    /// <summary>
    ///  Shows a folder in the system's file manager.
    /// </summary>
    public Action<string> OpenFolder { get; init; } = _ => { };

    /// <summary>
    ///  The application's themes folder, and the user's (null for a portable app, which has none).
    /// </summary>
    public string? AppThemesFolder { get; init; }

    public string? UserThemesFolder { get; init; }

    /// <summary>
    ///  Empties the author images' cache.
    /// </summary>
    public Func<Task> ClearAvatarCache { get; init; } = () => Task.CompletedTask;

    /// <summary>
    ///  Upstream's FormAvailableEncodings; true when the encodings were changed.
    /// </summary>
    public Func<Task<bool>> ConfigureEncodings { get; init; } = () => Task.FromResult(false);

    /// <summary>
    ///  The scripts' arguments help window.
    /// </summary>
    public Action ShowScriptHelp { get; init; } = () => { };

    /// <summary>
    ///  The editor background of a dark theme, toward which the Checklist dims its rows; null in a light theme.
    /// </summary>
    public Func<System.Drawing.Color?> DarkBackground { get; init; } = () => null;
}

/// <summary>
///  The view model of upstream's <c>FormSettings</c> for the pages that are one value per preference: the appearance, the
///  general, browse, commit, diff, blame, sorting and advanced pages, and the Git and SSH pages with upstream's HOME and ssh
///  client rules; and the OK, Apply and Cancel buttons. The window's editors of the other pages are saved through
///  <see cref="SettingsViewModelServices"/>. Closes with true when the values were saved.
/// </summary>
public sealed partial class SettingsViewModel : DialogViewModel
{
    public const string English = "English";

    // Upstream stores "none" for the dictionary list's first item.
    public const string NoDictionaryStored = "none";

    private const int DefaultQuickSearchTimeout = 4000;

    // Upstream's font labels, in upstream's order.
    private static readonly (AppFont Font, string Label)[] _fontLabels =
    [
        (AppFont.Application, "Application font"), (AppFont.Commit, "Commit font"), (AppFont.Code, "Code font"),
        (AppFont.Monospace, "Monospace font"),
    ];
    private const int MaxOutputHistoryDepth = 1000;

    /// <summary>
    ///  Upstream's type of the Git page (Paths), shown when OK finds an error on it.
    /// </summary>
    public const string GitPage = "GitSettingsPage";

    /// <summary>
    ///  Upstream's "Download Git" link on the Git page (<c>downloadGitForWindows</c>).
    /// </summary>
    public const string DownloadGitUrl = "https://github.com/gitextensions/gitextensions/wiki/Application-Dependencies#git";

    // Upstream's form whose texts the HOME check uses.
    private const string FixHomeCategory = "FormFixHome";

    private const string HotkeysPage = "HotkeysSettingsPage";

    private readonly IAppPreferences _preferences;
    private readonly SettingsViewModelServices _services;

    // True while the stored values are shown, so a stored PuTTY choice does not look for PuTTY again.
    private bool _loading = true;

    public SettingsViewModel(IDialogHost host, IAppPreferences preferences, SettingsViewModelServices services,
        bool isWindows)
        : base(host)
    {
        _preferences = preferences;
        _services = services;
        IsWindows = isWindows;

        List<ThemeOption> themes = [.. services.Themes.Select(id => new ThemeOption(id))];

        // The current theme is listed even when its file is gone, so opening and saving the page does not change it.
        if (!themes.Any(option => option.Id == preferences.Theme))
        {
            themes.Add(new ThemeOption(preferences.Theme));
        }

        Themes = themes;
        SelectedTheme = themes.First(option => option.Id == preferences.Theme);
        Colorblind = preferences.ThemeVariations.Contains(ThemeVariations.Colorblind);

        // Upstream's appearance page: "None", then the dictionaries next to the app; a dictionary whose file is missing
        // shows as "None", as upstream's.
        Dictionaries = [CommitSpellingNone, .. services.Dictionaries];
        SelectedDictionary = services.Dictionaries.FirstOrDefault(name =>
            string.Equals(name, preferences.Dictionary, StringComparison.OrdinalIgnoreCase)) ?? CommitSpellingNone;
        ShowLanguages(preferences.Translation);

        // Upstream shows a stored "Default" as "Open pull dialog".
        SelectedPullAction = PullActionOption.All.FirstOrDefault(option => option.Action == preferences.DefaultPullAction)
                             ?? PullActionOption.All[0];

        // Upstream's Advanced page lists "_", "-" and "(none)"; a symbol stored otherwise is shown too, so saving keeps it.
        List<NormaliseSymbolOption> symbols =
            [.. BranchNames.Symbols.Select(symbol => new NormaliseSymbolOption(symbol.Label, symbol.Symbol))];
        if (!symbols.Any(option => option.Symbol == preferences.AutoNormaliseSymbol))
        {
            symbols.Add(new NormaliseSymbolOption(preferences.AutoNormaliseSymbol, preferences.AutoNormaliseSymbol));
        }

        NormaliseSymbols = symbols;
        SelectedNormaliseSymbol = symbols.First(option => option.Symbol == preferences.AutoNormaliseSymbol);
        SelectedTruncatePath = TruncatePathOption.All.FirstOrDefault(option =>
            option.Method == preferences.TruncatePathMethod) ?? TruncatePathOption.All[0];
        AvatarOptions avatars = preferences.Avatars;
        SelectedAvatarProvider = AvatarProviderOption.All.First(option => option.Provider == avatars.Provider);
        SelectedAvatarFallback = AvatarFallbackOption.All.First(option => option.Fallback == avatars.Fallback);
        CustomAvatarTemplate = avatars.CustomTemplate;
        AvatarCacheDays = avatars.CacheDays;
        SelectedRevisionSort = EnumOption<RevisionSortOrder>.All.First(option => option.Value == preferences.RevisionSortOrder);
        SelectedRefsSortBy = EnumOption<GitRefsSortBy>.All.First(option => option.Value == preferences.RefsSortBy);
        SelectedRefsSortOrder = EnumOption<GitRefsSortOrder>.All.First(option => option.Value == preferences.RefsSortOrder);

        ScriptsPage = new ScriptsPageViewModel(services.Scripts);
        RevisionLinksPage = new RevisionLinksPageViewModel(services.RevisionLinkEditorAt, services.LoadRemotes);
        DetailedPage = new DetailedPageViewModel(services.SettingsAt);
        PluginsPage = new PluginsPageViewModel(services.Plugins, services.SkippedPlugins, services.PluginSettingsAt);
        BuildServerPage = new BuildServerPageViewModel(services.SettingsAt, services.BuildServerTypes, services.HasRepository);
        GitConfigPage = new GitConfigPageViewModel(services.GitConfig, services.RepositoryPath, () => GitCommand,
            services.CanFindGit, services.DiffMergeTools ?? new GitDiffMergeToolCatalog(), message => ErrorText = message);
        GitConfigAdvancedPage = new GitConfigAdvancedPageViewModel(services.GitConfig, services.RepositoryPath,
            services.SupportsUpdateRefs, host.Translate);
        ShellExtensionPage = new ShellExtensionPageViewModel(services.WindowsInstallation, ex => ErrorText = ex.Message,
            () => services.OpenUrl(services.ManualUrl(ShellExtensionPageViewModel.HelpSubfolder,
                ShellExtensionPageViewModel.HelpAnchor)), host.Translate);
        Fonts = [.. _fontLabels.Select(font =>
            new FontChoice(font.Font, host.Translate(font.Label), services.FontFamilies, preferences.GetFont(font.Font)))];

        // Upstream's ConfirmationsSettingsPage, with the questions whose action the new shell has, in upstream's groups.
        ConfirmationGroups =
        [
            .. Confirmations.Options.GroupBy(option => option.Group).Select(group => new ConfirmationGroup(group.Key + ":",
                [.. group.Select(option => new ConfirmationChoice(option, Confirmations.CheckState(preferences, option.Confirmation)))]))
        ];
        Hotkeys = new HotkeysPageViewModel(services.Hotkeys);
        Checklist = new ChecklistPageViewModel(new ChecklistPageServices
        {
            ReadFacts = services.ReadChecklistFacts ?? new ChecklistFactsReader(preferences, services.GitConfig,
                services.DiffMergeTools ?? new GitDiffMergeToolCatalog(), services.RepositoryPath, isWindows)
            {
                Installation = services.WindowsInstallation,
            }.ReadAsync,
            Installation = services.WindowsInstallation,
            Save = () => SaveAsync(close: false),
            GoToPage = GoToPage,
            ChooseLanguage = async () =>
            {
                await services.ChooseLanguage();
                ShowLanguages(preferences.Translation);
            },
            OpenUri = services.OpenUri,
            DarkBackground = services.DarkBackground,
            ShowError = ex => ErrorText = ex.Message,
            ReadCheckAtStartup = () => preferences.CheckSettings,
            WriteCheckAtStartup = value => preferences.CheckSettings = value,
            Locate = isWindows && services.GitLookup is not null ? LocateAsync : null,
            ShowMessage = (text, caption) => host.ShowMessageAsync(text, caption),
            Translate = host.Translate,
        });
        PageTree = new SettingsPageTreeViewModel(services.PageTree with { PageTexts = PageTexts }, PluginsPage.Pages);
        PageTree.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SettingsPageTreeViewModel.ShownNode) && PageTree.ShownNode is { } node)
            {
                OnPageShown(node);
            }
        };
        LoadValues();
        LoadGitAndSshPages();
        _loading = false;
        return;

        // Upstream's GetSearchKeywords: the texts of the page's visible controls; a plugin's page, its settings' captions.
        IEnumerable<string> PageTexts(SettingsPageNode node)
            => node.Plugin switch
            {
                PluginOption { Plugin: var plugin } => PluginsPage.EditorOf(plugin).Rows
                    .SelectMany(row => (string?[])[row.Caption, row.Text]).OfType<string>(),
                SkippedPluginOption { File: var file } => [file.FileName, file.Reason],

                // The rows are shown by templates, which the view makes only for the page shown.
                _ when node.Page == HotkeysPage =>
                [
                    .. services.PageTree.PageTexts(node),
                    .. Hotkeys.Groups.SelectMany(group =>
                        group.Rows.SelectMany(row => (string[])[row.Description, row.Text]).Prepend(group.Title)),
                ],
                _ when node.Page == SettingsPageTreeViewModel.ChecklistPage =>
                    [.. services.PageTree.PageTexts(node), .. Checklist.Rows.Select(row => row.Text)],
                _ => services.PageTree.PageTexts(node),
            };
    }

    /// <summary>
    ///  The Git page's HOME choices and the Linux tools folder are Windows' (upstream checks HOME there only).
    /// </summary>
    public bool IsWindows { get; }

    public IReadOnlyList<ThemeOption> Themes { get; }

    /// <summary>
    ///  The Scripts page; its scripts are stored by the window, with the hotkeys that follow them.
    /// </summary>
    public ScriptsPageViewModel ScriptsPage { get; }

    /// <summary>
    ///  The Revision links page.
    /// </summary>
    public RevisionLinksPageViewModel RevisionLinksPage { get; }

    /// <summary>
    ///  The Detailed page's push and merge window settings.
    /// </summary>
    public DetailedPageViewModel DetailedPage { get; }

    /// <summary>
    ///  The plugins' settings pages.
    /// </summary>
    public PluginsPageViewModel PluginsPage { get; }

    /// <summary>
    ///  The build server integration page.
    /// </summary>
    public BuildServerPageViewModel BuildServerPage { get; }

    /// <summary>
    ///  The Config page: git's own settings.
    /// </summary>
    public GitConfigPageViewModel GitConfigPage { get; }

    /// <summary>
    ///  Upstream's Git > Advanced page.
    /// </summary>
    public GitConfigAdvancedPageViewModel GitConfigAdvancedPage { get; }

    /// <summary>
    ///  Upstream's Shell extension page, which the tree has on Windows only.
    /// </summary>
    public ShellExtensionPageViewModel ShellExtensionPage { get; }

    /// <summary>
    ///  The page tree, the page shown, the search box and the window title.
    /// </summary>
    public SettingsPageTreeViewModel PageTree { get; }

    /// <summary>
    ///  Upstream's Checklist page.
    /// </summary>
    public ChecklistPageViewModel Checklist { get; }

    /// <summary>
    ///  Upstream's Hotkeys page.
    /// </summary>
    public HotkeysPageViewModel Hotkeys { get; }

    /// <summary>
    ///  The Fonts page's fonts.
    /// </summary>
    public IReadOnlyList<FontChoice> Fonts { get; }

    /// <summary>
    ///  The Confirmations page's questions, in upstream's groups.
    /// </summary>
    public IReadOnlyList<ConfirmationGroup> ConfirmationGroups { get; }

    /// <summary>
    ///  The Confirmations page's questions.
    /// </summary>
    public IEnumerable<ConfirmationChoice> ConfirmationChoices => ConfirmationGroups.SelectMany(group => group.Choices);

    public IReadOnlyList<string> Dictionaries { get; }

    public IReadOnlyList<PullActionOption> PullActions => PullActionOption.All;

    public IReadOnlyList<NormaliseSymbolOption> NormaliseSymbols { get; }

    public IReadOnlyList<TruncatePathOption> TruncatePaths => TruncatePathOption.All;

    public IReadOnlyList<AvatarProviderOption> AvatarProviders => AvatarProviderOption.All;

    public IReadOnlyList<AvatarFallbackOption> AvatarFallbacks => AvatarFallbackOption.All;

    public IReadOnlyList<EnumOption<RevisionSortOrder>> RevisionSorts => EnumOption<RevisionSortOrder>.All;

    public IReadOnlyList<EnumOption<GitRefsSortBy>> RefsSortBys => EnumOption<GitRefsSortBy>.All;

    public IReadOnlyList<EnumOption<GitRefsSortOrder>> RefsSortOrders => EnumOption<GitRefsSortOrder>.All;

    /// <summary>
    ///  Whether OK or Apply stored the values; Cancel closes with it, as upstream reports a save made with Apply.
    /// </summary>
    public bool Saved { get; private set; }

    [ObservableProperty]
    public partial string ErrorText { get; set; } = "";

    [ObservableProperty]
    public partial IReadOnlyList<string> Languages { get; private set; } = [];

    [ObservableProperty]
    public partial string? SelectedLanguage { get; set; }

    [ObservableProperty]
    public partial ThemeOption? SelectedTheme { get; set; }

    [ObservableProperty]
    public partial string? SelectedDictionary { get; set; }

    [ObservableProperty]
    public partial PullActionOption? SelectedPullAction { get; set; }

    [ObservableProperty]
    public partial NormaliseSymbolOption? SelectedNormaliseSymbol { get; set; }

    [ObservableProperty]
    public partial TruncatePathOption? SelectedTruncatePath { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCustomAvatar))]
    public partial AvatarProviderOption? SelectedAvatarProvider { get; set; }

    /// <summary>
    ///  As upstream: the template is used by the custom provider only.
    /// </summary>
    public bool IsCustomAvatar => SelectedAvatarProvider?.Provider == AvatarProvider.Custom;

    [ObservableProperty]
    public partial AvatarFallbackOption? SelectedAvatarFallback { get; set; }

    [ObservableProperty]
    public partial string? CustomAvatarTemplate { get; set; }

    [ObservableProperty]
    public partial decimal? AvatarCacheDays { get; set; }

    [ObservableProperty]
    public partial EnumOption<RevisionSortOrder>? SelectedRevisionSort { get; set; }

    [ObservableProperty]
    public partial EnumOption<GitRefsSortBy>? SelectedRefsSortBy { get; set; }

    [ObservableProperty]
    public partial EnumOption<GitRefsSortOrder>? SelectedRefsSortOrder { get; set; }

    [ObservableProperty]
    public partial string? PrioritizedBranchNames { get; set; }

    [ObservableProperty]
    public partial string? PrioritizedRemoteNames { get; set; }

    [ObservableProperty]
    public partial bool Colorblind { get; set; }

    [ObservableProperty]
    public partial bool ShowEolMarkerAsGlyph { get; set; }

    [ObservableProperty]
    public partial bool MulticolorBranches { get; set; }

    [ObservableProperty]
    public partial bool RevisionGraphDrawAlternateBackColor { get; set; }

    [ObservableProperty]
    public partial bool RevisionGraphDrawNonRelativesGray { get; set; }

    [ObservableProperty]
    public partial bool RevisionGraphDrawNonRelativesTextGray { get; set; }

    [ObservableProperty]
    public partial bool HighlightAuthoredRevisions { get; set; }

    [ObservableProperty]
    public partial bool FillRefLabels { get; set; }

    [ObservableProperty]
    public partial bool CloseCommitDialogAfterCommit { get; set; }

    [ObservableProperty]
    public partial bool CloseProcessDialog { get; set; }

    [ObservableProperty]
    public partial bool ShowGitCommandLine { get; set; }

    [ObservableProperty]
    public partial bool StartWithRecentWorkingDir { get; set; }

    [ObservableProperty]
    public partial bool FollowRenames { get; set; }

    [ObservableProperty]
    public partial bool ExactRenamesOnly { get; set; }

    [ObservableProperty]
    public partial bool UseHistogramDiffAlgorithm { get; set; }

    [ObservableProperty]
    public partial bool IncludeUntrackedFilesInAutoStash { get; set; }

    /// <summary>
    ///  Upstream's three-state "Update submodules on checkout": null asks.
    /// </summary>
    [ObservableProperty]
    public partial bool? UpdateSubmodulesOnCheckout { get; set; }

    [ObservableProperty]
    public partial bool CheckForUncommittedChangesInCheckoutBranch { get; set; }

    [ObservableProperty]
    public partial decimal? RevisionGridQuickSearchTimeout { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanShowSubmoduleStatus))]
    public partial bool ShowGitStatusForArtificialCommits { get; set; }

    [ObservableProperty]
    public partial bool ShowAheadBehindData { get; set; }

    [ObservableProperty]
    public partial bool ShowStashCount { get; set; }

    /// <summary>
    ///  Upstream's "Limit number of commits": unchecked is stored as 0, no limit.
    /// </summary>
    [ObservableProperty]
    public partial bool LimitCommits { get; set; }

    [ObservableProperty]
    public partial decimal? MaxRevisionGraphCommits { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanShowSubmoduleStatus))]
    public partial bool ShowGitStatusInBrowseToolbar { get; set; }

    [ObservableProperty]
    public partial bool ShowSubmoduleStatus { get; set; }

    /// <summary>
    ///  Upstream's SetSubmoduleStatus: the submodule status is read with the git status, so it is offered only while the
    ///  git status is shown on the commit button or the artificial commits.
    /// </summary>
    public bool CanShowSubmoduleStatus => ShowGitStatusInBrowseToolbar || ShowGitStatusForArtificialCommits;

    [ObservableProperty]
    public partial bool ShowRevisionGridTooltips { get; set; }

    [ObservableProperty]
    public partial bool ShowFindInCommitFilesGitGrep { get; set; }

    [ObservableProperty]
    public partial bool ShowGpgInformation { get; set; }

    [ObservableProperty]
    public partial bool ShowOutputHistoryAsTab { get; set; }

    [ObservableProperty]
    public partial decimal? OutputHistoryDepth { get; set; }

    [ObservableProperty]
    public partial bool MergeGraphLanesHavingCommonParent { get; set; }

    [ObservableProperty]
    public partial bool RenderGraphWithDiagonals { get; set; }

    [ObservableProperty]
    public partial bool StraightenGraphDiagonals { get; set; }

    [ObservableProperty]
    public partial string? DefaultCloneDestinationPath { get; set; }

    [ObservableProperty]
    public partial bool RelativeDate { get; set; }

    [ObservableProperty]
    public partial bool AlwaysShowCheckoutBranchDlg { get; set; }

    [ObservableProperty]
    public partial bool UseDefaultCheckoutBranchAction { get; set; }

    /// <summary>
    ///  Upstream's Advanced page: "Don't show help images".
    /// </summary>
    [ObservableProperty]
    public partial bool DontShowHelpImages { get; set; }

    /// <summary>
    ///  Upstream's Advanced page: "Always show advanced options".
    /// </summary>
    [ObservableProperty]
    public partial bool AlwaysShowAdvOpt { get; set; }

    [ObservableProperty]
    public partial bool AutoNormaliseBranchName { get; set; }

    [ObservableProperty]
    public partial bool CommitAndPushForcedWhenAmend { get; set; }

    [ObservableProperty]
    public partial bool EnsureCommitMessageSecondLineEmpty { get; set; }

    [ObservableProperty]
    public partial bool RememberAmendCommitState { get; set; }

    [ObservableProperty]
    public partial decimal? CommitDialogNumberOfPreviousMessages { get; set; }

    [ObservableProperty]
    public partial bool ShowCommitAndPush { get; set; }

    [ObservableProperty]
    public partial bool ShowResetWorkTreeChanges { get; set; }

    [ObservableProperty]
    public partial bool ShowResetAllChanges { get; set; }

    [ObservableProperty]
    public partial bool ShowErrorsWhenStagingFiles { get; set; }

    [ObservableProperty]
    public partial bool ProvideAutocompletion { get; set; }

    [ObservableProperty]
    public partial bool UseFormCommitMessage { get; set; }

    [ObservableProperty]
    public partial bool RememberIgnoreWhiteSpacePreference { get; set; }

    [ObservableProperty]
    public partial bool RememberShowEntireFilePreference { get; set; }

    [ObservableProperty]
    public partial bool RememberNumberOfContextLines { get; set; }

    [ObservableProperty]
    public partial bool RememberShowNonPrintingCharsPreference { get; set; }

    [ObservableProperty]
    public partial bool RememberShowSyntaxHighlightingInDiff { get; set; }

    [ObservableProperty]
    public partial bool AutomaticContinuousScroll { get; set; }

    [ObservableProperty]
    public partial bool UseDiffViewerForBlame { get; set; }

    [ObservableProperty]
    public partial bool OpenSubmoduleDiffInSeparateWindow { get; set; }

    [ObservableProperty]
    public partial bool RememberDiffDisplayAppearance { get; set; }

    [ObservableProperty]
    public partial bool UseGitColoring { get; set; }

    [ObservableProperty]
    public partial bool ReverseGitColoring { get; set; }

    [ObservableProperty]
    public partial bool ShowAvailableDiffTools { get; set; }

    [ObservableProperty]
    public partial bool ShowDiffForAllParents { get; set; }

    /// <summary>
    ///  Upstream's Diff viewer page: "Omit uninteresting changes from combined diff".
    /// </summary>
    [ObservableProperty]
    public partial bool OmitUninterestingDiff { get; set; }

    [ObservableProperty]
    public partial decimal? DiffVerticalRulerPosition { get; set; }

    [ObservableProperty]
    public partial decimal? RecentRepositoriesHistorySize { get; set; }

    [ObservableProperty]
    public partial bool ShowRepoCurrentBranch { get; set; }

    /// <summary>
    ///  Upstream's "Show current branch in Visual Studio" on the Appearance page, shown on Windows only.
    /// </summary>
    [ObservableProperty]
    public partial bool ShowCurrentBranchInVisualStudio { get; set; }

    [ObservableProperty]
    public partial bool ShowAuthorAvatarColumn { get; set; }

    [ObservableProperty]
    public partial bool ShowAuthorAvatarInCommitInfo { get; set; }

    [ObservableProperty]
    public partial bool BlameIgnoreWhitespace { get; set; }

    [ObservableProperty]
    public partial bool BlameDetectMoveInFile { get; set; }

    [ObservableProperty]
    public partial bool BlameDetectMoveInAllFiles { get; set; }

    [ObservableProperty]
    public partial bool BlameDisplayAuthorFirst { get; set; }

    [ObservableProperty]
    public partial bool BlameShowAuthor { get; set; }

    [ObservableProperty]
    public partial bool BlameShowAuthorDate { get; set; }

    [ObservableProperty]
    public partial bool BlameShowAuthorTime { get; set; }

    [ObservableProperty]
    public partial bool BlameShowLineNumbers { get; set; }

    [ObservableProperty]
    public partial bool BlameShowOriginalFilePath { get; set; }

    [ObservableProperty]
    public partial bool BlameShowAuthorAvatar { get; set; }

    [ObservableProperty]
    public partial string? GitCommand { get; set; }

    [ObservableProperty]
    public partial string? LinuxToolsDir { get; set; }

    /// <summary>
    ///  The terminal command of Linux and macOS (<see cref="IAppPreferences.TerminalCommand"/>); empty for the system's.
    /// </summary>
    [ObservableProperty]
    public partial string? TerminalCommand { get; set; }

    /// <summary>
    ///  The terminal the button opens when <see cref="TerminalCommand"/> is empty, shown as the box's placeholder.
    /// </summary>
    public string DefaultTerminalCommand { get; init; } = "";

    /// <summary>
    ///  Upstream's HOME as git sees it, and where it comes from.
    /// </summary>
    [ObservableProperty]
    public partial string HomeText { get; private set; } = "";

    public string DefaultHomeText { get; private set; } = "";

    public string UserProfileHomeText { get; private set; } = "";

    /// <summary>
    ///  The HOME choice on the Git tab (upstream's FormFixHome).
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOtherHome))]
    public partial HomeChoice SelectedHomeChoice { get; set; }

    public bool IsOtherHome => SelectedHomeChoice == HomeChoice.Other;

    [ObservableProperty]
    public partial string? OtherHome { get; set; }

    [ObservableProperty]
    public partial string? Plink { get; set; }

    [ObservableProperty]
    public partial string? Puttygen { get; set; }

    [ObservableProperty]
    public partial string? Pageant { get; set; }

    [ObservableProperty]
    public partial bool AutoStartPageant { get; set; }

    /// <summary>
    ///  The ssh client choice on the SSH tab.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOtherSsh), nameof(IsPutty))]
    public partial SshClientKind SelectedSshClient { get; set; }

    public bool IsOtherSsh => SelectedSshClient == SshClientKind.Other;

    public bool IsPutty => SelectedSshClient == SshClientKind.Putty;

    /// <summary>
    ///  PuTTY is offered on Windows, where upstream looks for it and recognises plink.exe; elsewhere plink is an "other ssh
    ///  client", unless it is the stored choice.
    /// </summary>
    public bool IsPuttyOffered { get; private set; }

    [ObservableProperty]
    public partial string? OtherSsh { get; set; }

    /// <summary>
    ///  As upstream's appearance page: English, then the translations in the Translation folder; the current one stays
    ///  selected even when its file is missing. Read again after the translation window chose one.
    /// </summary>
    public void ShowLanguages(string current)
    {
        List<string> languages = [English, .. _services.Translations()];
        if (current.Length > 0 && !languages.Contains(current))
        {
            languages.Add(current);
        }

        Languages = languages;
        SelectedLanguage = current.Length > 0 ? current : English;
    }

    /// <summary>
    ///  Upstream's FormFixHome checks before it closes: another folder must be typed, and HOME must then exist.
    /// </summary>
    public string? ValidateHome()
    {
        if (!IsWindows)
        {
            return null;
        }

        string other = OtherHome?.Trim() ?? "";
        return HomeSettings.Validate(SelectedHomeChoice, other,
            HomeSettings.HomeFor(SelectedHomeChoice, other, Environment.GetEnvironmentVariable), Directory.Exists);
    }

    /// <summary>
    ///  Stores the values on these pages, the window's pages and git config; true when stored. OK closes after it.
    /// </summary>
    public async Task<bool> SaveAsync(bool close)
    {
        ErrorText = "";
        try
        {
            if (ValidateHome() is { } homeError)
            {
                PageTree.GoToPage(GitPage);
                ErrorText = homeError;
                return false;
            }

            StoreValues();
            _services.SavePagesBeforeStore();
            _preferences.Save();

            // HOME and the Linux tools folder as the next git gets them; upstream sets them before every git start too.
            _services.SetEnvironment();

            // Upstream's CheckHomePath after a save on Windows. Its FormFixHome is the Git page here, so on Yes the window
            // stays open there with the folder the search found, for the user to save.
            bool fixHome = IsWindows && HomeSettings.NeedsFix(_services.HomeLookup) && await AskToFixHomeAsync();
            await _services.SavePagesAfterStore(close && !fixHome);
            Saved = true;
            if (fixHome)
            {
                await ShowFoundHomeAsync();
            }
            else if (close)
            {
                Host.Close(true);
            }

            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ErrorText = ex.Message;
            return false;
        }
    }

    partial void OnShowGitStatusForArtificialCommitsChanged(bool value) => ClampSubmoduleStatus();

    partial void OnShowGitStatusInBrowseToolbarChanged(bool value) => ClampSubmoduleStatus();

    private void GoToPage(string page) => PageTree.GoToPage(page);

    private Task<bool> AskToFixHomeAsync()
        => Host.ConfirmAsync(
            string.Format(Host.Translate(FixHomeCategory, "_gitGlobalConfigNotFound", HomeSettings.GlobalConfigNotFoundText),
                _services.HomeLookup.Variable("HOME")),
            Host.Translate(FixHomeCategory, "_gitGlobalConfigNotFoundCaption", HomeSettings.GlobalConfigNotFoundCaption));

    // Upstream FormFixHome's LoadSettings: the stored choice, which the page shows, then the first folder with a global git
    // config is chosen, as its message says.
    private async Task ShowFoundHomeAsync()
    {
        PageTree.GoToPage(GitPage);
        if (HomeSettings.Locate(_services.HomeLookup) is not { } found)
        {
            return;
        }

        SelectedHomeChoice = found.Choice;
        if (found.Choice == HomeChoice.Other)
        {
            OtherHome = found.Folder;
        }

        (string name, string english) = found.Message;
        await Host.ShowMessageAsync(string.Format(Host.Translate(FixHomeCategory, name, english), found.Folder),
            Host.Translate("Information"));
    }

    // Upstream's OnPageShown: a plugin's page shows the plugin's settings, and the Checklist checks again.
    private void OnPageShown(SettingsPageNode node)
    {
        if (node.Plugin is { } plugin)
        {
            PluginsPage.Show(plugin);
        }

        if (node.Page == SettingsPageTreeViewModel.ChecklistPage)
        {
            Checklist.Start();
        }
    }

    // Upstream's GitPath_TextChanged: what is typed is tried, and the Config page follows it.
    partial void OnGitCommandChanged(string? value)
    {
        if (!_loading)
        {
            Run(GitConfigPage.GitCommandChangedAsync);
        }
    }

    partial void OnSelectedSshClientChanged(SshClientKind value)
    {
        if (value == SshClientKind.Putty && !_loading)
        {
            FindPutty();
        }
    }

    [RelayCommand]
    private Task OkAsync() => SaveAsync(close: true);

    [RelayCommand]
    private Task ApplyAsync() => SaveAsync(close: false);

    // As upstream, the window reports a save made with Apply even when it is then closed with Cancel.
    [RelayCommand]
    private void Cancel() => Host.Close(Saved);

    private void ClampSubmoduleStatus()
    {
        if (!CanShowSubmoduleStatus)
        {
            ShowSubmoduleStatus = false;
        }
    }

    // The values of the one-value-per-preference pages as stored.
    private void LoadValues()
    {
        IAppPreferences p = _preferences;
        ShowEolMarkerAsGlyph = p.ShowEolMarkerAsGlyph;
        MulticolorBranches = p.MulticolorBranches;
        RevisionGraphDrawAlternateBackColor = p.RevisionGraphDrawAlternateBackColor;
        RevisionGraphDrawNonRelativesGray = p.RevisionGraphDrawNonRelativesGray;
        RevisionGraphDrawNonRelativesTextGray = p.RevisionGraphDrawNonRelativesTextGray;
        HighlightAuthoredRevisions = p.HighlightAuthoredRevisions;
        FillRefLabels = p.FillRefLabels;
        CloseCommitDialogAfterCommit = p.CloseCommitDialogAfterCommit;
        CloseProcessDialog = p.CloseProcessDialog;
        ShowGitCommandLine = p.ShowGitCommandLine;
        StartWithRecentWorkingDir = p.StartWithRecentWorkingDir;
        FollowRenames = p.FileHistory.FollowRenames;
        ExactRenamesOnly = p.FileHistory.ExactRenamesOnly;
        UseHistogramDiffAlgorithm = p.UseHistogramDiffAlgorithm;
        IncludeUntrackedFilesInAutoStash = p.IncludeUntrackedFilesInAutoStash;
        UpdateSubmodulesOnCheckout = p.UpdateSubmodulesOnCheckout;
        CheckForUncommittedChangesInCheckoutBranch = p.CheckForUncommittedChangesInCheckoutBranch;
        RevisionGridQuickSearchTimeout = p.RevisionGridQuickSearchTimeout;
        ShowGitStatusForArtificialCommits = p.ShowGitStatusForArtificialCommits;
        ShowAheadBehindData = p.ShowAheadBehindData;
        ShowStashCount = p.ShowStashCount;
        LimitCommits = p.MaxRevisionGraphCommits != 0;
        MaxRevisionGraphCommits = p.MaxRevisionGraphCommits;
        ShowGitStatusInBrowseToolbar = p.ShowGitStatusInBrowseToolbar;
        ShowSubmoduleStatus = p.ShowSubmoduleStatus && CanShowSubmoduleStatus;
        ShowRevisionGridTooltips = p.ShowRevisionGridTooltips;
        ShowFindInCommitFilesGitGrep = p.ShowFindInCommitFilesGitGrep;
        ShowGpgInformation = p.ShowGpgInformation;
        ShowOutputHistoryAsTab = p.ShowOutputHistoryAsTab;
        OutputHistoryDepth = Math.Clamp(p.OutputHistoryDepth, 0, MaxOutputHistoryDepth);
        MergeGraphLanesHavingCommonParent = p.MergeGraphLanesHavingCommonParent;
        RenderGraphWithDiagonals = p.RenderGraphWithDiagonals;
        StraightenGraphDiagonals = p.StraightenGraphDiagonals;
        DefaultCloneDestinationPath = p.DefaultCloneDestinationPath;
        RelativeDate = p.RelativeDate;
        AlwaysShowCheckoutBranchDlg = p.AlwaysShowCheckoutBranchDlg;
        UseDefaultCheckoutBranchAction = p.UseDefaultCheckoutBranchAction;
        DontShowHelpImages = p.DontShowHelpImages;
        AlwaysShowAdvOpt = p.AlwaysShowAdvOpt;
        AutoNormaliseBranchName = p.AutoNormaliseBranchName;
        CommitAndPushForcedWhenAmend = p.CommitAndPushForcedWhenAmend;
        EnsureCommitMessageSecondLineEmpty = p.EnsureCommitMessageSecondLineEmpty;
        RememberAmendCommitState = p.RememberAmendCommitState;
        CommitDialogNumberOfPreviousMessages = p.CommitDialogNumberOfPreviousMessages;
        ShowCommitAndPush = p.ShowCommitAndPush;
        ShowResetWorkTreeChanges = p.ShowResetWorkTreeChanges;
        ShowResetAllChanges = p.ShowResetAllChanges;
        ShowErrorsWhenStagingFiles = p.ShowErrorsWhenStagingFiles;
        ProvideAutocompletion = p.ProvideAutocompletion;
        UseFormCommitMessage = p.UseFormCommitMessage;
        RememberIgnoreWhiteSpacePreference = p.RememberIgnoreWhiteSpacePreference;
        RememberShowEntireFilePreference = p.RememberShowEntireFilePreference;
        RememberNumberOfContextLines = p.RememberNumberOfContextLines;
        RememberShowNonPrintingCharsPreference = p.RememberShowNonPrintingCharsPreference;
        RememberShowSyntaxHighlightingInDiff = p.RememberShowSyntaxHighlightingInDiff;
        AutomaticContinuousScroll = p.AutomaticContinuousScroll;
        UseDiffViewerForBlame = p.UseDiffViewerForBlame;
        OpenSubmoduleDiffInSeparateWindow = p.OpenSubmoduleDiffInSeparateWindow;
        RememberDiffDisplayAppearance = p.RememberDiffDisplayAppearance;
        UseGitColoring = p.UseGitColoring;
        ReverseGitColoring = p.ReverseGitColoring;
        ShowAvailableDiffTools = p.ShowAvailableDiffTools;
        ShowDiffForAllParents = p.ShowDiffForAllParents;
        OmitUninterestingDiff = p.OmitUninterestingDiff;
        DiffVerticalRulerPosition = p.DiffVerticalRulerPosition;
        RecentRepositoriesHistorySize = p.RecentRepositoriesHistorySize;
        ShowRepoCurrentBranch = p.ShowRepoCurrentBranch;
        ShowCurrentBranchInVisualStudio = p.ShowCurrentBranchInVisualStudio;
        if (IsWindows)
        {
            ShellExtensionPage.Load(p);
            ShellExtensionPage.UpdateRegistrationStatus();
        }

        ShowAuthorAvatarColumn = p.ShowAuthorAvatarColumn;
        ShowAuthorAvatarInCommitInfo = p.ShowAuthorAvatarInCommitInfo;
        PrioritizedBranchNames = p.PrioritizedBranchNames;
        PrioritizedRemoteNames = p.PrioritizedRemoteNames;

        BlameOptions blame = p.BlameOptions;
        BlameIgnoreWhitespace = blame.IgnoreWhitespace;
        BlameDetectMoveInFile = blame.DetectMoveInFile;
        BlameDetectMoveInAllFiles = blame.DetectMoveInAllFiles;
        BlameDisplayAuthorFirst = blame.DisplayAuthorFirst;
        BlameShowAuthor = blame.ShowAuthor;
        BlameShowAuthorDate = blame.ShowAuthorDate;
        BlameShowAuthorTime = blame.ShowAuthorTime;
        BlameShowLineNumbers = blame.ShowLineNumbers;
        BlameShowOriginalFilePath = blame.ShowOriginalFilePath;
        BlameShowAuthorAvatar = blame.ShowAuthorAvatar;
    }

    // Upstream's Git page and SSH page.
    private void LoadGitAndSshPages()
    {
        GitCommand = _preferences.GitCommand;
        LinuxToolsDir = _preferences.LinuxToolsDir;
        TerminalCommand = _preferences.TerminalCommand;

        _services.SetEnvironment();
        HomeText = HomeSettings.Describe(
            EnvironmentConfiguration.GetEnvironmentVariable(HomeSettings.GitConfigGlobalVariable),
            EnvironmentConfiguration.GetHomeDir(), IsWindows);
        DefaultHomeText = $"Use default for HOME ({EnvironmentConfiguration.GetDefaultHomeDir()})";
        UserProfileHomeText = $"Set HOME to USERPROFILE ({Environment.GetEnvironmentVariable("USERPROFILE")})";
        SelectedHomeChoice = HomeSettings.ChoiceOf(_preferences.CustomHomeDir, _preferences.UserProfileHomeDir);
        OtherHome = _preferences.CustomHomeDir;

        Plink = _preferences.Plink;
        Puttygen = _preferences.Puttygen;
        Pageant = _preferences.Pageant;
        AutoStartPageant = _preferences.AutoStartPageant;
        SshClientKind kind = SshClients.KindOf(_preferences.SshPath);
        IsPuttyOffered = IsWindows || kind == SshClientKind.Putty;
        OtherSsh = kind == SshClientKind.Other ? _preferences.SshPath : null;
        SelectedSshClient = kind;
    }

    // Upstream looks for PuTTY's programs when PuTTY is chosen; true when all three are found.
    private bool FindPutty()
    {
        if (!IsWindows || _services.GitLookup is not { } lookup)
        {
            return false;
        }

        PuttyPaths found = SshClients.FindPutty(new PuttyPaths(Plink ?? "", Puttygen ?? "", Pageant ?? ""),
            SshClients.PuttyLocations(lookup.Environment, lookup.Is64BitProcess), lookup.FileExists);
        Plink = found.Plink;
        Puttygen = found.Puttygen;
        Pageant = found.Pageant;
        return lookup.FileExists(found.Plink) && lookup.FileExists(found.Puttygen) && lookup.FileExists(found.Pageant);
    }

    // The Checklist's own repairs (upstream's GitFound_Click, GitBinFound_Click, SshConfig_Click): what is found goes on
    // the Git or SSH page, from where the Checklist's save stores it.
    private async Task<string?> LocateAsync(ChecklistItem item)
    {
        Platform.WindowsGitLookup lookup = _services.GitLookup!;
        string gitCommand = GitCommand?.Trim() ?? "";
        switch (item)
        {
            case ChecklistItem.Git:
                if (await Task.Run(() => Platform.WindowsGit.SolveGitCommand(gitCommand, null, lookup)) is not { } command)
                {
                    return null;
                }

                GitCommand = command;
                return command;
            case ChecklistItem.LinuxTools:
                if (await Task.Run(() => Platform.WindowsGit.SolveLinuxToolsDir(gitCommand, LinuxToolsDir?.Trim() ?? "",
                        lookup)) is not { } folder)
                {
                    return null;
                }

                LinuxToolsDir = folder;
                return folder;
            case ChecklistItem.Ssh:
                return SelectedSshClient == SshClientKind.Putty && FindPutty() ? Plink : null;
            default:
                return null;
        }
    }

    private void StoreValues()
    {
        IAppPreferences p = _preferences;
        if (SelectedTheme is { } theme)
        {
            p.Theme = theme.Id;
        }

        p.ThemeVariations = Colorblind ? [ThemeVariations.Colorblind] : [];
        p.MulticolorBranches = MulticolorBranches;
        p.RevisionGraphDrawAlternateBackColor = RevisionGraphDrawAlternateBackColor;
        p.RevisionGraphDrawNonRelativesGray = RevisionGraphDrawNonRelativesGray;
        p.RevisionGraphDrawNonRelativesTextGray = RevisionGraphDrawNonRelativesTextGray;
        p.HighlightAuthoredRevisions = HighlightAuthoredRevisions;
        p.FillRefLabels = FillRefLabels;
        p.CloseCommitDialogAfterCommit = CloseCommitDialogAfterCommit;
        p.CloseProcessDialog = CloseProcessDialog;
        p.ShowGitCommandLine = ShowGitCommandLine;
        p.StartWithRecentWorkingDir = StartWithRecentWorkingDir;
        p.FileHistory = p.FileHistory with { FollowRenames = FollowRenames, ExactRenamesOnly = ExactRenamesOnly };
        p.UseHistogramDiffAlgorithm = UseHistogramDiffAlgorithm;
        p.IncludeUntrackedFilesInAutoStash = IncludeUntrackedFilesInAutoStash;
        p.UpdateSubmodulesOnCheckout = UpdateSubmodulesOnCheckout;
        p.CheckForUncommittedChangesInCheckoutBranch = CheckForUncommittedChangesInCheckoutBranch;
        if (SelectedPullAction is { } pullAction)
        {
            p.DefaultPullAction = pullAction.Action;
        }

        p.RevisionGridQuickSearchTimeout = (int)(RevisionGridQuickSearchTimeout ?? DefaultQuickSearchTimeout);
        p.ShowGitStatusForArtificialCommits = ShowGitStatusForArtificialCommits;
        p.ShowRepoCurrentBranch = ShowRepoCurrentBranch;

        // The Visual Studio extension and the Explorer shell extension are Windows'; their settings are stored there only.
        if (IsWindows)
        {
            p.ShowCurrentBranchInVisualStudio = ShowCurrentBranchInVisualStudio;
            ShellExtensionPage.Store(p);
        }

        if (SelectedTruncatePath is { } truncate)
        {
            p.TruncatePathMethod = truncate.Method;
        }

        AvatarOptions avatars = p.Avatars;
        p.ShowAuthorAvatarColumn = ShowAuthorAvatarColumn;
        p.ShowAuthorAvatarInCommitInfo = ShowAuthorAvatarInCommitInfo;
        p.Avatars = new AvatarOptions(SelectedAvatarProvider?.Provider ?? avatars.Provider,
            SelectedAvatarFallback?.Fallback ?? avatars.Fallback, CustomAvatarTemplate ?? "",
            (int)(AvatarCacheDays ?? avatars.CacheDays));
        p.ShowAheadBehindData = ShowAheadBehindData;
        p.ShowStashCount = ShowStashCount;
        p.MaxRevisionGraphCommits = LimitCommits ? (int)(MaxRevisionGraphCommits ?? 0) : 0;
        p.ShowGitStatusInBrowseToolbar = ShowGitStatusInBrowseToolbar;
        p.ShowSubmoduleStatus = ShowSubmoduleStatus;
        p.ShowRevisionGridTooltips = ShowRevisionGridTooltips;
        p.ShowFindInCommitFilesGitGrep = ShowFindInCommitFilesGitGrep;
        p.ShowGpgInformation = ShowGpgInformation;

        // Upstream's FormBrowseRepoSettingsPage: a changed choice also shows the panel when the history is a panel.
        int outputHistoryDepth = (int)(OutputHistoryDepth ?? 0);
        if (p.ShowOutputHistoryAsTab != ShowOutputHistoryAsTab || p.OutputHistoryDepth != outputHistoryDepth)
        {
            p.ShowOutputHistoryAsTab = ShowOutputHistoryAsTab;
            p.OutputHistoryDepth = outputHistoryDepth;
            p.OutputHistoryPanelVisible = !ShowOutputHistoryAsTab && outputHistoryDepth > 0;
        }

        p.MergeGraphLanesHavingCommonParent = MergeGraphLanesHavingCommonParent;
        p.RenderGraphWithDiagonals = RenderGraphWithDiagonals;
        p.StraightenGraphDiagonals = StraightenGraphDiagonals;
        p.DefaultCloneDestinationPath = DefaultCloneDestinationPath?.Trim() ?? "";
        p.RelativeDate = RelativeDate;
        if (SelectedLanguage is { } language)
        {
            p.Translation = language;
        }

        if (SelectedDictionary is { } dictionary)
        {
            p.Dictionary = dictionary == CommitSpellingNone ? NoDictionaryStored : dictionary;
        }

        p.AlwaysShowCheckoutBranchDlg = AlwaysShowCheckoutBranchDlg;
        p.UseDefaultCheckoutBranchAction = UseDefaultCheckoutBranchAction;
        p.DontShowHelpImages = DontShowHelpImages;
        p.AlwaysShowAdvOpt = AlwaysShowAdvOpt;
        p.AutoNormaliseBranchName = AutoNormaliseBranchName;
        if (SelectedNormaliseSymbol is { } symbol)
        {
            p.AutoNormaliseSymbol = symbol.Symbol;
        }

        p.CommitAndPushForcedWhenAmend = CommitAndPushForcedWhenAmend;
        p.EnsureCommitMessageSecondLineEmpty = EnsureCommitMessageSecondLineEmpty;
        p.RememberAmendCommitState = RememberAmendCommitState;
        p.CommitDialogNumberOfPreviousMessages =
            (int)(CommitDialogNumberOfPreviousMessages ?? p.CommitDialogNumberOfPreviousMessages);
        p.ShowCommitAndPush = ShowCommitAndPush;
        p.ShowResetWorkTreeChanges = ShowResetWorkTreeChanges;
        p.ShowResetAllChanges = ShowResetAllChanges;
        p.ShowErrorsWhenStagingFiles = ShowErrorsWhenStagingFiles;
        p.ProvideAutocompletion = ProvideAutocompletion;
        p.UseFormCommitMessage = UseFormCommitMessage;
        p.RememberIgnoreWhiteSpacePreference = RememberIgnoreWhiteSpacePreference;
        p.RememberShowEntireFilePreference = RememberShowEntireFilePreference;
        p.RememberNumberOfContextLines = RememberNumberOfContextLines;
        p.RememberShowNonPrintingCharsPreference = RememberShowNonPrintingCharsPreference;
        p.RememberShowSyntaxHighlightingInDiff = RememberShowSyntaxHighlightingInDiff;
        p.AutomaticContinuousScroll = AutomaticContinuousScroll;
        p.UseDiffViewerForBlame = UseDiffViewerForBlame;
        p.OpenSubmoduleDiffInSeparateWindow = OpenSubmoduleDiffInSeparateWindow;
        p.RememberDiffDisplayAppearance = RememberDiffDisplayAppearance;
        p.UseGitColoring = UseGitColoring;
        p.ReverseGitColoring = ReverseGitColoring;
        p.ShowAvailableDiffTools = ShowAvailableDiffTools;
        p.ShowDiffForAllParents = ShowDiffForAllParents;
        p.OmitUninterestingDiff = OmitUninterestingDiff;
        p.DiffVerticalRulerPosition = (int)(DiffVerticalRulerPosition ?? 0);
        p.RecentRepositoriesHistorySize = (int)(RecentRepositoriesHistorySize ?? p.RecentRepositoriesHistorySize);
        StoreGitAndSshPages();
        p.BlameOptions = new BlameOptions(BlameIgnoreWhitespace, BlameDetectMoveInFile, BlameDetectMoveInAllFiles,
            BlameDisplayAuthorFirst, BlameShowAuthor, BlameShowAuthorDate, BlameShowAuthorTime, BlameShowLineNumbers,
            BlameShowOriginalFilePath, BlameShowAuthorAvatar);
        p.RevisionSortOrder = SelectedRevisionSort?.Value ?? p.RevisionSortOrder;
        p.RefsSortBy = SelectedRefsSortBy?.Value ?? p.RefsSortBy;
        p.RefsSortOrder = SelectedRefsSortOrder?.Value ?? p.RefsSortOrder;
        p.PrioritizedBranchNames = PrioritizedBranchNames ?? "";
        p.PrioritizedRemoteNames = PrioritizedRemoteNames ?? "";
        p.ShowEolMarkerAsGlyph = ShowEolMarkerAsGlyph;

        // Only a font the user changed is written; "(default)" removes the stored one.
        foreach (FontChoice font in Fonts)
        {
            if (font.Chosen != font.Stored)
            {
                p.SetFont(font.Font, font.Chosen);
            }
        }

        foreach (ConfirmationChoice choice in ConfirmationChoices)
        {
            Confirmations.SetCheckState(p, choice.Confirmation, choice.CheckState);
        }
    }

    private void StoreGitAndSshPages()
    {
        _preferences.GitCommand = GitCommand?.Trim() ?? "";
        if (!IsWindows)
        {
            _preferences.TerminalCommand = TerminalCommand?.Trim() ?? "";
        }

        if (IsWindows)
        {
            _preferences.LinuxToolsDir = LinuxToolsDir?.Trim() ?? "";
            _preferences.CustomHomeDir = SelectedHomeChoice == HomeChoice.Other ? OtherHome?.Trim() ?? "" : "";
            _preferences.UserProfileHomeDir = SelectedHomeChoice == HomeChoice.UserProfile;
        }

        _preferences.Plink = Plink?.Trim() ?? "";
        _preferences.Puttygen = Puttygen?.Trim() ?? "";
        _preferences.Pageant = Pageant?.Trim() ?? "";
        _preferences.AutoStartPageant = AutoStartPageant;

        // As upstream's page: the setting and the variable git reads, so the next git started uses the client.
        string sshPath = SshClients.PathFor(SelectedSshClient, _preferences.Plink, OtherSsh?.Trim() ?? "");
        _preferences.SshPath = sshPath;
        _services.ApplySshPath(sshPath);
    }

    private static string CommitSpellingNone => Spelling.CommitSpellChecker.NoDictionary;
}

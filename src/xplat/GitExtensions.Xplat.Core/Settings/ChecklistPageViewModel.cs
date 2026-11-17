using System.Drawing;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitCommands;
using GitCommands.Git;

namespace GitExtensions.Xplat.Core.Settings;

/// <summary>
///  A row of the Checklist page, in upstream's colors for its state.
/// </summary>
public sealed class ChecklistRow(ChecklistEntry entry, Color background)
{
    public ChecklistEntry Entry { get; } = entry;

    public ChecklistItem Item => Entry.Item;

    public string Text => Entry.Text;

    public Color Background { get; } = background;

    public Color Foreground { get; } = SettingsChecklist.TextColor(background);

    /// <summary>
    ///  True when the row has a Repair button: the setting is not right.
    /// </summary>
    public bool NeedsRepair => Entry.State != ChecklistState.Set;
}

/// <summary>
///  What <see cref="ChecklistPageViewModel"/> asks of the settings window.
/// </summary>
public sealed record ChecklistPageServices
{
    /// <summary>
    ///  What the checks look at (<see cref="ChecklistFactsReader"/>).
    /// </summary>
    public required Func<Task<ChecklistFacts>> ReadFacts { get; init; }

    /// <summary>
    ///  Stores the window's values (Apply), as "Save and rescan" does first.
    /// </summary>
    public required Func<Task> Save { get; init; }

    /// <summary>
    ///  Shows the page of upstream's page type, where a setting is fixed.
    /// </summary>
    public required Action<string> GoToPage { get; init; }

    /// <summary>
    ///  Upstream's FormChooseTranslation; the settings are stored and checked again after it.
    /// </summary>
    public required Func<Task> ChooseLanguage { get; init; }

    public required Func<Uri, Task> OpenUri { get; init; }

    /// <summary>
    ///  The editor background of a dark theme, toward which upstream dims the rows' colors; null in a light theme.
    /// </summary>
    public Func<Color?> DarkBackground { get; init; } = () => null;

    public required Action<Exception> ShowError { get; init; }
}

/// <summary>
///  Upstream's <c>ChecklistSettingsPage</c>: the basic settings checked each time the page shows, a row per check, and the
///  Repair buttons that show the page to fix a setting on.
/// </summary>
public sealed partial class ChecklistPageViewModel(ChecklistPageServices services) : ObservableObject
{
    private int _run;

    /// <summary>
    ///  The rows, once the checks ran.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Entries))]
    public partial IReadOnlyList<ChecklistRow> Rows { get; private set; } = [];

    public IReadOnlyList<ChecklistEntry> Entries => [.. Rows.Select(row => row.Entry)];

    /// <summary>
    ///  True when every checked setting is right.
    /// </summary>
    [ObservableProperty]
    public partial bool IsValid { get; private set; }

    /// <summary>
    ///  Runs the checks; a run started later wins over this one.
    /// </summary>
    public async Task RunAsync()
    {
        int run = ++_run;
        ChecklistFacts facts = await services.ReadFacts();
        if (run != _run)
        {
            return;
        }

        IReadOnlyList<ChecklistEntry> entries = SettingsChecklist.Check(facts);
        Color? darkBackground = services.DarkBackground();
        IsValid = SettingsChecklist.IsValid(facts, entries);
        Rows = [.. entries.Select(entry => new ChecklistRow(entry, SettingsChecklist.RowColor(entry.State, darkBackground)))];
    }

    /// <summary>
    ///  Runs the checks without waiting for them, as the page shows; an error is shown on the window.
    /// </summary>
    public void Start() => _ = GuardAsync(RunAsync);

    // Upstream's SaveAndRescan_Click: the window's values are stored first, so the checks see them.
    [RelayCommand]
    private Task SaveAndRescanAsync() => GuardAsync(SaveAndRescanCoreAsync);

    // Upstream repairs some settings itself (locating git, PuTTY or sh) and shows the page of the others; here each shows
    // its page, and the tools rescan once one is chosen. Upstream's rows are flat buttons that repair as Repair does.
    [RelayCommand]
    private Task RepairAsync(ChecklistRow row) => GuardAsync(async () =>
    {
        switch (row.Item)
        {
            case ChecklistItem.Git or ChecklistItem.LinuxTools:
                services.GoToPage("GitSettingsPage");
                break;
            case ChecklistItem.UserName:
                services.GoToPage("GitConfigSettingsPage");
                break;
            case ChecklistItem.MergeTool or ChecklistItem.DiffTool:
                if (row.Text is SettingsChecklist.ConfigureMergeTool or SettingsChecklist.AdviceDiffToolConfiguration)
                {
                    services.GoToPage("GitConfigSettingsPage");
                }
                else
                {
                    await SaveAndRescanCoreAsync();
                }

                break;
            case ChecklistItem.Ssh:
                services.GoToPage("SshSettingsPage");
                break;
            case ChecklistItem.Language:
                // Upstream's translationConfig_Click.
                await services.ChooseLanguage();
                await SaveAndRescanCoreAsync();
                break;
            case ChecklistItem.CredentialWinStore:
                await services.OpenUri(new Uri(SettingsChecklist.CredentialWinStoreHelp));
                break;
        }
    });

    private async Task SaveAndRescanCoreAsync()
    {
        await services.Save();
        await RunAsync();
    }

    private async Task GuardAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            services.ShowError(ex);
        }
    }
}

/// <summary>
///  Reads what upstream's checklist looks at: git, git config, the app's settings and the file system.
/// </summary>
public sealed class ChecklistFactsReader(
    IAppPreferences preferences,
    IGitConfigService gitConfig,
    IDiffMergeToolCatalog diffMergeTools,
    string? repositoryPath,
    bool isWindows)
{
    // Upstream's ConfigureMergeTool keys: the GUI tool first, then the plain one; the diff tool the same way, as the
    // browse window starts it.
    private static readonly string[] _mergeToolKeys = ["merge.guitool", "merge.tool"];
    private static readonly string[] _diffToolKeys = ["diff.guitool", "diff.tool"];

    /// <summary>
    ///  The version of a git command, or null when it is not found; the default asks git.
    /// </summary>
    public Func<string, GitVersion?> GitVersionOf { get; init; } = command =>
        XplatGitDiscovery.Discover(command) is { Status: not GitDiscoveryStatus.NotFound } git ? git.Version : null;

    public Func<string, string?> Environment { get; init; } = System.Environment.GetEnvironmentVariable;

    public Func<string, bool> FileExists { get; init; } = File.Exists;

    public async Task<ChecklistFacts> ReadAsync()
    {
        string gitCommand = preferences.GitCommand;
        GitVersion? gitVersion = await Task.Run(() => GitVersionOf(gitCommand));
        string? mergeTool = await FirstSetAsync(_mergeToolKeys);
        string? diffTool = await FirstSetAsync(_diffToolKeys);
        string? mergeToolCommand = await ToolCommandAsync(mergeTool, diff: false);
        string? diffToolCommand = await ToolCommandAsync(diffTool, diff: true);
        return new ChecklistFacts
        {
            GitVersion = gitVersion,
            UserName = await GlobalAsync("user.name"),
            UserEmail = await GlobalAsync("user.email"),
            Editor =
                new[]
                {
                    Environment("GIT_EDITOR"), await GlobalAsync("core.editor"), Environment("VISUAL"),
                    Environment("EDITOR"),
                }.FirstOrDefault(editor => !string.IsNullOrEmpty(editor)),
            MergeTool = mergeTool,
            MergeToolCommand = mergeToolCommand,
            MergeToolKnownToGit = await KnownToGitAsync(mergeTool, mergeToolCommand, diff: false),
            DiffTool = diffTool,
            DiffToolCommand = diffToolCommand,
            DiffToolKnownToGit = await KnownToGitAsync(diffTool, diffToolCommand, diff: true),
            Translation = preferences.Translation,
            IsWindows = isWindows,
            LinuxToolsFound = isWindows && LinuxToolsFound(preferences.LinuxToolsDir),
            SshPath = preferences.SshPath,
            SshPathExists = FileExists(preferences.SshPath),
            PuttyFilesExist = FileExists(preferences.Plink) && FileExists(preferences.Puttygen) &&
                              FileExists(preferences.Pageant),
            CredentialHelper = await GlobalAsync("credential.helper"),
        };

        async Task<string?> FirstSetAsync(IEnumerable<string> keys)
        {
            foreach (string key in keys)
            {
                if (await EffectiveAsync(key) is { } value)
                {
                    return value;
                }
            }

            return null;
        }

        async Task<string?> ToolCommandAsync(string? tool, bool diff)
        {
            if (tool is null)
            {
                return null;
            }

            Dictionary<string, string?> values = [];
            foreach (string prefix in (string[])["difftool", "mergetool"])
            {
                foreach (string setting in (string[])["cmd", "path"])
                {
                    string key = $"{prefix}.{tool}.{setting}";
                    values[key] = await EffectiveAsync(key);
                }
            }

            // Upstream's tool list looks for the tool's program on disk.
            return await Task.Run(() =>
                SettingsChecklist.ToolCommand(tool, diff, key => values.GetValueOrDefault(key)));
        }

        // git's own tool list (git mergetool --tool-help) takes seconds, so it is asked only for a tool upstream has no
        // command for.
        async Task<bool> KnownToGitAsync(string? tool, string? command, bool diff)
            => tool is not null && string.IsNullOrWhiteSpace(command) &&
               (await diffMergeTools.GetAvailableAsync(diff)).Contains(tool);
    }

    private async Task<string?> GlobalAsync(string key)
        => await gitConfig.GetAsync(ConfigScope.Global, key) is { Length: > 0 } value ? value : null;

    // The repository's value, or the global one, as git takes them (the system level aside).
    private async Task<string?> EffectiveAsync(string key)
        => repositoryPath is not null &&
           await gitConfig.GetAsync(ConfigScope.Local, key, repositoryPath) is { Length: > 0 } local
            ? local
            : await GlobalAsync(key);

    // Upstream's CheckGitExe: sh in the linux tools folder or on the PATH.
    private bool LinuxToolsFound(string linuxToolsDir)
    {
        string[] names = ["sh.exe", "sh"];
        IEnumerable<string> folders = (Environment("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries).Prepend(linuxToolsDir);
        return folders.Where(folder => folder.Length > 0)
            .Any(folder => names.Any(name => FileExists(Path.Combine(folder, name))));
    }
}

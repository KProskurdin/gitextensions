using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using GitCommands;
using GitExtensions.Xplat.Core.Settings;

namespace GitExtensions.Xplat.App;

// Upstream's ChecklistSettingsPage: the basic settings checked each time the page shows, a row per check, and the Repair
// buttons that show the page to fix a setting on.
public partial class SettingsWindow
{
    // Upstream's ConfigureMergeTool keys: the GUI tool first, then the plain one; the diff tool the same way, as the
    // browse window starts it.
    private static readonly string[] _mergeToolKeys = ["merge.guitool", "merge.tool"];
    private static readonly string[] _diffToolKeys = ["diff.guitool", "diff.tool"];

    private int _checklistRun;

    /// <summary>
    ///  The rows the Checklist page shows, once its checks ran.
    /// </summary>
    public IReadOnlyList<ChecklistEntry> ChecklistEntries { get; private set; } = [];

    /// <summary>
    ///  True when every checked setting is right.
    /// </summary>
    public bool IsChecklistValid { get; private set; }

    private void ShowChecklistPage()
        => ChecklistRescanButton.Click +=
            (_, _) => UiActions.Run(SaveAndRescanAsync, ex => ErrorText.Text = ex.Message);

    // Upstream's OnPageShown and the instant save notice of FormSettings.
    private void OnPageShown(TabItem page)
    {
        InstantSaveNoticeText.IsVisible = page == ChecklistTab;
        if (page == ChecklistTab)
        {
            UiActions.Run(RunChecklistAsync, ex => ErrorText.Text = ex.Message);
        }
    }

    private async Task RunChecklistAsync()
    {
        int run = ++_checklistRun;
        ChecklistFacts facts = await ReadChecklistFactsAsync();
        if (run != _checklistRun)
        {
            return;
        }

        ChecklistEntries = SettingsChecklist.Check(facts);
        IsChecklistValid = SettingsChecklist.IsValid(facts, ChecklistEntries);
        ShowChecklistRows();
    }

    // Upstream's SaveAndRescan_Click: the window's values are stored first, so the checks see them.
    private async Task SaveAndRescanAsync()
    {
        await SaveAsync(close: false);
        await RunChecklistAsync();
    }

    private void ShowChecklistRows()
    {
        ChecklistGrid.Children.Clear();
        ChecklistGrid.RowDefinitions.Clear();
        System.Drawing.Color? darkBackground = DarkEditorBackground();
        foreach (ChecklistEntry entry in ChecklistEntries)
        {
            int row = ChecklistGrid.RowDefinitions.Count;
            ChecklistGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            System.Drawing.Color color = SettingsChecklist.RowColor(entry.State, darkBackground);
            System.Drawing.Color textColor = SettingsChecklist.TextColor(color);

            // Upstream's rows are flat buttons: a click repairs as the Repair button does.
            Button status = new()
            {
                Content = new TextBlock { Text = entry.Text, TextWrapping = TextWrapping.Wrap },
                Background = new SolidColorBrush(Color.FromRgb(color.R, color.G, color.B)),
                Foreground = new SolidColorBrush(Color.FromRgb(textColor.R, textColor.G, textColor.B)),
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 4),
                Tag = entry.Item,
            };
            status.Click += (_, _) => Repair(entry);
            Grid.SetRow(status, row);
            ChecklistGrid.Children.Add(status);
            if (entry.State != ChecklistState.Set)
            {
                Button repair = new()
                {
                    Content = SettingsChecklist.RepairText, VerticalAlignment = VerticalAlignment.Top
                };
                repair.Click += (_, _) => Repair(entry);
                Grid.SetRow(repair, row);
                Grid.SetColumn(repair, 2);
                ChecklistGrid.Children.Add(repair);
            }
        }
    }

    // Upstream repairs some settings itself (locating git, PuTTY or sh) and shows the page of the others; here each shows
    // its page, and the tools rescan once one is chosen.
    private void Repair(ChecklistEntry entry)
    {
        switch (entry.Item)
        {
            case ChecklistItem.Git or ChecklistItem.LinuxTools:
                GoToTab(GitTab);
                break;
            case ChecklistItem.UserName:
                GoToTab(GitConfigTab);
                break;
            case ChecklistItem.MergeTool or ChecklistItem.DiffTool:
                if (entry.Text is SettingsChecklist.ConfigureMergeTool or SettingsChecklist.AdviceDiffToolConfiguration)
                {
                    GoToTab(GitConfigTab);
                }
                else
                {
                    UiActions.Run(SaveAndRescanAsync, ex => ErrorText.Text = ex.Message);
                }

                break;
            case ChecklistItem.Ssh:
                GoToTab(SshTab);
                break;
            case ChecklistItem.Language:
                GoToTab(AppearanceTab);
                break;
            case ChecklistItem.CredentialWinStore:
                UiActions.Run(() => Launcher.LaunchUriAsync(new Uri(SettingsChecklist.CredentialWinStoreHelp)),
                    ex => ErrorText.Text = ex.Message);
                break;
        }
    }

    private void GoToTab(TabItem tab) => GoToPage(_allPageNodes.First(node => node.Page == tab));

    private async Task<ChecklistFacts> ReadChecklistFactsAsync()
    {
        string gitCommand = _preferences.GitCommand;
        GitDiscoveryResult git = await Task.Run(() => XplatGitDiscovery.Discover(gitCommand));
        string? mergeTool = await FirstSetAsync(_mergeToolKeys);
        string? diffTool = await FirstSetAsync(_diffToolKeys);
        string? mergeToolCommand = await ToolCommandAsync(mergeTool, diff: false);
        string? diffToolCommand = await ToolCommandAsync(diffTool, diff: true);
        bool windows = OperatingSystem.IsWindows();
        return new ChecklistFacts
        {
            GitVersion = git.Status == GitDiscoveryStatus.NotFound ? null : git.Version,
            UserName = await GlobalAsync("user.name"),
            UserEmail = await GlobalAsync("user.email"),
            Editor =
                new[]
                {
                    Environment.GetEnvironmentVariable("GIT_EDITOR"), await GlobalAsync("core.editor"),
                    Environment.GetEnvironmentVariable("VISUAL"), Environment.GetEnvironmentVariable("EDITOR"),
                }.FirstOrDefault(editor => !string.IsNullOrEmpty(editor)),
            MergeTool = mergeTool,
            MergeToolCommand = mergeToolCommand,
            MergeToolKnownToGit = await KnownToGitAsync(mergeTool, mergeToolCommand, diff: false),
            DiffTool = diffTool,
            DiffToolCommand = diffToolCommand,
            DiffToolKnownToGit = await KnownToGitAsync(diffTool, diffToolCommand, diff: true),
            Translation = _preferences.Translation,
            IsWindows = windows,
            LinuxToolsFound = windows && LinuxToolsFound(_preferences.LinuxToolsDir),
            SshPath = _preferences.SshPath,
            SshPathExists = File.Exists(_preferences.SshPath),
            PuttyFilesExist = File.Exists(_preferences.Plink) && File.Exists(_preferences.Puttygen) &&
                              File.Exists(_preferences.Pageant),
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
               (await AppServices.DiffMergeTools.GetAvailableAsync(diff)).Contains(tool);
    }

    private async Task<string?> GlobalAsync(string key)
        => await _gitConfig.GetAsync(ConfigScope.Global, key) is { Length: > 0 } value ? value : null;

    // The repository's value, or the global one, as git takes them (the system level aside).
    private async Task<string?> EffectiveAsync(string key)
        => _repositoryPath is not null &&
           await _gitConfig.GetAsync(ConfigScope.Local, key, _repositoryPath) is { Length: > 0 } local
            ? local
            : await GlobalAsync(key);

    // Upstream's CheckGitExe: sh in the linux tools folder or on the PATH.
    private static bool LinuxToolsFound(string linuxToolsDir)
    {
        string[] names = ["sh.exe", "sh"];
        IEnumerable<string> folders = (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries).Prepend(linuxToolsDir);
        return folders.Where(folder => folder.Length > 0)
            .Any(folder => names.Any(name => File.Exists(Path.Combine(folder, name))));
    }

    // Upstream dims the rows' colors toward the editor background in a dark theme.
    private System.Drawing.Color? DarkEditorBackground()
    {
        if (ActualThemeVariant != ThemeVariant.Dark)
        {
            return null;
        }

        return this.TryFindResource(ThemeApplier.EditorBackgroundKey, ActualThemeVariant, out object? resource) &&
               resource is ISolidColorBrush { Color: var color }
            ? System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B)
            : System.Drawing.Color.Black;
    }
}

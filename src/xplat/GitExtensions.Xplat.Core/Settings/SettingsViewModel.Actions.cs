using CommunityToolkit.Mvvm.Input;
using GitUI.CommandsDialogs.SettingsDialog.RevisionLinks;

namespace GitExtensions.Xplat.Core.Settings;

/// <summary>
///  The path boxes of the settings pages that have a Browse button.
/// </summary>
public enum SettingsPathField
{
    GitCommand,
    LinuxToolsDir,
    OtherHome,
    OtherSsh,
    Plink,
    Puttygen,
    Pageant,
    CommitTemplate,
    MergeToolPath,
    DiffToolPath,
}

// The settings pages' buttons and menu items that open a picker, a folder, a page or a window (MVVM.md): the view model
// decides what they apply to and where the answer goes; the window supplies the pickers and the system's programs through
// SettingsViewModelServices. A failure shows in the window's error line, as before.
public sealed partial class SettingsViewModel
{
    // Upstream's titles of the pickers.
    public const string GitCommandTitle = "Command used to run git";
    public const string LinuxToolsTitle = "Path to linux tools";
    public const string HomeTitle = "HOME";
    public const string OtherSshTitle = "Other ssh client";
    public const string PlinkTitle = "Path to plink";
    public const string PuttygenTitle = "Path to puttygen";
    public const string PageantTitle = "Path to pageant";
    public const string CommitTemplateTitle = "Path to commit template";
    public const string ScriptIconTitle = "Icon or associated file path";

    // Upstream's user manual folder of the settings pages.
    private const string ManualSubfolder = "settings";

    /// <summary>
    ///  Upstream's "User folder" item: off for a portable app, which has no user themes folder.
    /// </summary>
    public bool CanOpenUserThemesFolder => _services.UserThemesFolder is not null;

    // Upstream's Browse buttons of the Git, SSH and Config pages: the path chosen goes into the box; a tool's path also
    // suggests the tool's command (btnDiffToolBrowse_Click, btnMergeToolBrowse_Click).
    [RelayCommand]
    private Task BrowsePathAsync(SettingsPathField field) => ShowErrorOfAsync(async () =>
    {
        switch (field)
        {
            case SettingsPathField.LinuxToolsDir:
                LinuxToolsDir = await _services.PickFolder(LinuxToolsTitle) ?? LinuxToolsDir;
                return;
            case SettingsPathField.OtherHome:
                OtherHome = await _services.PickFolder(HomeTitle) ?? OtherHome;
                return;
            case SettingsPathField.MergeToolPath or SettingsPathField.DiffToolPath:
                bool diff = field == SettingsPathField.DiffToolPath;
                if (await _services.PickFile(diff ? GitConfigPage.DiffTool ?? "" : GitConfigPage.MergeTool ?? "", false) is
                    { } tool)
                {
                    if (diff)
                    {
                        GitConfigPage.DiffToolPath = tool;
                    }
                    else
                    {
                        GitConfigPage.MergeToolPath = tool;
                    }

                    GitConfigPage.SuggestToolCommand(diff);
                }

                return;
        }

        string title = field switch
        {
            SettingsPathField.GitCommand => GitCommandTitle,
            SettingsPathField.OtherSsh => OtherSshTitle,
            SettingsPathField.Plink => PlinkTitle,
            SettingsPathField.Puttygen => PuttygenTitle,
            SettingsPathField.Pageant => PageantTitle,
            _ => CommitTemplateTitle,
        };
        if (await _services.PickFile(title, false) is not { } path)
        {
            return;
        }

        switch (field)
        {
            case SettingsPathField.GitCommand:
                GitCommand = path;
                break;
            case SettingsPathField.OtherSsh:
                OtherSsh = path;
                break;
            case SettingsPathField.Plink:
                Plink = path;
                break;
            case SettingsPathField.Puttygen:
                Puttygen = path;
                break;
            case SettingsPathField.Pageant:
                Pageant = path;
                break;
            default:
                GitConfigPage.CommitTemplate = path;
                break;
        }
    });

    // Upstream's SortingSettingsPage help pictures: a section of the user manual.
    [RelayCommand]
    private void OpenManual(string anchor) => _services.OpenUrl(_services.ManualUrl(ManualSubfolder, anchor));

    // Upstream's ShowAppThemesDirectory and ShowUserThemesDirectory; the user folder is made first, as upstream makes it.
    [RelayCommand]
    private void OpenThemesFolder(bool user)
    {
        if (!user)
        {
            if (_services.AppThemesFolder is { } application)
            {
                _services.OpenFolder(application);
            }

            return;
        }

        if (_services.UserThemesFolder is { } folder)
        {
            Directory.CreateDirectory(folder);
            _services.OpenFolder(folder);
        }
    }

    // As upstream, the button saves at once, not with OK.
    [RelayCommand]
    private void SaveDiffDefaults() => _preferences.SaveDiffOptionsAsDefault();

    [RelayCommand]
    private Task ClearImageCacheAsync() => ShowErrorOfAsync(_services.ClearAvatarCache);

    // Upstream's ConfigureEncoding_Click.
    [RelayCommand]
    private Task ConfigureEncodingsAsync() => ShowErrorOfAsync(async () =>
    {
        if (await _services.ConfigureEncodings())
        {
            GitConfigPage.RefreshEncodings();
        }
    });

    // Upstream's ScriptsSettingsPage "Browse...": an icon or a file whose icon the script shows.
    [RelayCommand]
    private Task PickScriptIconFileAsync() => ShowErrorOfAsync(async () =>
    {
        if (ScriptsPage.Selected is { } item && await _services.PickFile(ScriptIconTitle, true) is { } path)
        {
            item.IconFilePath = path;
        }
    });

    [RelayCommand]
    private void ShowScriptHelp() => _services.ShowScriptHelp();

    // Upstream's revision link template menu, one button per template.
    [RelayCommand]
    private Task AddRevisionLinkTemplateAsync(ICloudProviderExternalLinkDefinitionExtractor template)
        => ShowErrorOfAsync(() => RevisionLinksPage.AddTemplateCommand.ExecuteAsync(template));

    private async Task ShowErrorOfAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (OperationCanceledException)
        {
            // Nothing to report.
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
        }
    }
}

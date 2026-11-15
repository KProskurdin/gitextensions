using GitCommands.Settings;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Settings;
using GitExtensions.Xplat.Core.BuildServer;
using GitExtensions.Xplat.Core.Settings;
using GitUIPluginInterfaces.BuildServerIntegration;

namespace GitExtensions.Xplat.App;

/// <summary>
///  The Build server integration tab: the new shell's version of upstream's <c>BuildServerIntegrationSettingsPage</c>, on
///  the repository's settings. Its values are <see cref="BuildServerPageViewModel"/>'s; the build server plugin's own
///  settings control, a view the plugin makes, is shown here for the chosen type.
/// </summary>
public partial class SettingsWindow
{
    // Upstream's BuildServerSettings path; the type's own settings are under "BuildServer.<type>".
    private const string BuildServerSettingsPath = "BuildServer";

    private IBuildServerSettingsUserControl? _buildServerControl;
    private string? _buildServerControlType;

    private BuildServerPageViewModel BuildServerPage => ViewModel.BuildServerPage;

    /// <summary>
    ///  The build server plugin's settings control on the tab, or null.
    /// </summary>
    public IBuildServerSettingsUserControl? BuildServerControl => _buildServerControl;

    /// <summary>
    ///  True once the build server types are listed.
    /// </summary>
    public bool IsBuildServerPageLoaded => BuildServerPage.IsLoaded;

    // Upstream's Init and SettingsToPage: the types are read off the UI thread, since the plugins may still be loading.
    private void ShowBuildServerPage()
    {
        BuildServerPage.TypeChosen += (_, _) =>
            UiActions.Run(ShowBuildServerControlAsync, ex => ViewModel.ErrorText = ex.Message);
        UiActions.Run(async () =>
        {
            await BuildServerPage.LoadAsync();
            await ShowBuildServerControlAsync();
        }, ex => ViewModel.ErrorText = ex.Message);
    }

    // As upstream's DistributedSettingsPage.SetCurrentSettings: the page's values go to the level they were edited at, and
    // the page shows the chosen level.
    private void ChangeBuildServerLevel(SettingLevel level)
    {
        if (!BuildServerPage.IsLoaded)
        {
            BuildServerPage.SetLevel(level);
            return;
        }

        SaveBuildServerPage();
        BuildServerPage.ShowLevel(level);
        UiActions.Run(ShowBuildServerControlAsync, ex => ViewModel.ErrorText = ex.Message);
    }

    // Upstream's ActivateBuildServerSettingsControl: the plugin's control for the chosen type, given the repository's name
    // and remotes, and loaded from that type's settings.
    private async Task ShowBuildServerControlAsync()
    {
        _buildServerControl = null;
        _buildServerControlType = null;
        BuildServerSettingsPanel.Content = null;
        if (BuildServerPage.SelectedType?.Name is not { } type || _repositoryPath is not { } repositoryPath)
        {
            return;
        }

        IReadOnlyList<Remote> remotes = await RevisionLinkTemplates.LoadRemotesAsync(repositoryPath);
        if (type != BuildServerPage.SelectedType?.Name
            || AppServices.BuildServers.CreateSettingsControl(type) is not { } control)
        {
            return;
        }

        string defaultProjectName = repositoryPath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries)[^1];
        control.Initialize(defaultProjectName,
            remotes.Select(remote =>
                remote.PushUrls.FirstOrDefault(url => !string.IsNullOrEmpty(url)) ?? remote.FetchUrl));
        control.LoadSettings(SettingsOf(type));
        _buildServerControl = control;
        _buildServerControlType = type;
        BuildServerSettingsPanel.Content = control;
    }

    // The page's values, then the plugin control's settings of its type, at the level shown.
    private void SaveBuildServerPage()
    {
        if (BuildServerPage.Save() && _buildServerControl is not null && _buildServerControlType is not null)
        {
            _buildServerControl.SaveSettings(SettingsOf(_buildServerControlType));
        }
    }

    private SettingsSource SettingsOf(string type)
        => new SettingsPath(BuildServerPage.Settings, new SettingsPath(null, BuildServerSettingsPath).PathFor(type));
}

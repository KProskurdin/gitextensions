using Avalonia.Controls;
using GitCommands.Settings;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Settings;
using GitExtensions.Xplat.Core.BuildServer;
using GitExtensions.Xplat.Core.Settings;
using GitUIPluginInterfaces.BuildServerIntegration;

namespace GitExtensions.Xplat.App;

/// <summary>
///  The Build server integration tab: the new shell's version of upstream's <c>BuildServerIntegrationSettingsPage</c>, on
///  the repository's settings, with the build server plugin's own settings control.
/// </summary>
public partial class SettingsWindow
{
    // Upstream's _noneItem.
    private const string NoBuildServer = "None";

    // Upstream's BuildServerSettings path; the type's own settings are under "BuildServer.<type>".
    private const string BuildServerSettingsPath = "BuildServer";

    private IBuildServerSettingsUserControl? _buildServerControl;
    private string? _buildServerControlType;
    private bool _buildServerPageShown;

    /// <summary>
    ///  The build server plugin's settings control on the tab, or null.
    /// </summary>
    public IBuildServerSettingsUserControl? BuildServerControl => _buildServerControl;

    /// <summary>
    ///  True once the build server types are listed.
    /// </summary>
    public bool IsBuildServerPageLoaded => _buildServerPageShown;

    // Upstream's Init and SettingsToPage: the types are read off the UI thread, since the plugins may still be loading.
    private void ShowBuildServerPage()
    {
        EnableBuildServerCheck.IsEnabled = BuildServerTypeBox.IsEnabled = false;
        BuildServerNoRepositoryText.IsVisible = _repositoryPath is null;
        UiActions.Run(ShowBuildServerTypesAsync, ex => ErrorText.Text = ex.Message);
    }

    private async Task ShowBuildServerTypesAsync()
    {
        IReadOnlyList<BuildServerType> types = await Task.Run(() => AppServices.BuildServers.Types);
        SettingsSource settings = _revisionLinkSettings;
        string? current = BuildServerSettings.ServerName[settings];
        List<ComboBoxItem> items = [new() { Content = NoBuildServer }];
        items.AddRange(types.Select(type => new ComboBoxItem { Content = type.DisplayName, Tag = type.Name }));
        BuildServerTypeBox.ItemsSource = items;
        BuildServerTypeBox.SelectedItem = items.FirstOrDefault(item => Equals(item.Tag, current)) ?? items[0];
        EnableBuildServerCheck.IsChecked = BuildServerSettings.IntegrationEnabled[settings];
        EnableBuildServerCheck.IsEnabled = BuildServerTypeBox.IsEnabled = true;
        BuildServerTypeBox.SelectionChanged += (_, _) => UiActions.Run(ShowBuildServerControlAsync, ex => ErrorText.Text = ex.Message);
        await ShowBuildServerControlAsync();
        _buildServerPageShown = true;
    }

    private string? SelectedBuildServerType => (BuildServerTypeBox.SelectedItem as ComboBoxItem)?.Tag as string;

    // Upstream's ActivateBuildServerSettingsControl: the plugin's control for the chosen type, given the repository's name
    // and remotes, and loaded from that type's settings.
    private async Task ShowBuildServerControlAsync()
    {
        _buildServerControl = null;
        _buildServerControlType = null;
        BuildServerSettingsPanel.Content = null;
        if (SelectedBuildServerType is not { } type || _repositoryPath is not { } repositoryPath)
        {
            return;
        }

        IReadOnlyList<Remote> remotes = await RevisionLinkTemplates.LoadRemotesAsync(repositoryPath);
        if (type != SelectedBuildServerType || AppServices.BuildServers.CreateSettingsControl(type) is not { } control)
        {
            return;
        }

        string defaultProjectName = repositoryPath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries)[^1];
        control.Initialize(defaultProjectName,
            remotes.Select(remote => remote.PushUrls.FirstOrDefault(url => !string.IsNullOrEmpty(url)) ?? remote.FetchUrl));
        control.LoadSettings(SettingsOf(type));
        _buildServerControl = control;
        _buildServerControlType = type;
        BuildServerSettingsPanel.Content = control;
    }

    // Upstream's PageToSettings: an undecided check box stores nothing, so a lower level or the detection decides.
    private void SaveBuildServerPage()
    {
        if (!_buildServerPageShown)
        {
            return;
        }

        SettingsSource settings = _revisionLinkSettings;
        BuildServerSettings.ServerName[settings] = SelectedBuildServerType;
        BuildServerSettings.IntegrationEnabled[settings] = EnableBuildServerCheck.IsChecked;
        if (_buildServerControl is not null && _buildServerControlType is not null)
        {
            _buildServerControl.SaveSettings(SettingsOf(_buildServerControlType));
        }
    }

    private SettingsSource SettingsOf(string type)
        => new SettingsPath(_revisionLinkSettings, new SettingsPath(null, BuildServerSettingsPath).PathFor(type));
}

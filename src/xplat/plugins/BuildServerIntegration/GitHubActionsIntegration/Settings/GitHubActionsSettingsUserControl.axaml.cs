using System.ComponentModel.Composition;
using GitCommands.Remotes;
using GitExtensions.Extensibility.Settings;
using GitExtensions.Xplat.Ui;
using GitUIPluginInterfaces.BuildServerIntegration;
using UserControl = Avalonia.Controls.UserControl;

namespace GitExtensions.Plugins.GitHubActionsIntegration.Settings;

/// <summary>
///  The new shell's version of upstream's <c>GitHubActionsSettingsUserControl</c>, exported as upstream's control is: the
///  owner and repository come from the first GitHub remote unless the settings have them. The values are upstream's
///  settings, under upstream's keys.
/// </summary>
[Export(typeof(IBuildServerSettingsUserControl))]
[BuildServerSettingsUserControlMetadata(GitHubActionsAdapter.PluginName)]
[PartCreationPolicy(CreationPolicy.NonShared)]
public partial class GitHubActionsSettingsUserControl : UserControl, IBuildServerSettingsUserControl
{
    private const string DefaultApiUrl = "https://api.github.com";

    private readonly GitHubRemoteParser _remoteParser = new();

    public GitHubActionsSettingsUserControl()
    {
        InitializeComponent();
        UpstreamTranslation.Apply(this, nameof(GitHubActionsSettingsUserControl));
    }

    public void Initialize(string defaultProjectName, IEnumerable<string?> remotes)
    {
        foreach (string? remote in remotes)
        {
            if (remote is not null
                && _remoteParser.TryExtractGitHubDataFromRemoteUrl(remote, out string? owner, out string? repository))
            {
                txtOwner.Text = owner;
                txtRepository.Text = repository;
                break;
            }
        }
    }

    public void LoadSettings(SettingsSource buildServerConfig)
    {
        string? apiUrl = buildServerConfig.GetString(GitHubActionsAdapter.SettingApiUrl, null);
        string? owner = buildServerConfig.GetString(GitHubActionsAdapter.SettingOwner, null);
        string? repository = buildServerConfig.GetString(GitHubActionsAdapter.SettingRepository, null);
        txtApiUrl.Text = apiUrl ?? DefaultApiUrl;
        if (!string.IsNullOrWhiteSpace(owner))
        {
            txtOwner.Text = owner;
        }

        if (!string.IsNullOrWhiteSpace(repository))
        {
            txtRepository.Text = repository;
        }

        txtApiToken.Text = buildServerConfig.GetString(GitHubActionsAdapter.SettingApiToken, null);
    }

    // As upstream, the default API URL is not stored.
    public void SaveSettings(SettingsSource buildServerConfig)
    {
        string? apiUrl = (txtApiUrl.Text ?? "").Trim().TrimEnd('/').NullIfEmpty();
        if (string.Equals(apiUrl, DefaultApiUrl, StringComparison.OrdinalIgnoreCase))
        {
            apiUrl = null;
        }

        buildServerConfig.SetString(GitHubActionsAdapter.SettingApiUrl, apiUrl);
        buildServerConfig.SetString(GitHubActionsAdapter.SettingOwner, txtOwner.Text.NullIfEmpty());
        buildServerConfig.SetString(GitHubActionsAdapter.SettingRepository, txtRepository.Text.NullIfEmpty());
        buildServerConfig.SetString(GitHubActionsAdapter.SettingApiToken, txtApiToken.Text.NullIfEmpty());
    }
}

using System.ComponentModel.Composition;
using System.Globalization;
using GitCommands;
using GitExtensions.Extensibility.Settings;
using GitExtensions.Plugins.GitlabIntegration.ApiClient;
using GitExtensions.Plugins.GitlabIntegration.ApiClient.Models;
using GitExtensions.Xplat.Ui;
using GitUIPluginInterfaces.BuildServerIntegration;
using UserControl = Avalonia.Controls.UserControl;

namespace GitExtensions.Plugins.GitlabIntegration.Settings;

/// <summary>
///  The new shell's version of upstream's <c>GitlabSettingsUserControl</c>, exported as upstream's control is: the instance
///  comes from the first GitLab remote unless the settings have one, and the project ID can be looked up on the server
///  with upstream's API client. The values are upstream's settings, under upstream's keys.
/// </summary>
[Export(typeof(IBuildServerSettingsUserControl))]
[BuildServerSettingsUserControlMetadata(GitlabAdapter.PluginName)]
[PartCreationPolicy(CreationPolicy.NonShared)]
public partial class GitlabSettingsUserControl : UserControl, IBuildServerSettingsUserControl
{
    private const string TokenManagementUrlTail = "/-/profile/personal_access_tokens?name=GitExtensionsIntegration&scopes=api";

    private readonly GitlabRemoteParser _remoteParser = new();
    private IEnumerable<string?> _remotes = [];
    private string? _repositoryNamespace;
    private string? _repositoryName;

    public GitlabSettingsUserControl()
    {
        InitializeComponent();
        UpstreamTranslation.Apply(this, nameof(GitlabSettingsUserControl));
        InstanceUrlTextBox.TextChanged += (_, _) =>
            TokenManagementLink.IsEnabled = GetProjectIdLink.IsEnabled = IsInstanceUrlValid();
        TokenManagementLink.Click += (_, _) => OsShellUtil.OpenUrlInDefaultBrowser($"{InstanceUrlTextBox.Text}{TokenManagementUrlTail}");
        GetProjectIdLink.Click += (_, _) => _ = GetProjectIdAsync();
    }

    public void Initialize(string defaultProjectName, IEnumerable<string?> remotes)
    {
        _remotes = remotes;
    }

    public void LoadSettings(SettingsSource buildServerConfig)
    {
        string? host = buildServerConfig.GetValue("InstanceUrl");
        int? projectId = buildServerConfig.GetInt("ProjectId");
        string? apiToken = buildServerConfig.GetValue("ApiToken");
        foreach (string? remote in _remotes)
        {
            if (remote is not null
                && _remoteParser.TryExtractGitlabDataFromRemoteUrl(remote, out string? remoteHost, out string? owner, out string? repository))
            {
                if (string.IsNullOrWhiteSpace(host))
                {
                    host = $"https://{remoteHost}";
                }

                _repositoryNamespace = owner;
                _repositoryName = repository;
                break;
            }
        }

        InstanceUrlTextBox.Text = host;
        ApiTokenTextBox.Text = apiToken;
        if (projectId is not null)
        {
            ProjectIdTextBox.Text = projectId.Value.ToString(CultureInfo.InvariantCulture);
        }
        else if (host is not null)
        {
            // As upstream, a project without an ID is looked up at once; a failure leaves the box empty.
            _ = FillProjectIdAsync(host, apiToken);
        }
    }

    public void SaveSettings(SettingsSource buildServerConfig)
    {
        buildServerConfig.SetString("InstanceUrl", InstanceUrlTextBox.Text.NullIfEmpty());
        if (int.TryParse(ProjectIdTextBox.Text, out int projectId))
        {
            buildServerConfig.SetInt("ProjectId", projectId);
        }

        buildServerConfig.SetString("ApiToken", ApiTokenTextBox.Text.NullIfEmpty());
        buildServerConfig.SetInt("PagesLimit", 0);
    }

    private bool IsInstanceUrlValid() => Uri.IsWellFormedUriString(InstanceUrlTextBox.Text, UriKind.Absolute);

    // Upstream's GetProjectIdLink_LinkClicked: says so when the server does not know the project.
    private async Task GetProjectIdAsync()
    {
        if (!IsInstanceUrlValid())
        {
            return;
        }

        GetProjectIdStatusText.IsVisible = false;
        GetProjectIdStatusText.IsVisible = !await FillProjectIdAsync(InstanceUrlTextBox.Text!, ApiTokenTextBox.Text);
    }

    private async Task<bool> FillProjectIdAsync(string host, string? apiToken)
    {
        int projectId = await FindProjectIdAsync(host, apiToken);
        if (projectId > 0)
        {
            ProjectIdTextBox.Text = projectId.ToString(CultureInfo.InvariantCulture);
        }

        return projectId > 0;
    }

    // Upstream's UpdateProjectIdAsync: 0 when the remote names no project or the server cannot be asked.
    private async Task<int> FindProjectIdAsync(string host, string? apiToken)
    {
        if (string.IsNullOrWhiteSpace(_repositoryNamespace) || string.IsNullOrWhiteSpace(_repositoryName))
        {
            return 0;
        }

        try
        {
            GitlabApiClient apiClient = new(host, apiToken ?? string.Empty);
            GitlabProject? project = await apiClient.GetProjectAsync(_repositoryNamespace, _repositoryName);
            return project?.Id ?? 0;
        }
        catch (Exception)
        {
            return 0;
        }
    }
}

using System.ComponentModel.Composition;
using Avalonia.Input.Platform;
using GitCommands;
using GitExtensions.Extensibility.Settings;
using GitExtensions.Xplat.Ui;
using GitUIPluginInterfaces.BuildServerIntegration;
using TopLevel = Avalonia.Controls.TopLevel;
using UserControl = Avalonia.Controls.UserControl;

namespace AzureDevOpsIntegration.Settings;

/// <summary>
///  The new shell's version of upstream's Azure DevOps <c>SettingsUserControl</c>, exported as upstream's control is: the
///  project comes from the first Azure DevOps remote unless the settings have one, and a build URL on the clipboard fills
///  the project and the build definition. The values are upstream's <see cref="IntegrationSettings"/>, under upstream's keys.
/// </summary>
[Export(typeof(IBuildServerSettingsUserControl))]
[BuildServerSettingsUserControlMetadata(AzureDevOpsAdapter.PluginName)]
[PartCreationPolicy(CreationPolicy.NonShared)]
public partial class SettingsUserControl : UserControl, IBuildServerSettingsUserControl
{
    private const string Category = nameof(SettingsUserControl);

    private IEnumerable<string?> _remotes = [];
    private bool _isUpdating;
    private IntegrationSettings _currentSettings = new();

    public SettingsUserControl()
    {
        InitializeComponent();
        UpstreamTranslation.Apply(this, Category);
        TfsServer.TextChanged += (_, _) => OnTextChanged();
        TfsBuildDefinitionNameFilter.TextChanged += (_, _) => OnTextChanged();
        RestApiToken.TextChanged += (_, _) => OnTextChanged();
        TokenManagementLink.Click += (_, _) => OsShellUtil.OpenUrlInDefaultBrowser(TokenManagementUrl);
        ExtractLink.Click += (_, _) => _ = ExtractFromClipboardAsync();
        UpdateView();
    }

    private static string FailToExtractDataFromClipboardMessage => UpstreamTranslation.Text(Category,
        "_failToExtractDataFromClipboardMessage",
        $"The clipboard doesn't contain a valid build url.{Environment.NewLine}{Environment.NewLine}Please copy the url of the build into the clipboard before retrying.{Environment.NewLine}(Should contain at least the \"buildId\" parameter)");

    private static string FailToLoadBuildDefinitionInfoMessage => UpstreamTranslation.Text(Category,
        "_failToLoadBuildDefinitionInfoMessage",
        $"Error while trying to retrieve build definition information from url.{Environment.NewLine}{Environment.NewLine}Please ensure that the url is valid and that the API token has access to build and project information.");

    private static string InfoNoApiTokenMessage => UpstreamTranslation.Text(Category, "_infoNoApiTokenMessage",
        "Unable to retrieve build definition information without API token. Field will be left blank.");

    private static string FailToExtractDataFromClipboardCaption => UpstreamTranslation.Text(Category,
        "_failToExtractDataFromClipboardCaption", "Could not extract data");

    private string? TokenManagementUrl
        => ProjectUrlHelper.TryGetTokenManagementUrlFromProject(_currentSettings.ProjectUrl).tokenManagementUrl;

    public void Initialize(string defaultProjectName, IEnumerable<string?> remotes)
    {
        _remotes = remotes;
    }

    public void LoadSettings(SettingsSource buildServerConfig)
    {
        IntegrationSettings settings = IntegrationSettings.ReadFrom(buildServerConfig);
        if (string.IsNullOrWhiteSpace(settings.ProjectUrl))
        {
            (bool found, string? projectUrl) = ProjectUrlHelper.TryDetectProjectFromRemoteUrls(_remotes);
            if (found && projectUrl is not null)
            {
                settings.ProjectUrl = projectUrl;
            }
        }

        _currentSettings = settings;
        UpdateView();
    }

    // As upstream, settings without a project or with an invalid filter are not saved.
    public void SaveSettings(SettingsSource buildServerConfig)
    {
        if (_currentSettings.IsValid())
        {
            _currentSettings.WriteTo(buildServerConfig);
        }
    }

    private void OnTextChanged()
    {
        if (_isUpdating)
        {
            return;
        }

        _currentSettings.ProjectUrl = TfsServer.Text ?? "";
        _currentSettings.BuildDefinitionFilter = TfsBuildDefinitionNameFilter.Text ?? "";
        _currentSettings.ApiToken = RestApiToken.Text ?? "";
        UpdateView();
    }

    private void UpdateView()
    {
        _isUpdating = true;
        try
        {
            TfsServer.Text = _currentSettings.ProjectUrl;
            TfsBuildDefinitionNameFilter.Text = _currentSettings.BuildDefinitionFilter;
            labelRegexError.IsVisible = !BuildServerSettingsHelper.IsRegexValid(_currentSettings.BuildDefinitionFilter);
            RestApiToken.Text = _currentSettings.ApiToken;
            TokenManagementLink.IsEnabled = BuildServerSettingsHelper.IsUrlValid(TokenManagementUrl);
        }
        finally
        {
            _isUpdating = false;
        }
    }

    // Upstream's ExtractLink_LinkClicked: the build definition's name needs the API token; without one it stays empty.
    private async Task ExtractFromClipboardAsync()
    {
        IClipboard? clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        string buildUrl = clipboard is null ? "" : await clipboard.TryGetTextAsync() ?? "";
        WindowOwner? owner = TopLevel.GetTopLevel(this) is Avalonia.Controls.Window window ? new WindowOwner(window) : null;
        (bool success, string? projectUrl, int buildId) = ProjectUrlHelper.TryParseBuildUrl(buildUrl);
        if (!success || projectUrl is null)
        {
            GitExtensions.Extensibility.MessageBoxes.ShowError(owner, FailToExtractDataFromClipboardMessage,
                FailToExtractDataFromClipboardCaption);
            return;
        }

        string buildDefinitionName = "";
        if (!string.IsNullOrWhiteSpace(_currentSettings.ApiToken))
        {
            try
            {
                using ApiClient apiClient = new(projectUrl, _currentSettings.ApiToken);
                buildDefinitionName = await apiClient.GetBuildDefinitionNameFromIdAsync(buildId) ?? "";
            }
            catch (Exception)
            {
                GitExtensions.Extensibility.MessageBoxes.ShowError(owner, FailToLoadBuildDefinitionInfoMessage,
                    FailToExtractDataFromClipboardCaption);
                return;
            }
        }
        else
        {
            GitExtensions.Extensibility.MessageBoxes.Show(owner, InfoNoApiTokenMessage, FailToExtractDataFromClipboardCaption,
                System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
        }

        _currentSettings.ProjectUrl = projectUrl;
        _currentSettings.BuildDefinitionFilter = buildDefinitionName;
        UpdateView();
    }
}

using System.ComponentModel.Composition;
using System.Text.RegularExpressions;
using Avalonia.Input.Platform;
using GitExtensions.Extensibility.Settings;
using GitExtensions.Xplat.Ui;
using GitUIPluginInterfaces.BuildServerIntegration;
using TopLevel = Avalonia.Controls.TopLevel;
using UserControl = Avalonia.Controls.UserControl;
using Window = Avalonia.Controls.Window;

namespace TeamCityIntegration.Settings;

/// <summary>
///  The new shell's version of upstream's <c>TeamCitySettingsUserControl</c>, exported as upstream's control is: the
///  project and build can be chosen from the server's tree (<see cref="TeamCityBuildChooser"/>) or read from a build URL on
///  the clipboard, with upstream's adapter. The values are upstream's settings, under upstream's keys.
/// </summary>
[Export(typeof(IBuildServerSettingsUserControl))]
[BuildServerSettingsUserControlMetadata(TeamCityAdapter.PluginName)]
[PartCreationPolicy(CreationPolicy.NonShared)]
public partial class TeamCitySettingsUserControl : UserControl, IBuildServerSettingsUserControl
{
    private const string Category = nameof(TeamCitySettingsUserControl);

    private readonly TeamCityAdapter _teamCityAdapter = new();
    private string? _defaultProjectName;

    public TeamCitySettingsUserControl()
    {
        InitializeComponent();
        UpstreamTranslation.Apply(this, Category);
        TeamCityServerUrl.TextChanged += (_, _) => SetChooseBuildButtonState();
        TeamCityBuildIdFilter.TextChanged += (_, _) =>
            labelRegexError.IsVisible = !BuildServerSettingsHelper.IsRegexValid(TeamCityBuildIdFilter.Text ?? "");
        buttonProjectChooser.Click += (_, _) => ChooseBuild();
        lnkExtractDataFromBuildUrlCopiedInTheClipboard.Click += (_, _) => _ = ExtractFromClipboardAsync();
    }

    [GeneratedRegex(@"(\?|\&)(?<buildtypeid>[^=]+)\=(?<buildtype>[^&]+)", RegexOptions.ExplicitCapture)]
    private static partial Regex TeamcityBuildUrl { get; }

    private static string FailToLoadProjectMessage => UpstreamTranslation.Text(Category, "_failToLoadProjectMessage",
        $"Failed to load the projects and build list.{Environment.NewLine}Please verify the server url.");

    private static string FailToLoadProjectCaption => UpstreamTranslation.Text(Category, "_failToLoadProjectCaption",
        "Error when loading the projects and build list");

    private static string FailToExtractDataFromClipboardMessage => UpstreamTranslation.Text(Category,
        "_failToExtractDataFromClipboardMessage",
        $"The clipboard doesn't contain a valid build url.{Environment.NewLine}{Environment.NewLine}Please copy in the clipboard the url of the build before retrying.{Environment.NewLine}(Should contain at least the \"buildTypeId\" parameter)");

    private static string FailToExtractDataFromClipboardCaption => UpstreamTranslation.Text(Category,
        "_failToExtractDataFromClipboardCaption", "Build url not valid");

    private WindowOwner? Owner => TopLevel.GetTopLevel(this) is Window window ? new WindowOwner(window) : null;

    public void Initialize(string defaultProjectName, IEnumerable<string?> remotes)
    {
        _defaultProjectName = defaultProjectName;
        SetChooseBuildButtonState();
    }

    public void LoadSettings(SettingsSource buildServerConfig)
    {
        TeamCityServerUrl.Text = buildServerConfig.GetString("BuildServerUrl", null);
        TeamCityProjectName.Text = buildServerConfig.GetString("ProjectName", _defaultProjectName);
        TeamCityBuildIdFilter.Text = buildServerConfig.GetString("BuildIdFilter", null);
        CheckBoxLogAsGuest.IsChecked = buildServerConfig.GetBool("LogAsGuest", false);
    }

    // As upstream, nothing is saved while the filter is not a valid regular expression, and an empty value or an undecided
    // check box sets nothing, so a lower settings level still applies.
    public void SaveSettings(SettingsSource buildServerConfig)
    {
        if (!BuildServerSettingsHelper.IsRegexValid(TeamCityBuildIdFilter.Text ?? ""))
        {
            return;
        }

        buildServerConfig.SetString("BuildServerUrl", TeamCityServerUrl.Text.NullIfEmpty());
        buildServerConfig.SetString("ProjectName", TeamCityProjectName.Text.NullIfEmpty());
        buildServerConfig.SetString("BuildIdFilter", TeamCityBuildIdFilter.Text.NullIfEmpty());
        buildServerConfig.SetBool("LogAsGuest", CheckBoxLogAsGuest.IsChecked);
    }

    private void SetChooseBuildButtonState()
        => buttonProjectChooser.IsEnabled = !string.IsNullOrWhiteSpace(TeamCityServerUrl.Text);

    // Upstream's buttonProjectChooser_Click: a server that cannot be read shows upstream's error.
    private void ChooseBuild()
    {
        try
        {
            TeamCityBuildChooser chooser = new(TeamCityServerUrl.Text ?? "", TeamCityProjectName.Text ?? "",
                TeamCityBuildIdFilter.Text ?? "");
            ModalWindow.Show(() => chooser, Owner);
            if (chooser.DialogResult == System.Windows.Forms.DialogResult.OK)
            {
                TeamCityProjectName.Text = chooser.TeamCityProjectName;
                TeamCityBuildIdFilter.Text = chooser.TeamCityBuildIdFilter;
            }
        }
        catch
        {
            GitExtensions.Extensibility.MessageBoxes.ShowError(Owner, FailToLoadProjectMessage, FailToLoadProjectCaption);
        }
    }

    // Upstream's lnkExtractDataFromBuildUrlCopiedInTheClipboard_LinkClicked.
    private async Task ExtractFromClipboardAsync()
    {
        IClipboard? clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        string text = clipboard is null ? "" : await clipboard.TryGetTextAsync() ?? "";
        if (text.Contains("buildTypeId=") && Uri.TryCreate(text, UriKind.Absolute, out Uri? buildUri))
        {
            string teamCityServerUrl = buildUri.Scheme + "://" + buildUri.Authority;
            TeamCityServerUrl.Text = teamCityServerUrl;
            _teamCityAdapter.InitializeHttpClient(teamCityServerUrl);
            foreach (Match paramResult in TeamcityBuildUrl.Matches(buildUri.Query))
            {
                if (paramResult.Success && paramResult.Groups["buildtypeid"].ValueSpan is "buildTypeId")
                {
                    Build buildType = _teamCityAdapter.GetBuildType(paramResult.Groups["buildtype"].Value);
                    TeamCityProjectName.Text = buildType.ParentProject;
                    TeamCityBuildIdFilter.Text = buildType.Id;
                    return;
                }
            }
        }

        GitExtensions.Extensibility.MessageBoxes.Show(Owner, FailToExtractDataFromClipboardMessage,
            FailToExtractDataFromClipboardCaption, System.Windows.Forms.MessageBoxButtons.OK,
            System.Windows.Forms.MessageBoxIcon.Warning);
    }
}

using System.ComponentModel.Composition;
using GitExtensions.Extensibility.Settings;
using GitExtensions.Xplat.Ui;
using GitUIPluginInterfaces.BuildServerIntegration;
using UserControl = Avalonia.Controls.UserControl;

namespace JenkinsIntegration.Settings;

/// <summary>
///  The new shell's version of upstream's <c>JenkinsSettingsUserControl</c>, exported as upstream's control is. The values
///  are upstream's settings, under upstream's keys.
/// </summary>
[Export(typeof(IBuildServerSettingsUserControl))]
[BuildServerSettingsUserControlMetadata(JenkinsAdapter.PluginName)]
[PartCreationPolicy(CreationPolicy.NonShared)]
public partial class JenkinsSettingsUserControl : UserControl, IBuildServerSettingsUserControl
{
    private string? _defaultProjectName;

    public JenkinsSettingsUserControl()
    {
        InitializeComponent();
        UpstreamTranslation.Apply(this, nameof(JenkinsSettingsUserControl));
    }

    public void Initialize(string defaultProjectName, IEnumerable<string?> remotes)
    {
        _defaultProjectName = defaultProjectName;
    }

    public void LoadSettings(SettingsSource buildServerConfig)
    {
        JenkinsServerUrl.Text = buildServerConfig.GetString("BuildServerUrl", null);
        JenkinsProjectName.Text = buildServerConfig.GetString("ProjectName", _defaultProjectName);
        IgnoreBuildBranch.Text = buildServerConfig.GetString("IgnoreBuildBranch", null);
    }

    public void SaveSettings(SettingsSource buildServerConfig)
    {
        buildServerConfig.SetString("BuildServerUrl", JenkinsServerUrl.Text.NullIfEmpty());
        buildServerConfig.SetString("ProjectName", JenkinsProjectName.Text.NullIfEmpty());
        buildServerConfig.SetString("IgnoreBuildBranch", IgnoreBuildBranch.Text.NullIfEmpty());
    }
}

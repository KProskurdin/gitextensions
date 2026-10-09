using System.ComponentModel.Composition;
using GitExtensions.Extensibility.Settings;
using GitExtensions.Xplat.Ui;
using GitUIPluginInterfaces.BuildServerIntegration;
using UserControl = Avalonia.Controls.UserControl;

namespace AppVeyorIntegration.Settings;

/// <summary>
///  The new shell's version of upstream's <c>AppVeyorSettingsUserControl</c>, exported as upstream's control is, so the
///  Build server integration settings tab shows it for AppVeyor. The values are upstream's settings, under upstream's keys.
/// </summary>
[Export(typeof(IBuildServerSettingsUserControl))]
[BuildServerSettingsUserControlMetadata(AppVeyorAdapter.PluginName)]
[PartCreationPolicy(CreationPolicy.NonShared)]
public partial class AppVeyorSettingsUserControl : UserControl, IBuildServerSettingsUserControl
{
    private string? _defaultProjectName;

    public AppVeyorSettingsUserControl()
    {
        InitializeComponent();
        UpstreamTranslation.Apply(this, nameof(AppVeyorSettingsUserControl));
    }

    public void Initialize(string defaultProjectName, IEnumerable<string?> remotes)
    {
        _defaultProjectName = defaultProjectName;
    }

    public void LoadSettings(SettingsSource buildServerConfig)
    {
        AppVeyorProjectName.Text = buildServerConfig.GetString("AppVeyorProjectName", _defaultProjectName);
        AppVeyorAccountName.Text = buildServerConfig.GetString("AppVeyorAccountName", null);
        AppVeyorAccountToken.Text = buildServerConfig.GetString("AppVeyorAccountToken", null);
        cbLoadTestResults.IsChecked = buildServerConfig.GetBool("AppVeyorLoadTestsResults");
    }

    // As upstream, an empty value or an undecided check box sets nothing, so a lower settings level still applies.
    public void SaveSettings(SettingsSource buildServerConfig)
    {
        buildServerConfig.SetString("AppVeyorProjectName", AppVeyorProjectName.Text.NullIfEmpty());
        buildServerConfig.SetString("AppVeyorAccountName", AppVeyorAccountName.Text.NullIfEmpty());
        buildServerConfig.SetString("AppVeyorAccountToken", AppVeyorAccountToken.Text.NullIfEmpty());
        buildServerConfig.SetBool("AppVeyorLoadTestsResults", cbLoadTestResults.IsChecked);
    }
}

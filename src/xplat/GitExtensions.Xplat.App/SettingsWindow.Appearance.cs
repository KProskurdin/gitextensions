using GitCommands;
using GitExtensions.Xplat.Core.Avatars;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream Appearance page's General settings that are not elsewhere, and its "Author images" group.
/// </summary>
public partial class SettingsWindow
{
    private void WireAppearancePage()
    {
        ClearImageCacheButton.Click += (_, _) => _ = AppServices.Avatars.ClearCacheAsync();
    }
}

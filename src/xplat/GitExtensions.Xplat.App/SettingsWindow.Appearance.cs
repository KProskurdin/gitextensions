using GitCommands;
using GitExtensions.Xplat.Core.Avatars;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream Appearance page's General settings that are not elsewhere, and its "Author images" group.
/// </summary>
public partial class SettingsWindow
{
    private void LoadAppearanceSettings()
    {
        ShowRepoBranchCheck.IsChecked = _preferences.ShowRepoCurrentBranch;
        TruncatePathBox.ItemsSource = TruncatePathOption.All;
        TruncatePathBox.SelectedItem = TruncatePathOption.All.FirstOrDefault(option =>
            option.Method == _preferences.TruncatePathMethod) ?? TruncatePathOption.All[0];
        AvatarOptions avatars = _preferences.Avatars;
        ShowAvatarColumnCheck.IsChecked = _preferences.ShowAuthorAvatarColumn;
        ShowAvatarInCommitInfoCheck.IsChecked = _preferences.ShowAuthorAvatarInCommitInfo;
        AvatarProviderBox.ItemsSource = AvatarProviderOption.All;
        AvatarProviderBox.SelectedItem = AvatarProviderOption.All.First(option => option.Provider == avatars.Provider);
        AvatarFallbackBox.ItemsSource = AvatarFallbackOption.All;
        AvatarFallbackBox.SelectedItem = AvatarFallbackOption.All.First(option => option.Fallback == avatars.Fallback);
        CustomAvatarTemplateBox.Text = avatars.CustomTemplate;
        AvatarCacheDaysBox.Value = avatars.CacheDays;

        // As upstream: the template is used by the custom provider only.
        CustomAvatarTemplateBox.IsEnabled = avatars.Provider == AvatarProvider.Custom;
        AvatarProviderBox.SelectionChanged += (_, _) => CustomAvatarTemplateBox.IsEnabled =
            AvatarProviderBox.SelectedItem is AvatarProviderOption { Provider: AvatarProvider.Custom };
        ClearImageCacheButton.Click += (_, _) => _ = AppServices.Avatars.ClearCacheAsync();
    }

    private void SaveAppearanceSettings()
    {
        _preferences.ShowRepoCurrentBranch = ShowRepoBranchCheck.IsChecked == true;
        if (TruncatePathBox.SelectedItem is TruncatePathOption truncate)
        {
            _preferences.TruncatePathMethod = truncate.Method;
        }

        AvatarOptions avatars = _preferences.Avatars;
        _preferences.ShowAuthorAvatarColumn = ShowAvatarColumnCheck.IsChecked == true;
        _preferences.ShowAuthorAvatarInCommitInfo = ShowAvatarInCommitInfoCheck.IsChecked == true;
        _preferences.Avatars = new AvatarOptions(
            (AvatarProviderBox.SelectedItem as AvatarProviderOption)?.Provider ?? avatars.Provider,
            (AvatarFallbackBox.SelectedItem as AvatarFallbackOption)?.Fallback ?? avatars.Fallback,
            CustomAvatarTemplateBox.Text ?? "",
            (int)(AvatarCacheDaysBox.Value ?? avatars.CacheDays));
    }
}

/// <summary>
///  One of upstream's avatar providers, named as upstream's list names them.
/// </summary>
public sealed record AvatarProviderOption(AvatarProvider Provider)
{
    public static IReadOnlyList<AvatarProviderOption> All { get; } =
        [.. Enum.GetValues<AvatarProvider>().Select(provider => new AvatarProviderOption(provider))];

    public override string ToString() => Provider.ToString();
}

/// <summary>
///  One of upstream's fallback avatar styles, named by upstream's descriptions.
/// </summary>
public sealed record AvatarFallbackOption(AvatarFallbackType Fallback)
{
    public static IReadOnlyList<AvatarFallbackOption> All { get; } =
        [.. Enum.GetValues<AvatarFallbackType>().Select(fallback => new AvatarFallbackOption(fallback))];

    // Upstream lists the enum by its Description attribute ("Author initials"), otherwise by name.
    public override string ToString()
        => typeof(AvatarFallbackType).GetField(Fallback.ToString())?
               .GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), inherit: false)
               .OfType<System.ComponentModel.DescriptionAttribute>().FirstOrDefault()?.Description
           ?? Fallback.ToString();
}

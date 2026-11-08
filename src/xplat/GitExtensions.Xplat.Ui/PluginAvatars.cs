namespace GitExtensions.Xplat.Ui;

/// <summary>
///  The app's avatar service for plugins, which cannot reference the app: upstream's plugins reach
///  <c>AvatarService.DefaultProvider</c> in GitUI.
/// </summary>
public static class PluginAvatars
{
    /// <summary>
    ///  The author's avatar (email, name, size in pixels) as an image file, or null when the avatar settings find none.
    /// </summary>
    public static Func<string, string?, int, Task<byte[]?>>? GetImage { get; set; }
}

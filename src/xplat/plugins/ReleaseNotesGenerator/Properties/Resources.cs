namespace GitExtensions.Plugins.ReleaseNotesGenerator.Properties;

/// <summary>
///  Stands in for upstream's generated resources: its bitmap cannot be created off Windows. The new shell reads the icon from
///  the embedded PNG instead (see Directory.Build.targets).
/// </summary>
internal static class Resources
{
    internal static System.Drawing.Bitmap? IconReleaseNotesGenerator => null;
}

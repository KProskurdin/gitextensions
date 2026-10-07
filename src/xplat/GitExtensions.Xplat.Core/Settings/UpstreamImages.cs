namespace GitUI.Properties;

/// <summary>
///  Stands in for upstream's generated WinForms images, for the upstream files the core links (the revision link
///  templates). A bitmap cannot be created off Windows, and the new shell does not show these icons.
/// </summary>
internal static class Images
{
    // Never read by the new shell; upstream's property type is not nullable.
    internal static Image GitHub => null!;

    internal static Image VisualStudioTeamServices => null!;
}

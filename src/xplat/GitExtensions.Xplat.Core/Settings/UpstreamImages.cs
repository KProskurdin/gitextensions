namespace GitUI.Properties;

/// <summary>
///  Stands in for upstream's generated WinForms images, for the upstream files the core links (the revision link
///  templates, the diff groups' icon names). A bitmap cannot be created off Windows, and the new shell does not show these
///  bitmaps.
/// </summary>
internal static class Images
{
    // Never read by the new shell; upstream's property type is not nullable.
    internal static Image GitHub => null!;

    internal static Image VisualStudioTeamServices => null!;

    // Named (nameof) as icons by upstream's FileStatusDiffCalculator and FileStatusWithDescription.
    internal static Image Diff => null!;

    internal static Image DiffA => null!;

    internal static Image DiffB => null!;

    internal static Image DiffC => null!;

    internal static Image DiffR => null!;
}

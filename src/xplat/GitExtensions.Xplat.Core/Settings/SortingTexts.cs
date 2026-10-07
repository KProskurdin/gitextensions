namespace GitExtensions.Xplat.Core.Settings;

/// <summary>
///  The help texts of upstream's <c>SortingSettingsPage</c>, which it shows as tooltips.
/// </summary>
public static class SortingTexts
{
    public const string RevisionSortWarning = "Sorting revisions may delay rendering of the revision graph.";

    public const string PrioritizedBranchNames = "Regex to prioritize branch names in the left panel and commit info.\n" +
                                                 "The branches matching the pattern will be shown before the others.\n" +
                                                 "Separate the priorities with ';'.";

    public const string PrioritizedRemoteNames = "Regex to prioritize remote names in the left panel and commit info.\n" +
                                                 "The remotes matching the pattern will be shown before the others.\n" +
                                                 "Separate the priorities with ';'.";
}

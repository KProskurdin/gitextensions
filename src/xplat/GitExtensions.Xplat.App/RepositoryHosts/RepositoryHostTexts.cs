using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App.RepositoryHosts;

/// <summary>
///  The texts of upstream's GitUI <c>TranslatedStrings</c> and <c>FormBrowse</c> that the repository host windows show, in
///  the chosen language.
/// </summary>
internal static class RepositoryHostTexts
{
    private const string TranslatedStrings = "TranslatedStrings";
    private const string FormBrowse = "FormBrowse";

    public static string Error => UpstreamTranslation.Text(TranslatedStrings, "_error", "Error");

    public static string Yes => UpstreamTranslation.Text(TranslatedStrings, "_yes", "Yes");

    public static string No => UpstreamTranslation.Text(TranslatedStrings, "_no", "No");

    public static string RemoteInError => UpstreamTranslation.Text(TranslatedStrings, "_remoteInError", "{0}\n\nRemote: {1}");

    public static string ForkCloneRepo => UpstreamTranslation.Text(TranslatedStrings, "_forkCloneRepo", "Fork or clone a repository");

    public static string ViewPullRequest => UpstreamTranslation.Text(TranslatedStrings, "_viewPullRequest", "View pull requests");

    public static string CreatePullRequest => UpstreamTranslation.Text(TranslatedStrings, "_createPullRequest", "Create pull request");

    public static string AddUpstreamRemote => UpstreamTranslation.Text(TranslatedStrings, "_addUpstreamRemote", "Add upstream remote");

    public static string NoReposHostPluginLoaded
        => UpstreamTranslation.Text(FormBrowse, "_noReposHostPluginLoaded", "No repository host plugin loaded.");

    public static string NoReposHostFound => UpstreamTranslation.Text(FormBrowse, "_noReposHostFound",
        "Could not find any relevant repository hosts for the currently open repository.");
}

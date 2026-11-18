using GitExtensions.Xplat.Core.Presentation;

namespace GitExtensions.Xplat.Core.RepositoryHosts;

/// <summary>
///  A message of a repository host window: its text, caption and whether it reports an error (upstream's
///  <c>MessageBoxIcon.Error</c>, else <c>Information</c>).
/// </summary>
public delegate void ShowRepositoryHostMessage(string text, string caption, bool isError);

/// <summary>
///  The base of the repository host windows' view models (upstream's <c>CreatePullRequestForm</c>,
///  <c>ViewPullRequestsForm</c> and <c>ForkAndCloneForm</c>): their texts, translated by the form's field names, and their
///  messages, which the app shows as upstream's message boxes.
/// </summary>
public abstract class RepositoryHostViewModel(IDialogHost host, string category, ShowRepositoryHostMessage showMessage)
    : DialogViewModel(host)
{
    private const string TranslatedStrings = "TranslatedStrings";

    /// <summary>
    ///  Upstream's GitUI <c>TranslatedStrings._error</c>.
    /// </summary>
    protected string ErrorCaption => Host.Translate(TranslatedStrings, "_error", "Error");

    /// <summary>
    ///  Upstream's GitUI <c>TranslatedStrings._remoteInError</c>: an error and the remote it came from.
    /// </summary>
    protected string RemoteInError => Host.Translate(TranslatedStrings, "_remoteInError", "{0}\n\nRemote: {1}");

    protected string Yes => Host.Translate(TranslatedStrings, "_yes", "Yes");

    protected string No => Host.Translate(TranslatedStrings, "_no", "No");

    /// <summary>
    ///  The form's text <paramref name="name"/> in the chosen language.
    /// </summary>
    protected string Text(string name, string english) => Host.Translate(category, name, english);

    protected void ShowMessage(string text, string caption, bool isError = true) => showMessage(text, caption, isError);

    protected void ShowError(string text) => showMessage(text, ErrorCaption, true);
}

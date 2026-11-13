using GitExtensions.Xplat.Core.Presentation;

namespace GitExtensions.Xplat.Core.Platform;

/// <summary>
///  The view model of upstream's <c>FormContributors</c> (texts in <see cref="Contributors"/>): the developers (team and
///  contributors), translators and designers on three tabs. Upstream builds the form in code, so its texts are
///  <c>TranslationString</c>s, translated here.
/// </summary>
public sealed class ContributorsViewModel : DialogViewModel
{
    /// <param name="team">Upstream's resource list of the team.</param>
    /// <param name="coders">Upstream's resource list of the contributors.</param>
    /// <param name="translators">Upstream's resource list of the translators.</param>
    /// <param name="designers">Upstream's resource list of the designers.</param>
    public ContributorsViewModel(IDialogHost host, string team, string coders, string translators, string designers)
        : base(host)
    {
        Title = host.Translate(Contributors.Caption);
        DevelopersHeader = host.Translate(Contributors.Developers);
        TranslatorsHeader = host.Translate(Contributors.Translators);
        DesignersHeader = host.Translate(Contributors.Designers);
        DevelopersText = Contributors.DevelopersText(team, coders, host.Translate(Contributors.Team),
            host.Translate(Contributors.ContributorsCaption));
        TranslatorsText = Contributors.OneLine(translators);
        DesignersText = Contributors.OneLine(designers);
    }

    public string Title { get; }

    public string DevelopersHeader { get; }

    public string TranslatorsHeader { get; }

    public string DesignersHeader { get; }

    public string DevelopersText { get; }

    public string TranslatorsText { get; }

    public string DesignersText { get; }
}

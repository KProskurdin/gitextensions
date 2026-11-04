using Avalonia.Controls;
using GitExtensions.Xplat.Core.Platform;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormContributors</c>, opened from FormAbout's thanks line: upstream's contributor lists on three tabs.
/// </summary>
public partial class ContributorsWindow : Window
{
    public ContributorsWindow()
    {
        InitializeComponent();

        // Upstream builds the form in code, so its texts are TranslationStrings, not controls.
        Title = UpstreamTranslation.ByText(Contributors.Caption);
        DevelopersTab.Header = UpstreamTranslation.ByText(Contributors.Developers);
        TranslatorsTab.Header = UpstreamTranslation.ByText(Contributors.Translators);
        DesignersTab.Header = UpstreamTranslation.ByText(Contributors.Designers);
        DevelopersText.Text = Contributors.DevelopersText(UpstreamResources.Team, UpstreamResources.Coders,
            UpstreamTranslation.ByText(Contributors.Team),
            UpstreamTranslation.ByText(Contributors.ContributorsCaption));
        TranslatorsText.Text = Contributors.OneLine(UpstreamResources.Translators);
        DesignersText.Text = Contributors.OneLine(UpstreamResources.Designers);
    }
}

using Avalonia.Controls;
using GitExtensions.Xplat.Core.Platform;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormContributors</c>, opened from FormAbout's thanks line, bound to <see cref="ContributorsViewModel"/>:
///  upstream's contributor lists on three tabs.
/// </summary>
public partial class ContributorsWindow : Window
{
    public ContributorsWindow()
    {
        DataContext = new ContributorsViewModel(new WindowDialogHost(this), UpstreamResources.Team, UpstreamResources.Coders,
            UpstreamResources.Translators, UpstreamResources.Designers);
        InitializeComponent();
    }
}

using Avalonia.Controls;
using GitExtensions.Xplat.Core.Platform;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormDonate</c>, bound to <see cref="DonateViewModel"/>: its text, and the donate badge, which opens the
///  donation page.
/// </summary>
public partial class DonateWindow : Window
{
    // Upstream's text and address (kept here for the windows and tests that name them).
    public const string DonateText = DonateViewModel.DonateText;
    public const string DonationUrl = DonateViewModel.DonationUrl;

    public DonateWindow()
        : this(_ => { })
    {
    }

    /// <param name="openUrl">Opens an address in the browser.</param>
    public DonateWindow(Action<string> openUrl)
    {
        DataContext = new DonateViewModel(new WindowDialogHost(this), openUrl);
        InitializeComponent();
        UpstreamTranslation.Apply(this, DonateViewModel.Category);
    }
}

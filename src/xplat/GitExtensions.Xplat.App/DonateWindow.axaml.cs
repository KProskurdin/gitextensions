using Avalonia.Controls;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormDonate</c> (Help > Donate): its text and the badge that opens the donation page.
/// </summary>
public partial class DonateWindow : Window
{
    // Upstream's text and address.
    public const string DonateText =
        "We have a dedicated team of collaborators that spends a lot of time maintaining the app, working on new features and fixing bugs." +
        "You can support the project by making a financial contribution. Donations will be used to cover running costs " +
        "and to get the resources needed to keep the project running. We will also use donations to thank collaborators for their efforts.\r\n\r\n" +
        "Click on the button below to get more information about making a donation.";

    public const string DonationUrl = "https://opencollective.com/gitextensions";

    public DonateWindow()
        : this(_ => { })
    {
    }

    /// <param name="openUrl">Opens an address in the browser.</param>
    public DonateWindow(Action<string> openUrl)
    {
        InitializeComponent();
        UpstreamTranslation.Apply(this, "FormDonate");
        lblText.Text = UpstreamTranslation.Text("FormDonate", "_donateText", DonateText);
        pbxDonate.Click += (_, _) => openUrl(DonationUrl);
    }
}

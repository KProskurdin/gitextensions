using CommunityToolkit.Mvvm.Input;
using GitExtensions.Xplat.Core.Presentation;

namespace GitExtensions.Xplat.Core.Platform;

/// <summary>
///  The view model of upstream's <c>FormDonate</c>: its text, and the donate badge, which opens the donation page.
/// </summary>
public sealed partial class DonateViewModel : DialogViewModel
{
    /// <summary>
    ///  The upstream form whose texts the window shows, by their names.
    /// </summary>
    public const string Category = "FormDonate";

    // Upstream's text and address.
    public const string DonateText =
        "We have a dedicated team of collaborators that spends a lot of time maintaining the app, working on new features and fixing bugs." +
        "You can support the project by making a financial contribution. Donations will be used to cover running costs " +
        "and to get the resources needed to keep the project running. We will also use donations to thank collaborators for their efforts.\r\n\r\n" +
        "Click on the button below to get more information about making a donation.";

    public const string DonationUrl = "https://opencollective.com/gitextensions";

    private readonly Action<string> _openUrl;

    /// <param name="openUrl">Opens an address in the browser.</param>
    public DonateViewModel(IDialogHost host, Action<string> openUrl)
        : base(host)
    {
        _openUrl = openUrl;
        Text = host.Translate(Category, "_donateText", DonateText);
    }

    public string Text { get; }

    [RelayCommand]
    private void Donate() => _openUrl(DonationUrl);
}

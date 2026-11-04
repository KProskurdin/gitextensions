using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using GitCommands;
using GitExtensions.Xplat.Core.Platform;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormAbout</c>: the product and its links, a contributor thanked at random every second (a click opens
///  <see cref="ContributorsWindow"/>), the donate badge, and the environment information with its copy button.
/// </summary>
public partial class AboutWindow : Window
{
    private const string FormName = "FormAbout";
    private const string LogoUri = "avares://GitExtensions/Assets/git-extensions-logo-256px.png";

    // Upstream's EnvironmentInfo shows the lines without their "- " marks; the copy keeps them.
    private const string LineMark = "- ";

    private readonly string? _gitVersion;
    private readonly IReadOnlyList<string> _contributors;
    private readonly string _thanksToContributors;
    private readonly Random _random = new();
    private readonly DispatcherTimer _thanksTimer;

    public AboutWindow()
        : this(null)
    {
    }

    /// <param name="gitVersion">git's version, or null when it is not known.</param>
    public AboutWindow(string? gitVersion)
    {
        _gitVersion = gitVersion;
        InitializeComponent();
        UpstreamTranslation.Apply(this, FormName);
        _NO_TRANSLATE_labelProductName.Content = AppSettings.ApplicationName;
        logoPictureBox.Source = new Bitmap(AssetLoader.Open(new Uri(LogoUri)));
        ToolTip.SetTip(copyButton, UpstreamTranslation.ByText(Contributors.CopyTooltip));

        _contributors = Contributors.List(UpstreamResources.Team, UpstreamResources.Coders,
            UpstreamResources.Translators, UpstreamResources.Designers);
        _thanksToContributors = string.Format(UpstreamTranslation.ByText(Contributors.ThanksTo), _contributors.Count);
        ThankNextContributor();
        _thanksTimer = new DispatcherTimer { Interval = Contributors.ThanksInterval };
        _thanksTimer.Tick += (_, _) => ThankNextContributor();
        _thanksTimer.Start();
        Closed += (_, _) => _thanksTimer.Stop();

        _NO_TRANSLATE_labelProductName.Click += (_, _) => OsShellUtil.OpenUrlInDefaultBrowser(Contributors.ProjectUrl);
        _NO_TRANSLATE_ThanksTo.Click += (_, _) => _ = new ContributorsWindow().ShowDialog(this);
        pictureDonate.Click += (_, _) => OsShellUtil.OpenUrlInDefaultBrowser(DonateWindow.DonationUrl);
        linkLabelIcons.Click += (_, _) => OsShellUtil.OpenUrlInDefaultBrowser(Contributors.IconsUrl);
        copyButton.Click += (_, _) => UiActions.Run(() => CopyInformationAsync(this, _gitVersion), _ => { });
        Opened += (_, _) => environmentIssueInfo.Text = Information().Replace(LineMark, "");
    }

    /// <summary>
    ///  The contributor thanked at the moment.
    /// </summary>
    public string ThanksText => ThanksToText.Text ?? "";

    /// <summary>
    ///  Upstream's <c>UserEnvironmentInformation.GetInformation</c> for this window's screen and the app's build.
    /// </summary>
    public string Information() => Information(this, _gitVersion);

    /// <summary>
    ///  Upstream's <c>UserEnvironmentInformation.GetInformation</c> for <paramref name="window"/>'s screen and the app's
    ///  build; without <paramref name="gitVersion"/>, git is asked as upstream asks it.
    /// </summary>
    public static string Information(TopLevel window, string? gitVersion)
        => EnvironmentInformation.Information(AppSettings.ProductVersion, ThisAssembly.Git.Sha,
            ThisAssembly.Git.IsDirty, gitVersion ?? EnvironmentInformation.CurrentGitVersion(), window.RenderScaling,
            AppSettings.IsPortable());

    /// <summary>
    ///  Upstream's <c>UserEnvironmentInformation.CopyInformation</c>, without the Windows desktop runtimes.
    /// </summary>
    public static async Task CopyInformationAsync(TopLevel window, string? gitVersion)
    {
        if (window.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(Information(window, gitVersion));
        }
    }

    // Upstream's ThankNextContributor: a contributor at random.
    private void ThankNextContributor()
    {
        if (_contributors.Count > 0)
        {
            ThanksToText.Text = _thanksToContributors + _contributors[_random.Next(_contributors.Count)].Trim();
        }
    }
}

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
///  Upstream's <c>FormAbout</c>, bound to <see cref="AboutViewModel"/>: the product and its links, a contributor thanked
///  at random every second (a click opens <see cref="ContributorsWindow"/>), the donate badge, and the environment
///  information with its copy button.
/// </summary>
public partial class AboutWindow : Window
{
    private const string LogoUri = "avares://GitExtensions/Assets/git-extensions-logo-256px.png";

    private readonly string? _gitVersion;

    public AboutWindow()
        : this(null)
    {
    }

    /// <param name="gitVersion">git's version, or null when it is not known.</param>
    public AboutWindow(string? gitVersion)
    {
        _gitVersion = gitVersion;
        ViewModel = new AboutViewModel(new WindowDialogHost(this), AppSettings.ApplicationName,
            Contributors.List(UpstreamResources.Team, UpstreamResources.Coders, UpstreamResources.Translators,
                UpstreamResources.Designers),
            new Random());
        DataContext = ViewModel;
        InitializeComponent();
        UpstreamTranslation.Apply(this, AboutViewModel.Category);
        logoPictureBox.Source = new Bitmap(AssetLoader.Open(new Uri(LogoUri)));
        ToolTip.SetTip(copyButton, UpstreamTranslation.ByText(Contributors.CopyTooltip));

        // View concerns: the timer that thanks the next contributor, the screen the information is read for, the browser,
        // the clipboard and the contributors window.
        DispatcherTimer thanksTimer = new() { Interval = Contributors.ThanksInterval };
        thanksTimer.Tick += (_, _) => ViewModel.ThankNextContributor();
        thanksTimer.Start();
        Closed += (_, _) => thanksTimer.Stop();
        Opened += (_, _) => ViewModel.ShowInformation(Information());
        _NO_TRANSLATE_labelProductName.Click += (_, _) => OsShellUtil.OpenUrlInDefaultBrowser(Contributors.ProjectUrl);
        _NO_TRANSLATE_ThanksTo.Click += (_, _) => _ = new ContributorsWindow().ShowDialog(this);
        pictureDonate.Click += (_, _) => OsShellUtil.OpenUrlInDefaultBrowser(DonateWindow.DonationUrl);
        linkLabelIcons.Click += (_, _) => OsShellUtil.OpenUrlInDefaultBrowser(Contributors.IconsUrl);
        copyButton.Click += (_, _) => UiActions.Run(() => CopyInformationAsync(this, _gitVersion), _ => { });
    }

    public AboutViewModel ViewModel { get; }

    /// <summary>
    ///  The contributor thanked at the moment.
    /// </summary>
    public string ThanksText => ViewModel.ThanksText;

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
}

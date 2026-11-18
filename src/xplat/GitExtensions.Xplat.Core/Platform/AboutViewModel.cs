using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitExtensions.Xplat.Core.Presentation;

namespace GitExtensions.Xplat.Core.Platform;

/// <summary>
///  The view model of upstream's <c>FormAbout</c> (texts in <see cref="Contributors"/>): the product name, a contributor
///  thanked at random (<see cref="ThankNextContributor"/>, which the window's timer calls every
///  <see cref="Contributors.ThanksInterval"/>), and the environment information.
/// </summary>
public sealed partial class AboutViewModel : DialogViewModel
{
    /// <summary>
    ///  The upstream form whose texts the window shows, by their names.
    /// </summary>
    public const string Category = "FormAbout";

    // Upstream's EnvironmentInfo shows the lines without their "- " marks; the copy keeps them.
    private const string LineMark = "- ";

    private readonly IReadOnlyList<string> _contributors;
    private readonly string _thanksToContributors;
    private readonly Random _random;

    /// <param name="productName">Upstream's <c>AppSettings.ApplicationName</c>.</param>
    /// <param name="contributors">Everyone thanked (<see cref="Contributors.List"/>).</param>
    /// <param name="random">Picks the contributor thanked.</param>
    public AboutViewModel(IDialogHost host, string productName, IReadOnlyList<string> contributors, Random random)
        : base(host)
    {
        ProductName = productName;
        _contributors = contributors;
        _random = random;
        _thanksToContributors = string.Format(Host.Translate(Contributors.ThanksTo), contributors.Count);
        ThankNextContributor();
    }

    public string ProductName { get; }

    /// <summary>
    ///  Upstream's <c>FormContributors</c>, opened by a click on the contributor thanked.
    /// </summary>
    public Func<Task>? ShowContributors { get; init; }

    /// <summary>
    ///  Upstream's <c>UserEnvironmentInformation.CopyInformation</c>: the environment information on the clipboard.
    /// </summary>
    public Func<Task>? CopyInformation { get; init; }

    /// <summary>
    ///  The contributor thanked at the moment.
    /// </summary>
    [ObservableProperty]
    public partial string ThanksText { get; private set; } = "";

    /// <summary>
    ///  Upstream's <c>environmentIssueInfo</c>: the environment information without its lines' marks.
    /// </summary>
    [ObservableProperty]
    public partial string EnvironmentInfo { get; private set; } = "";

    /// <summary>
    ///  Upstream's <c>ThankNextContributor</c>: a contributor at random.
    /// </summary>
    public void ThankNextContributor()
    {
        if (_contributors.Count > 0)
        {
            ThanksText = _thanksToContributors + _contributors[_random.Next(_contributors.Count)].Trim();
        }
    }

    /// <summary>
    ///  Shows upstream's <c>UserEnvironmentInformation.GetInformation</c>, which the window reads for its screen.
    /// </summary>
    public void ShowInformation(string information) => EnvironmentInfo = information.Replace(LineMark, "");

    [RelayCommand]
    private Task ShowContributorsWindowAsync() => ShowContributors is { } show ? GuardAsync(show) : Task.CompletedTask;

    [RelayCommand]
    private Task CopyEnvironmentInformationAsync()
        => CopyInformation is { } copy ? GuardAsync(copy) : Task.CompletedTask;
}

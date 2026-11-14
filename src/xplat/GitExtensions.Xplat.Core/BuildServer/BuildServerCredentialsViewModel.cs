using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitExtensions.Xplat.Core.Presentation;
using GitUIPluginInterfaces.BuildServerIntegration;

namespace GitExtensions.Xplat.Core.BuildServer;

/// <summary>
///  The view model of upstream's <c>FormBuildServerCredentials</c>: guest access, a user name and password, or a bearer
///  token, filled with the credentials known so far. <see cref="Credentials"/> is the answer, null until OK.
/// </summary>
public sealed partial class BuildServerCredentialsViewModel : DialogViewModel
{
    /// <summary>
    ///  The upstream form whose texts the window shows, by their names.
    /// </summary>
    public const string Category = "FormBuildServerCredentials";

    // Upstream's labelHeader text.
    private const string HeaderText = "Please enter the credentials for the build server at {0}.";

    /// <param name="buildServerUniqueKey">The build server's address, shown in the header.</param>
    /// <param name="credentials">The credentials known so far.</param>
    public BuildServerCredentialsViewModel(IDialogHost host, string buildServerUniqueKey, IBuildServerCredentials credentials)
        : base(host)
    {
        Header = string.Format(Host.Translate(Category, "labelHeader", HeaderText), buildServerUniqueKey);
        CredentialsType = credentials.BuildServerCredentialsType;
        UserName = credentials.Username;
        Password = credentials.Password;
        BearerToken = credentials.BearerToken;
    }

    public string Header { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UsesUserName))]
    [NotifyPropertyChangedFor(nameof(UsesBearerToken))]
    public partial BuildServerCredentialsType CredentialsType { get; set; }

    [ObservableProperty]
    public partial string? UserName { get; set; }

    [ObservableProperty]
    public partial string? Password { get; set; }

    [ObservableProperty]
    public partial string? BearerToken { get; set; }

    /// <summary>
    ///  Upstream's <c>UpdateUI</c>: the user name and password boxes are enabled.
    /// </summary>
    public bool UsesUserName => CredentialsType == BuildServerCredentialsType.UsernameAndPassword;

    /// <summary>
    ///  Upstream's <c>UpdateUI</c>: the bearer token box is enabled.
    /// </summary>
    public bool UsesBearerToken => CredentialsType == BuildServerCredentialsType.BearerToken;

    /// <summary>
    ///  The credentials entered, or null when the window was cancelled.
    /// </summary>
    public IBuildServerCredentials? Credentials { get; private set; }

    // Upstream's buttonOK_Click: every box's text is kept, whatever the access type.
    [RelayCommand]
    private void Ok()
    {
        Credentials = new BuildServerCredentials
        {
            BuildServerCredentialsType = CredentialsType,
            Username = UserName,
            Password = Password,
            BearerToken = BearerToken,
        };
        Host.Close(null);
    }

    [RelayCommand]
    private void Cancel() => Host.Close(null);
}

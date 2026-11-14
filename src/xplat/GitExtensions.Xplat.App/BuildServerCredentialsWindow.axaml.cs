using Avalonia.Controls;
using GitExtensions.Xplat.Core.BuildServer;
using GitExtensions.Xplat.Ui;
using GitUIPluginInterfaces.BuildServerIntegration;

namespace GitExtensions.Xplat.App;

/// <summary>
///  The new shell's version of upstream's <c>FormBuildServerCredentials</c>, bound to
///  <see cref="BuildServerCredentialsViewModel"/>: asks how to sign in to a build server, filled with the credentials known
///  so far. <see cref="Credentials"/> is the answer, null when cancelled.
/// </summary>
public partial class BuildServerCredentialsWindow : Window
{
    /// <summary>
    ///  The design-time constructor (<see cref="DesignTime"/>): the window's XAML only.
    /// </summary>
    [Obsolete(DesignTime.ConstructorOnly)]
#pragma warning disable CS8618 // The fields the window's data fills stay unset: no code of the window runs.
    public BuildServerCredentialsWindow() => InitializeComponent();
#pragma warning restore CS8618

    public BuildServerCredentialsWindow(string buildServerUniqueKey, IBuildServerCredentials credentials)
    {
        ViewModel = new BuildServerCredentialsViewModel(new WindowDialogHost(this), buildServerUniqueKey, credentials);
        InitializeComponent();
        UpstreamTranslation.Apply(this, BuildServerCredentialsViewModel.Category);

        // After the translation pass, which would otherwise leave the header unformatted: the view model translates it.
        DataContext = ViewModel;
    }

    public BuildServerCredentialsViewModel ViewModel { get; }

    /// <summary>
    ///  The credentials entered, or null when the window was cancelled.
    /// </summary>
    public IBuildServerCredentials? Credentials => ViewModel.Credentials;
}

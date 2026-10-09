using Avalonia.Controls;
using GitExtensions.Xplat.Ui;
using GitUIPluginInterfaces.BuildServerIntegration;

namespace GitExtensions.Xplat.App;

/// <summary>
///  The new shell's version of upstream's <c>FormBuildServerCredentials</c>: asks how to sign in to a build server, filled
///  with the credentials known so far. <see cref="Credentials"/> is the answer, null when cancelled.
/// </summary>
public partial class BuildServerCredentialsWindow : Window
{
    public BuildServerCredentialsWindow(string buildServerUniqueKey, IBuildServerCredentials credentials)
    {
        InitializeComponent();
        UpstreamTranslation.Apply(this, "FormBuildServerCredentials");
        labelHeader.Text = string.Format(labelHeader.Text ?? "", buildServerUniqueKey);
        radioButtonGuestAccess.IsChecked = credentials.BuildServerCredentialsType == BuildServerCredentialsType.Guest;
        radioButtonAuthenticatedUser.IsChecked =
            credentials.BuildServerCredentialsType == BuildServerCredentialsType.UsernameAndPassword;
        radioButtonBearerToken.IsChecked = credentials.BuildServerCredentialsType == BuildServerCredentialsType.BearerToken;
        textBoxUserName.Text = credentials.Username;
        textBoxPassword.Text = credentials.Password;
        textBoxBearerToken.Text = credentials.BearerToken;
        foreach (RadioButton choice in new[] { radioButtonGuestAccess, radioButtonAuthenticatedUser, radioButtonBearerToken })
        {
            choice.IsCheckedChanged += (_, _) => UpdateUI();
        }

        UpdateUI();
        buttonOK.Click += (_, _) => Accept();
        buttonCancel.Click += (_, _) => Close();
    }

    /// <summary>
    ///  The credentials entered, or null when the window was cancelled.
    /// </summary>
    public IBuildServerCredentials? Credentials { get; private set; }

    // Upstream's buttonOK_Click: every box's text is kept, whatever the access type.
    private void Accept()
    {
        Credentials = new BuildServerCredentials
        {
            BuildServerCredentialsType = radioButtonBearerToken.IsChecked == true ? BuildServerCredentialsType.BearerToken
                : radioButtonAuthenticatedUser.IsChecked == true ? BuildServerCredentialsType.UsernameAndPassword
                : BuildServerCredentialsType.Guest,
            Username = textBoxUserName.Text,
            Password = textBoxPassword.Text,
            BearerToken = textBoxBearerToken.Text,
        };
        Close();
    }

    private void UpdateUI()
    {
        textBoxUserName.IsEnabled = textBoxPassword.IsEnabled = radioButtonAuthenticatedUser.IsChecked == true;
        textBoxBearerToken.IsEnabled = radioButtonBearerToken.IsChecked == true;
    }
}

using System.Text;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using GitCommands;
using GitCommands.Settings;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Settings;
using GitExtensions.Xplat.Ui;
using GitExtUtils;

namespace GitExtensions.Plugins.ProxySwitcher;

/// <summary>
///  The new shell's version of upstream's <c>ProxySwitcherForm</c>: shows git's local (effective) and global
///  <c>http.proxy</c> with the password hidden, and sets or unsets it from the plugin's settings, locally or globally.
/// </summary>
public partial class ProxySwitcherWindow : Window
{
    private const string Category = "ProxySwitcherForm";
    private const string PleaseSetProxy = "There is no proxy configured. Please set the proxy host in the plugin settings.";
    private const string ProxySetting = "http.proxy";

    private readonly ProxySwitcherPlugin _plugin;
    private readonly SettingsSource _settings;
    private readonly IGitModule _module;

    public ProxySwitcherWindow(ProxySwitcherPlugin plugin, SettingsSource settings, IGitModule module)
    {
        _plugin = plugin;
        _settings = settings;
        _module = module;
        InitializeComponent();
        UpstreamTranslation.Apply(this, Category);

        // As upstream, the title is the plugin's description.
        Title = UpstreamTranslation.Text(Category, "_pluginDescription", "Proxy Switcher");
        SetProxy_Button.Click += (_, _) => SetProxy();
        UnsetProxy_Button.Click += (_, _) => UnsetProxy();
        Opened += (_, _) => OnOpened();
    }

    [GeneratedRegex(@":(.*)@", RegexOptions.ExplicitCapture)]
    private static partial Regex PasswordRegex { get; }

    // Upstream's ProxySwitcherForm_Load: without a proxy host in the settings there is nothing to switch to.
    private void OnOpened()
    {
        if (string.IsNullOrEmpty(_plugin.HttpProxy.ValueOrDefault(_settings)))
        {
            MessageBoxes.ShowError(new WindowOwner(this),
                UpstreamTranslation.Text(Category, "_pleaseSetProxy", PleaseSetProxy),
                Title);
            Close();
            return;
        }

        RefreshProxy();
    }

    // Upstream reads the module's cached config here, so the value it shows right after a change can be the old one; the
    // callers invalidate the cache first.
    private void RefreshProxy()
    {
        LocalProxyBox.Text = HidePassword(_module.GetEffectiveSetting(ProxySetting));
        GlobalProxyBox.Text = HidePassword(
            new GitConfigSettings(_module.GitExecutable, GitSettingLevel.Global).GetValue(ProxySetting) ?? "");
        ApplyGlobally_CheckBox.IsChecked = string.Equals(LocalProxyBox.Text, GlobalProxyBox.Text, StringComparison.Ordinal);
    }

    private static string HidePassword(string httpProxy) => PasswordRegex.Replace(httpProxy, ":****@");

    // Upstream's BuildHttpProxy: "user:password@host:port", each part only when set.
    private string BuildHttpProxy()
    {
        StringBuilder proxy = new("\"");
        string username = _plugin.Username.ValueOrDefault(_settings);
        if (!string.IsNullOrEmpty(username))
        {
            proxy.Append(username);
            string password = _plugin.Password.ValueOrDefault(_settings);
            if (!string.IsNullOrEmpty(password))
            {
                proxy.Append(':').Append(password);
            }

            proxy.Append('@');
        }

        proxy.Append(_plugin.HttpProxy.ValueOrDefault(_settings));
        string port = _plugin.HttpProxyPort.ValueOrDefault(_settings);
        if (!string.IsNullOrEmpty(port))
        {
            proxy.Append(':').Append(port);
        }

        return proxy.Append('"').ToString();
    }

    private void SetProxy()
    {
        GitArgumentBuilder args = new("config")
        {
            { ApplyGlobally_CheckBox.IsChecked == true, "--global" },
            ProxySetting,
            BuildHttpProxy(),
        };
        _module.GitExecutable.GetOutput(args);
        _module.InvalidateGitSettings();
        RefreshProxy();
    }

    private void UnsetProxy()
    {
        GitArgumentBuilder args = new("config")
        {
            { ApplyGlobally_CheckBox.IsChecked == true, "--global" },
            "--unset",
            ProxySetting,
        };
        _module.GitExecutable.GetOutput(args);
        _module.InvalidateGitSettings();
        RefreshProxy();
    }
}

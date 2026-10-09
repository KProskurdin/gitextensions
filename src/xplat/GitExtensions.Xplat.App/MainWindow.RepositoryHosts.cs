using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Plugins;
using GitExtensions.Xplat.App.RepositoryHosts;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;
using MessageBoxIcon = System.Windows.Forms.MessageBoxIcon;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream <c>FormBrowse</c>'s repository host menu: named after the first repository host plugin (upstream supports one),
///  with its fork and clone, pull request and upstream remote commands, run through the plugin host as upstream runs them
///  through <c>GitUICommands</c>.
/// </summary>
public partial class MainWindow
{
    private void WireRepositoryHosts()
    {
        ForkCloneRepositoryMenuItem.Click += (_, _) =>
        {
            if (_loadedPlugins?.OfType<IRepositoryHostPlugin>().FirstOrDefault() is { } host)
            {
                _plugins.StartCloneForkFromHoster(_plugins.Owner, host, gitModuleChanged: null);
            }
            else
            {
                MessageBoxes.ShowError(_plugins.Owner, RepositoryHostTexts.NoReposHostPluginLoaded, RepositoryHostTexts.Error);
            }
        };
        ViewPullRequestsMenuItem.Click += (_, _) => WithRepositoryHost(host => _plugins.StartPullRequestsDialog(_plugins.Owner, host));
        CreatePullRequestsMenuItem.Click += (_, _) => WithRepositoryHost(host => _plugins.StartCreatePullRequest(_plugins.Owner, host));
        AddUpstreamRemoteMenuItem.Click += (_, _) => WithRepositoryHost(host => _plugins.AddUpstreamRemote(_plugins.Owner, host));
    }

    // Upstream's UpdateRepositoryHostsMenu, once the plugins are loaded.
    private void ShowRepositoryHostsMenu(IReadOnlyList<IGitPlugin> plugins)
    {
        IRepositoryHostPlugin? host = plugins.OfType<IRepositoryHostPlugin>().FirstOrDefault();
        RepositoryHostsMenu.IsVisible = host is not null;
        if (host is not null)
        {
            RepositoryHostsMenu.Header = host.Name;
        }
    }

    // Upstream's PluginRegistry.TryGetGitHosterForModule.
    private IRepositoryHostPlugin? RelevantRepositoryHost()
        => RepositoryPath is null
            ? null
            : _plugins.Registered.OfType<IRepositoryHostPlugin>().FirstOrDefault(candidate => candidate.GitModuleIsRelevantToMe());

    // Upstream's TryGetRepositoryHost: the registered host plugin that knows a remote of the open repository.
    private void WithRepositoryHost(Action<IRepositoryHostPlugin> action)
    {
        if (RelevantRepositoryHost() is not { } host)
        {
            MessageBoxes.Show(_plugins.Owner, RepositoryHostTexts.NoReposHostFound, RepositoryHostTexts.Error, MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        action(host);
    }
}

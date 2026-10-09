using System.Globalization;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using GitCommands;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Plugins;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Ui;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;
using MessageBoxIcon = System.Windows.Forms.MessageBoxIcon;

namespace GitExtensions.Xplat.App.RepositoryHosts;

/// <summary>
///  A repository of a repository host as the fork and clone lists show it; a row without a repository is upstream's
///  " : LOADING : " or " : SEARCHING : " placeholder.
/// </summary>
public sealed record HostedRepositoryRow(IHostedRepository? Repo, string Name, string Owner = "", string IsFork = "",
    string Forks = "", string IsPrivate = "");

/// <summary>
///  The new shell's version of upstream's <c>ForkAndCloneForm</c>: the user's repositories on the host and a search of
///  others, forking one, and cloning the selected one with upstream's options (folder, upstream remote, protocol, depth).
///  The host plugin is asked off the UI thread, as upstream does.
/// </summary>
public partial class ForkAndCloneWindow : Window
{
    private const string Category = "ForkAndCloneForm";
    private const string UpstreamRemoteName = "upstream";

    private readonly IRepositoryHostPlugin _gitHoster;
    private readonly RepositoryOperationsViewModel _actions;
    private readonly EventHandler<GitModuleEventArgs>? _gitModuleChanged;
    private bool _updating;

    public ForkAndCloneWindow(IRepositoryHostPlugin gitHoster, RepositoryOperationsViewModel actions, string defaultDestination,
        EventHandler<GitModuleEventArgs>? gitModuleChanged = null)
    {
        _gitHoster = gitHoster;
        _actions = actions;
        _gitModuleChanged = gitModuleChanged;
        InitializeComponent();
        UpstreamTranslation.Apply(this, Category);
        Title = $"{gitHoster.Name}: {Title}";
        destinationTB.Text = defaultDestination;
        tabControl.SelectionChanged += (_, e) =>
        {
            if (e.Source == tabControl)
            {
                UpdateCloneInfo();
            }
        };
        myReposLV.SelectionChanged += (_, _) => UpdateCloneInfo();
        searchResultsLV.SelectionChanged += (_, _) => OnSearchResultSelected();
        createDirTB.TextChanged += (_, _) => UpdateCloneInfo(updateCreateDir: false, updateProtocols: false);
        destinationTB.TextChanged += (_, _) => UpdateCloneInfo(updateCreateDir: false, updateProtocols: false);
        addUpstreamRemoteAsCB.PropertyChanged += (_, e) =>
        {
            if (e.Property == ComboBox.TextProperty)
            {
                UpdateCloneInfo(updateCreateDir: false, updateProtocols: false);
            }
        };
        ProtocolDropdownList.SelectionChanged += (_, _) => OnProtocolChanged();
        searchBtn.Click += (_, _) => _ = SearchAsync(byUser: false);
        getFromUserBtn.Click += (_, _) => _ = SearchAsync(byUser: true);
        searchTB.KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Enter)
            {
                _ = SearchAsync(byUser: false);
            }
        };
        forkBtn.Click += (_, _) => _ = ForkAsync();
        browseForCloneToDirbtn.Click += (_, _) => _ = BrowseAsync();
        cloneBtn.Click += (_, _) => _ = CloneSelectedAsync();
        openGitupPageBtn.Click += (_, _) => OpenHomepage();
        closeBtn.Click += (_, _) => Close();
        Opened += (_, _) =>
        {
            UpdateCloneInfo();
            _ = UpdateMyReposAsync();
        };
    }

    private static string Text(string name, string english) => UpstreamTranslation.Text(Category, name, english);

    private IHostedRepository? CurrentlySelectedGitRepo
        => ((tabControl.SelectedItem == searchReposPage ? searchResultsLV : myReposLV).SelectedItem as HostedRepositoryRow)?.Repo;

    private WindowOwner MessageOwner => new(this);

    // Upstream's UpdateMyRepos.
    private async Task UpdateMyReposAsync()
    {
        myReposLV.ItemsSource = new[] { new HostedRepositoryRow(null, Text("_strLoading", " : LOADING : ")) };
        try
        {
            IReadOnlyList<IHostedRepository> repos = await Task.Run(() => _gitHoster.GetMyRepos());
            myReposLV.ItemsSource = repos.OrderBy(repo => repo.Name)
                .Select(repo => new HostedRepositoryRow(repo, repo.Name, repo.Owner ?? "", YesNo(repo.IsAFork),
                    repo.Forks.ToString(CultureInfo.CurrentCulture), YesNo(repo.IsPrivate)))
                .ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            myReposLV.ItemsSource = null;
            helpTextLbl.Text = string.Format(Text("_strFailedToGetRepos",
                    "Failed to get repositories. This most likely means you didn't configure {0}, please do so via the menu \"Plugins/{0}\"."),
                _gitHoster.Name) + $"{Environment.NewLine}{Environment.NewLine}Exception: {ex.Message}{Environment.NewLine}{Environment.NewLine}{helpTextLbl.Text}";
        }
    }

    // Upstream's _searchBtn_Click and _getFromUserBtn_Click.
    private async Task SearchAsync(bool byUser)
    {
        string search = (searchTB.Text ?? "").Trim();
        if (search.Length == 0)
        {
            return;
        }

        searchBtn.IsEnabled = false;
        searchResultsLV.ItemsSource = new[] { new HostedRepositoryRow(null, Text("_strSearching", " : SEARCHING : ")) };
        try
        {
            IReadOnlyList<IHostedRepository> repos = await Task.Run(() =>
                byUser ? _gitHoster.GetRepositoriesOfUser(search) : _gitHoster.SearchForRepository(search));
            searchResultsLV.ItemsSource = repos.OrderBy(repo => repo.Name)
                .Select(repo => new HostedRepositoryRow(repo, repo.Name, repo.Owner ?? "", YesNo(repo.IsAFork),
                    repo.Forks.ToString(CultureInfo.CurrentCulture)))
                .ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            searchResultsLV.ItemsSource = null;
            string message = !byUser ? Text("_strSearchFailed", "Search failed!") + Environment.NewLine + ex.Message
                : ex.Message.Contains("404") ? Text("_strUserNotFound", "User not found!")
                : Text("_strCouldNotFetchReposOfUser", "Could not fetch repositories of user!") + Environment.NewLine + ex.Message;
            ShowError(message);
        }
        finally
        {
            searchBtn.IsEnabled = true;
        }
    }

    private void OnSearchResultSelected()
    {
        UpdateCloneInfo();
        IHostedRepository? repo = (searchResultsLV.SelectedItem as HostedRepositoryRow)?.Repo;
        forkBtn.IsEnabled = repo is not null;
        if (repo is not null)
        {
            searchResultItemDescription.Text = repo.Description;
        }
    }

    // Upstream's _forkBtn_Click: the user's repositories are read again, with the new fork.
    private async Task ForkAsync()
    {
        if ((searchResultsLV.SelectedItem as HostedRepositoryRow)?.Repo is not { } repo)
        {
            ShowError(Text("_strSelectOneItem", "You must select exactly one item"));
            return;
        }

        try
        {
            await Task.Run(repo.Fork);
        }
        catch (Exception ex)
        {
            ShowError(Text("_strFailedToFork", "Failed to fork:") + Environment.NewLine + ex.Message);
        }

        tabControl.SelectedItem = myReposPage;
        await UpdateMyReposAsync();
    }

    private async Task BrowseAsync()
    {
        IStorageFolder? start = string.IsNullOrWhiteSpace(destinationTB.Text)
            ? null
            : await StorageProvider.TryGetFolderFromPathAsync(destinationTB.Text);
        IReadOnlyList<IStorageFolder> folders = await StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions { SuggestedStartLocation = start });
        if (folders is [{ } folder] && folder.TryGetLocalPath() is { } path)
        {
            destinationTB.Text = path;
        }
    }

    // Upstream's Clone: an upstream remote is added to the clone when asked for and the repository is a fork.
    private async Task CloneSelectedAsync()
    {
        if (CurrentlySelectedGitRepo is not { } repo || GetTargetDir() is not { } targetDir)
        {
            return;
        }

        int depth = (int)(depthUpDown.Value ?? 0);
        if (!await _actions.CloneAsync(repo.CloneUrl, targetDir, depth > 0 ? depth : null))
        {
            return;
        }

        GitModule module = GitModules.Open(targetDir);
        string upstreamRemote = (addUpstreamRemoteAsCB.Text ?? "").Trim();
        if (upstreamRemote.Length > 0 && !string.IsNullOrEmpty(repo.ParentUrl))
        {
            string error = module.AddRemote(upstreamRemote, repo.ParentUrl);
            if (!string.IsNullOrEmpty(error))
            {
                MessageBoxes.Show(MessageOwner, error, Text("_strCouldNotAddRemote", "Could not add remote"), MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        _gitModuleChanged?.Invoke(this, new GitModuleEventArgs(module));
        Close();
    }

    private void OpenHomepage()
    {
        if (CurrentlySelectedGitRepo is not { } repo)
        {
            return;
        }

        string homepage = repo.Homepage;
        if (string.IsNullOrEmpty(homepage) || !(homepage.StartsWith("http://") || homepage.StartsWith("https://")))
        {
            ShowError(Text("_strNoHomepageDefined", "No homepage defined"));
        }
        else
        {
            OsShellUtil.OpenUrlInDefaultBrowser(homepage);
        }
    }

    // Upstream's UpdateCloneInfo: the selected repository's name as the folder, its parent's owner as the upstream remote,
    // its protocols, and what the clone will do.
    private void UpdateCloneInfo(bool updateCreateDir = true, bool updateProtocols = true)
    {
        if (_updating)
        {
            return;
        }

        _updating = true;
        try
        {
            IHostedRepository? repo = CurrentlySelectedGitRepo;
            if (repo is null)
            {
                SetProtocolSelectionVisibility(false);
                cloneBtn.IsEnabled = false;
                cloneInfoText.Text = "";
                createDirTB.Text = "";
                return;
            }

            bool multipleProtocols = repo.SupportedCloneProtocols.Any();
            if (multipleProtocols && updateProtocols)
            {
                GitProtocol current = ProtocolDropdownList.SelectedItem as GitProtocol? ?? repo.SupportedCloneProtocols[0];
                ProtocolDropdownList.ItemsSource = repo.SupportedCloneProtocols;
                if (repo.SupportedCloneProtocols.Contains(current))
                {
                    repo.CloneProtocol = current;
                }

                ProtocolDropdownList.SelectedItem = repo.CloneProtocol;
            }

            SetProtocolSelectionVisibility(multipleProtocols);
            if (updateCreateDir)
            {
                createDirTB.Text = repo.Name;
                addUpstreamRemoteAsCB.ItemsSource = repo.ParentOwner is { } parentOwner
                    ? new[] { parentOwner, UpstreamRemoteName }
                    : Array.Empty<string>();
                addUpstreamRemoteAsCB.Text = repo.ParentOwner ?? "";
                addUpstreamRemoteAsCB.IsEnabled = repo.ParentOwner is not null;
            }

            cloneBtn.IsEnabled = true;
            SetCloneInfoText(repo);
        }
        finally
        {
            _updating = false;
        }
    }

    private void SetCloneInfoText(IHostedRepository repo)
    {
        string upstreamRemote = (addUpstreamRemoteAsCB.Text ?? "").Trim();
        string moreInfo = upstreamRemote.Length > 0
            ? string.Format(Text("_strWillBeAddedAsARemote", "\"{0}\" will be added as a remote."), upstreamRemote)
            : "";
        string format = tabControl.SelectedItem == searchReposPage
            ? Text("_strWillCloneInfo", "Will clone {0} into {1}.\r\nYou can not push unless you are a collaborator. {2}")
            : Text("_strWillCloneWithPushAccess", "Will clone {0} into {1}.\r\nYou will have push access. {2}");
        cloneInfoText.Text = string.Format(format, repo.CloneUrl, TargetDir() ?? "", moreInfo).ReplaceLineEndings();
    }

    private void SetProtocolSelectionVisibility(bool visible)
    {
        ProtocolLabel.IsVisible = visible;
        ProtocolDropdownList.IsVisible = visible;
    }

    private void OnProtocolChanged()
    {
        if (!_updating && CurrentlySelectedGitRepo is { } repo && ProtocolDropdownList.SelectedItem is GitProtocol protocol)
        {
            repo.CloneProtocol = protocol;
            SetCloneInfoText(repo);
        }
    }

    // Upstream's GetTargetDir: an empty destination is an error when cloning.
    private string? GetTargetDir()
    {
        if (TargetDir() is { } targetDir)
        {
            return targetDir;
        }

        ShowError(Text("_strCloneFolderCanNotBeEmpty", "Clone folder can not be empty"));
        return null;
    }

    private string? TargetDir()
    {
        string destination = (destinationTB.Text ?? "").Trim();
        return destination.Length == 0 ? null : Path.Combine(destination, createDirTB.Text ?? "");
    }

    private static string YesNo(bool value) => value ? RepositoryHostTexts.Yes : RepositoryHostTexts.No;

    private void ShowError(string message)
        => MessageBoxes.Show(MessageOwner, message, RepositoryHostTexts.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
}

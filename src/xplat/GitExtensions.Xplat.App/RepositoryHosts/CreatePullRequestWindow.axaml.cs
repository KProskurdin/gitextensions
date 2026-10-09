using Avalonia.Controls;
using GitCommands;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Plugins;
using GitExtensions.Xplat.Ui;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;
using MessageBoxIcon = System.Windows.Forms.MessageBoxIcon;

namespace GitExtensions.Xplat.App.RepositoryHosts;

/// <summary>
///  The new shell's version of upstream's <c>CreatePullRequestForm</c>: a pull request from a branch of the user's own
///  hosted repository to a branch of another hosted repository of the module (its upstream), with the title taken from the
///  branch's last commit and the body from the repository's <c>.github/PULL_REQUEST_TEMPLATE.md</c>.
/// </summary>
public partial class CreatePullRequestWindow : Window
{
    private const string Category = "CreatePullRequestForm";

    private readonly IRepositoryHostPlugin _repoHost;
    private readonly IGitModule _module;
    private readonly string? _chooseRemote;
    private IReadOnlyList<IHostedRemote> _hostedRemotes = [];
    private IHostedRemote? _currentHostedRemote;
    private string? _prevTitle;

    public CreatePullRequestWindow(IRepositoryHostPlugin repoHost, IGitModule module, string? chooseRemote = null)
    {
        _repoHost = repoHost;
        _module = module;
        _chooseRemote = chooseRemote;
        InitializeComponent();
        UpstreamTranslation.Apply(this, Category);
        _prevTitle = _titleTB.Text;
        _pullReqTargetsCB.DisplayMemberBinding = new Avalonia.Data.Binding(nameof(IHostedRemote.DisplayData));
        _pullReqTargetsCB.SelectionChanged += (_, _) => OnTargetChanged();
        _yourBranchesCB.SelectionChanged += (_, _) => _ = SuggestTitleAsync();
        _createBtn.Click += (_, _) => _ = CreateAsync();
        Opened += (_, _) => _ = LoadAsync();
    }

    /// <summary>
    ///  True once the target and the user's branches are listed.
    /// </summary>
    public bool AreBranchesLoaded { get; private set; }

    private static string Text(string name, string english) => UpstreamTranslation.Text(Category, name, english);

    private WindowOwner MessageOwner => new(this);

    private IHostedRemote? MyRemote => _hostedRemotes.FirstOrDefault(remote => remote.IsOwnedByMe);

    // Upstream's CreatePullRequestForm_Load: without a hosted repository owned by someone else there is no target, and the
    // window closes.
    private async Task LoadAsync()
    {
        _yourBranchesCB.Text = Text("_strLoading", "Loading...");
        _hostedRemotes = await Task.Run(() => _repoHost.GetHostedRemotesForModule());
        IHostedRemote[] foreignHostedRemotes = [.. _hostedRemotes.Where(remote => !remote.IsOwnedByMe)];
        if (foreignHostedRemotes.Length == 0)
        {
            MessageBoxes.Show(MessageOwner,
                Text("_strFailedToCreatePullRequest", "Failed to create pull request.") + Environment.NewLine
                + Text("_strPleaseCloneGitHubRep", "Please clone GitHub repository before pull request."), "",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            Close();
            return;
        }

        _pullReqTargetsCB.ItemsSource = foreignHostedRemotes;
        _pullReqTargetsCB.SelectedItem = foreignHostedRemotes.FirstOrDefault(remote => remote.Name == _chooseRemote)
            ?? foreignHostedRemotes[0];
        Task myBranches = MyRemote is { } myRemote ? PopulateBranchesAsync(myRemote, _yourBranchesCB) : Task.CompletedTask;
        string templatePath = Path.Join(_module.WorkingDir, ".github", "PULL_REQUEST_TEMPLATE.md");
        if (File.Exists(templatePath))
        {
            try
            {
                _bodyTB.Text = await File.ReadAllTextAsync(templatePath);
            }
            catch (IOException ex)
            {
                MessageBoxes.ShowError(MessageOwner,
                    Text("_strFailedToLoadTemplate", "Failed to load PR template from file.") + Environment.NewLine + ex.Message);
            }
        }

        await myBranches;
        AreBranchesLoaded = true;
    }

    private void OnTargetChanged()
    {
        if (_pullReqTargetsCB.SelectedItem is IHostedRemote remote)
        {
            _currentHostedRemote = remote;
            _ = PopulateBranchesAsync(remote, _remoteBranchesCB);
        }
    }

    // Upstream's PopulateBranchesComboAndEnableCreateButton: the hosted repository's branches with its default one chosen.
    private async Task PopulateBranchesAsync(IHostedRemote remote, ComboBox comboBox)
    {
        comboBox.ItemsSource = null;
        comboBox.Text = Text("_strLoading", "Loading...");
        try
        {
            (IReadOnlyList<IHostedBranch> branches, string defaultBranch) = await Task.Run(() =>
            {
                IHostedRepository hostedRepository = remote.GetHostedRepository();
                return (hostedRepository.GetBranches(), hostedRepository.GetDefaultBranch());
            });
            List<string> names = [.. branches.Select(branch => branch.Name)];
            comboBox.ItemsSource = names;
            comboBox.SelectedItem = names.Contains(defaultBranch) ? defaultBranch : names.FirstOrDefault();
            if (names.Count == 0)
            {
                comboBox.Text = "";
            }

            _createBtn.IsEnabled = true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            comboBox.Text = "";
            MessageBoxes.Show(MessageOwner, string.Format(RepositoryHostTexts.RemoteInError, ex.Message, remote.DisplayData),
                Text("_strRemoteFailToLoadBranches", "Fail to load target branches"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // Upstream's _yourBranchCB_SelectedIndexChanged: until the user types a title, it is the subject of the branch's last
    // commit (on the user's remote).
    private async Task SuggestTitleAsync()
    {
        if (_prevTitle != _titleTB.Text || _yourBranchesCB.SelectedItem is not string branch || MyRemote is not { } myRemote)
        {
            return;
        }

        string revision = myRemote.Name.Combine("/", branch)!;
        string? lastMessage = await Task.Run(() =>
            _module.GetPreviousCommitMessages(count: 1, revision: revision, authorPattern: string.Empty).FirstOrDefault());
        _titleTB.Text = lastMessage?.SubstringUntil('\n');
        _prevTitle = _titleTB.Text;
    }

    private async Task CreateAsync()
    {
        if (_currentHostedRemote is not { } remote)
        {
            return;
        }

        string title = (_titleTB.Text ?? "").Trim();
        string body = (_bodyTB.Text ?? "").Trim();
        if (title.Length == 0)
        {
            MessageBoxes.Show(MessageOwner, Text("_strYouMustSpecifyATitle", "You must specify a title."), RepositoryHostTexts.Error,
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        string myBranch = _yourBranchesCB.Text ?? "";
        string remoteBranch = _remoteBranchesCB.Text ?? "";
        try
        {
            await Task.Run(() => remote.GetHostedRepository().CreatePullRequest(myBranch, remoteBranch, title, body));
            MessageBoxes.Show(MessageOwner, Text("_strDone", "Done"), Text("_strPullRequest", "Pull request"), MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            Close();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            MessageBoxes.Show(MessageOwner, Text("_strFailedToCreatePullRequest", "Failed to create pull request.")
                + Environment.NewLine + ex.Message, RepositoryHostTexts.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}

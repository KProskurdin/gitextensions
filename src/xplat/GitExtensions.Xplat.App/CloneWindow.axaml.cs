using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using GitCommands;
using GitCommands.Config;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;
using GitExtensions.Xplat.Ui;
using DialogResult = System.Windows.Forms.DialogResult;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormClone</c> in the new shell (rules in <see cref="CloneDialog"/>): clones a URL or local repository
///  into a new subdirectory of the destination, with the branch to check out (read from the remote when the list opens),
///  the repository type and the options; afterwards it asks whether to open the clone, as upstream does when opened from
///  the browse window. Closes with true after cloning.
/// </summary>
public partial class CloneWindow : Window
{
    private const string ErrorClass = "error";
    private const string Origin = "origin";
    private const string ExceptionPrefix = "Exception: ";

    // Upstream's BrowseForPrivateKey texts.
    private const string SshKeyTitle = "Browse for key";
    private const string SshKeyFilter = " (*.ppk)";
    private const string SshKeyPattern = "*.ppk";

    private readonly string? _url;
    private readonly RepositoryOperationsViewModel _actions;
    private readonly IAppPreferences _preferences;
    private readonly IReadOnlyList<string> _recentRepositories;
    private readonly Func<string, Task> _addRecent;
    private readonly Func<string, Task> _openRepository;
    private readonly GitModule _module;
    private readonly CloneDialog _dialog = new();
    private readonly TaskCompletionSource _loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private CancellationTokenSource? _branchLoad;
    private string? _puttySshKey;

    /// <param name="repositoryPath">The open repository, whose remote and folder the dialog proposes; null for none.</param>
    /// <param name="recentRepositories">The recent local repositories, whose parent folders are offered as destinations.</param>
    /// <param name="addRecent">Adds the clone to the recent repositories.</param>
    /// <param name="openRepository">Opens the clone, when the user wants to.</param>
    /// <param name="url">The URL (or destination folder) to start with; null for none.</param>
    public CloneWindow(string? repositoryPath, RepositoryOperationsViewModel actions, IAppPreferences preferences,
        IReadOnlyList<string> recentRepositories, Func<string, Task> addRecent, Func<string, Task> openRepository,
        string? url = null)
    {
        _url = url;
        _actions = actions;
        _preferences = preferences;
        _recentRepositories = recentRepositories;
        _addRecent = addRecent;
        _openRepository = openRepository;
        _module = GitModules.Open(repositoryPath ?? "");
        InitializeComponent();
        LoadSshKeyButton.IsVisible = GitSshHelpers.IsPlink;
        BranchesBox.ItemsSource = CloneDialog.DefaultBranchItems;
        BranchesBox.Text = CloneDialog.BranchDefaultRemoteHead;

        FromBox.PropertyChanged += (_, e) =>
        {
            if (e.Property == ComboBox.TextProperty)
            {
                FromTextUpdate();
            }
        };
        ToBox.PropertyChanged += (_, e) =>
        {
            if (e.Property == ComboBox.TextProperty)
            {
                ShowInfo();
            }
        };
        SubdirectoryBox.TextChanged += (_, _) => ShowInfo();
        BranchesBox.DropDownOpened += (_, _) => Run(() => BranchesLoaded = LoadBranchesAsync());
        FromBrowseButton.Click += (_, _) => Run(() => BrowseAsync(FromBox));
        ToBrowseButton.Click += (_, _) => Run(() => BrowseAsync(ToBox));
        LoadSshKeyButton.Click += (_, _) => Run(LoadSshKeyAsync);
        CloneButton.Click += (_, _) => Run(CloneAsync);
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Close(false);
            }
        };
        Opened += (_, _) => Run(LoadAsync);
        Closed += (_, _) => _branchLoad?.Cancel();
    }

    /// <summary>
    ///  The dialog state, for tests.
    /// </summary>
    public CloneDialog Dialog => _dialog;

    /// <summary>
    ///  Completes when the histories, the source and the destination are shown, for tests.
    /// </summary>
    public Task CloneLoaded => _loaded.Task;

    /// <summary>
    ///  The last read of the remote's branches, for tests.
    /// </summary>
    public Task BranchesLoaded { get; private set; } = Task.CompletedTask;

    // Upstream's OnRuntimeLoad: the remote history, the recent repositories' folders, and the source and destination
    // proposed from the URL given, the clipboard or the open repository.
    private async Task LoadAsync()
    {
        IList<GitCommands.UserRepositoryHistory.Repository> remotes = await AppServices.RemoteUrls.LoadAsync();
        FromBox.ItemsSource = remotes.Select(remote => remote.Path).ToList();
        ToBox.ItemsSource = await Task.Run(() => CloneDialog.DestinationsFromRecent(_recentRepositories));

        string? clipboardText = PathUtil.CanBeGitURL(_url) ? null : await ClipboardTextAsync();
        string defaultDestination = _preferences.DefaultCloneDestinationPath;
        (string from, string destination) = await Task.Run(() =>
            CloneDialog.Initial(_url, defaultDestination, clipboardText, _module));
        ToBox.Text = destination;
        FromBox.Text = from;
        FromTextUpdate();
        FromBox.Focus();
        _loaded.TrySetResult();
    }

    // Upstream tries the clipboard to be more helpful, and ignores a clipboard it cannot read.
    private async Task<string?> ClipboardTextAsync()
    {
        try
        {
            return GetTopLevel(this)?.Clipboard is { } clipboard ? await clipboard.TryGetTextAsync() : null;
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException or IOException)
        {
            return null;
        }
    }

    // Upstream's FromTextUpdate: the subdirectory named after the repository, and the branch list back to its defaults.
    private void FromTextUpdate()
    {
        _dialog.From = FromBox.Text ?? "";
        SubdirectoryBox.Text = _dialog.Subdirectory;
        _branchLoad?.Cancel();
        BranchesBox.ItemsSource = CloneDialog.DefaultBranchItems;
        BranchesBox.Text = CloneDialog.BranchDefaultRemoteHead;
        ShowInfo();
    }

    // Upstream's ToTextUpdate.
    private void ShowInfo()
    {
        _dialog.Destination = ToBox.Text ?? "";
        _dialog.Subdirectory = SubdirectoryBox.Text ?? "";
        (string text, bool isError) = _dialog.LocationInfo(UpstreamTranslation.ByText);
        InfoText.Text = text;
        InfoText.Classes.Set(ErrorClass, isError);
    }

    // Upstream's LoadBranches, when the branch list opens: the remote's branches after the defaults, keeping the text when
    // it is one of them.
    private async Task LoadBranchesAsync()
    {
        if (_branchLoad is { } previous)
        {
            await previous.CancelAsync();
        }

        CancellationTokenSource load = new();
        _branchLoad = load;
        CancellationToken cancellationToken = load.Token;
        string from = FromBox.Text ?? "";
        (IReadOnlyList<IGitRef> refs, string? error) = await Task.Run(() =>
        {
            IReadOnlyList<IGitRef> found = _module.GetRemoteServerRefs(from, false, true, out string? errorOutput,
                cancellationToken);
            return (found, errorOutput);
        });
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (!string.IsNullOrEmpty(error))
        {
            throw new InvalidOperationException(error);
        }

        string text = BranchesBox.Text ?? "";
        IReadOnlyList<string> names = CloneDialog.BranchItems(refs);
        BranchesBox.ItemsSource = names;
        BranchesBox.Text = names.Contains(text) ? text : CloneDialog.BranchDefaultRemoteHead;
    }

    private async Task BrowseAsync(ComboBox box)
    {
        IStorageFolder? start = string.IsNullOrWhiteSpace(box.Text) || !Directory.Exists(box.Text)
            ? null
            : await StorageProvider.TryGetFolderFromPathAsync(box.Text);
        IReadOnlyList<IStorageFolder> folders = await StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions { AllowMultiple = false, SuggestedStartLocation = start });
        if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } path)
        {
            box.Text = path;
        }
    }

    // Upstream's BrowseForPrivateKey: the key is set on the clone's origin after cloning.
    private async Task LoadSshKeyAsync()
    {
        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = SshKeyTitle,
            FileTypeFilter = [new FilePickerFileType(SshKeyFilter) { Patterns = [SshKeyPattern] }],
        });
        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path)
        {
            _puttySshKey = path;
        }
    }

    // Upstream's OkClick: the checks, the new folder, the clone with git's progress, the histories, the SSH key and the
    // question whether to open the clone.
    private async Task CloneAsync()
    {
        if (_branchLoad is { } load)
        {
            await load.CancelAsync();
        }

        _dialog.From = FromBox.Text ?? "";
        _dialog.Destination = ToBox.Text ?? "";
        _dialog.Subdirectory = SubdirectoryBox.Text ?? "";
        _dialog.Branch = BranchesBox.Text ?? "";
        _dialog.Bare = CentralRepositoryRadio.IsChecked == true;
        _dialog.InitializeAllSubmodules = InitializeSubmodulesCheck.IsChecked == true;
        _dialog.DownloadFullHistory = DownloadFullHistoryCheck.IsChecked == true;
        string caption = UpstreamTranslation.ByText(CloneDialog.ErrorCloneFailed);
        if (_dialog.Validate() is { } error)
        {
            await new MessageBoxWindow(UpstreamTranslation.ByText(error), caption, MessageBoxButtons.OK)
                .ShowDialog(this);
            ToBox.Focus();
            return;
        }

        string target;
        ArgumentString arguments;
        try
        {
            target = _dialog.TargetPath();
            arguments = await Task.Run(() =>
            {
                Directory.CreateDirectory(target);
                return _dialog.Arguments(target, GitModules.Open(target).GetPathForGitExecution);
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException
                                       or NotSupportedException)
        {
            await new MessageBoxWindow(ExceptionPrefix + ex.Message, caption, MessageBoxButtons.OK).ShowDialog(this);
            return;
        }

        if (!await _actions.CloneAsync(_dialog.From, target, arguments))
        {
            return;
        }

        await AppServices.RemoteUrls.AddAsMostRecentAsync(_dialog.From.Trim());
        await _addRecent(target);
        if (_puttySshKey is { Length: > 0 } key)
        {
            await Task.Run(() => GitModules.Open(target)
                .SetSetting(string.Format(SettingKeyString.RemotePuttySshKey, Origin), key));
        }

        MessageBoxWindow question = new(string.Format(UpstreamTranslation.ByText(CloneDialog.QuestionOpenRepo), target),
            UpstreamTranslation.ByText(CloneDialog.QuestionOpenRepoCaption), MessageBoxButtons.YesNo);
        await question.ShowDialog(this);
        if (question.Result == DialogResult.Yes)
        {
            await _openRepository(target);
        }

        Close(true);
    }

    private void Run(Func<Task> action)
        => UiActions.Run(action, ex => _ = new ErrorWindow(ex.Message).ShowDialog(this));
}

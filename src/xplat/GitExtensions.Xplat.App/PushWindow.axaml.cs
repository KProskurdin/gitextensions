using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Settings;
using GitExtensions.Xplat.Ui;
using DialogResult = System.Windows.Forms.DialogResult;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormPush</c> in the new shell: where to push (a remote or a URL), and a branch, a tag, or several
///  branches. Closes with the push to run, or null. The checks before pushing are upstream's: the choices are complete, a
///  branch new for the remote is confirmed, a tracking reference is offered, and a force push is offered as force with
///  lease.
/// </summary>
public partial class PushWindow : Window
{
    private readonly PushDialog _dialog;
    private readonly IAppPreferences _preferences;
    private bool _showing;
    private readonly Func<Window, string?, Task<PushRepository>>? _manageRemotes;
    private string? _rowsRemote;

    /// <param name="canCreatePullRequest">True when a repository host plugin knows the repository.</param>
    /// <param name="openPull">Opens the pull dialog over this window (upstream's Pull button).</param>
    /// <param name="manageRemotes">Opens the remotes window over this window with a remote chosen, and reads the
    ///  repository again (upstream's Manage remotes).</param>
    public PushWindow(PushDialog dialog, IAppPreferences preferences, string workingDirectory,
        bool canCreatePullRequest = false, Func<Window, Task>? openPull = null,
        Func<Window, string?, Task<PushRepository>>? manageRemotes = null)
    {
        _dialog = dialog;
        _preferences = preferences;
        _manageRemotes = manageRemotes;
        InitializeComponent();
        InitialFocus.Attach(this, () => PushButton);
        Title = string.Format("{0} ({1})", UpstreamTranslation.ByText(PushDialog.PushCaption), workingDirectory);
        dialog.RecursiveSubmodules = preferences.RecursiveSubmodules;
        RemotesBox.ItemsSource = dialog.Remotes;
        BranchBox.ItemsSource = dialog.Branches;
        TagBox.ItemsSource = dialog.Tags;
        RecursiveSubmodulesBox.ItemsSource = PushDialog.RecursiveSubmoduleChoices;
        ToolTip.SetTip(ForceWithLeaseCheck, UpstreamTranslation.ByText(PushDialog.ForceWithLeaseTooltip));
        CreatePullRequestCheck.IsEnabled = canCreatePullRequest;

        AddRemoteButton.IsEnabled = manageRemotes is not null;
        AddRemoteButton.Click += (_, _) => UiActions.Run(() => ManageRemotesAsync(_dialog.Remote), _ => { });
        PullButton.IsEnabled = openPull is not null;
        PullButton.Click += (_, _) => UiActions.Run(() => openPull!(this), _ => { });

        if (preferences.AlwaysShowAdvOpt)
        {
            ShowOptions();
        }

        ShowDialogState();
        RemotesBox.SelectionChanged += (_, _) => Edit(() => _dialog.Remote = RemotesBox.SelectedItem as string ?? "");
        PushToRemoteRadio.IsCheckedChanged += (_, _) => Edit(() => _dialog.ToUrl = PushToUrlRadio.IsChecked == true);
        BranchBox.SelectionChanged += (_, _) => Edit(() => _dialog.Branch = BranchBox.SelectedItem as string ?? "");
        ForceWithLeaseCheck.IsCheckedChanged += (_, _) =>
            Edit(() => _dialog.ForceWithLease = ForceWithLeaseCheck.IsChecked == true);
        ForcePushCheck.IsCheckedChanged += (_, _) => Edit(() => _dialog.Force = ForcePushCheck.IsChecked == true);

        // Upstream binds the tag tab's force box to "Force with lease".
        ForcePushTagsCheck.IsCheckedChanged +=
            (_, _) => Edit(() => _dialog.ForceWithLease = ForcePushTagsCheck.IsChecked == true);
        PushTabs.SelectionChanged += (_, e) =>
        {
            if (e.Source == PushTabs)
            {
                Edit(() => _dialog.Tab = PushTabs.SelectedIndex switch
                {
                    1 => PushTab.Tags,
                    2 => PushTab.MultipleBranches,
                    _ => PushTab.Branches,
                });
            }
        };
        ShowOptionsLink.Click += (_, _) => ShowOptions();
        UnselectAllMenuItem.Click += (_, _) => _dialog.SelectAll(false);
        SelectAllMenuItem.Click += (_, _) => _dialog.SelectAll(true);
        SelectTrackedMenuItem.Click += (_, _) => _dialog.SelectTracked();
        BrowseDestinationButton.Click += (_, _) => UiActions.Run(BrowseDestinationAsync, _ => { });
        PushButton.Click += (_, _) => UiActions.Run(PushAsync, ex => Title = ex.Message);
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Close(null);
            }
        };
    }

    /// <summary>
    ///  The dialog state, for tests.
    /// </summary>
    public PushDialog Dialog => _dialog;

    /// <summary>
    ///  Upstream's "Create pull request after push", read by the caller once the push succeeded.
    /// </summary>
    public bool CreatePullRequestAfterPush => _dialog.CreatePullRequest;

    private void ShowOptions()
    {
        OptionsPanel.IsVisible = true;
        ShowOptionsLink.IsVisible = false;
    }

    private void Edit(Action change)
    {
        if (_showing)
        {
            return;
        }

        KeepTypedValues();
        change();
        ShowDialogState();
    }

    private void KeepTypedValues()
    {
        _dialog.Destination = DestinationBox.Text?.Trim() ?? "";
        _dialog.RemoteBranch = RemoteBranchBox.Text?.Trim() ?? "";
        _dialog.Tag = TagBox.SelectedItem as string ?? "";
        _dialog.ReplaceTrackingReference = ReplaceTrackingCheck.IsChecked == true;
        _dialog.CreatePullRequest = CreatePullRequestCheck.IsChecked == true;
        _dialog.RecursiveSubmodules = Math.Max(0, RecursiveSubmodulesBox.SelectedIndex);
    }

    private void ShowDialogState()
    {
        _showing = true;

        // As upstream, the rows are read when the tab shows and again for another remote.
        if (_dialog.Tab == PushTab.MultipleBranches && _rowsRemote != _dialog.Remote)
        {
            _rowsRemote = _dialog.Remote;
            _dialog.LoadMultipleBranches();
            BranchRowsList.ItemsSource = _dialog.MultipleBranches;
        }

        PushToRemoteRadio.IsChecked = !_dialog.ToUrl;
        PushToUrlRadio.IsChecked = _dialog.ToUrl;
        RemotesBox.SelectedItem = _dialog.Remote;
        RemotesBox.IsEnabled = !_dialog.ToUrl;
        DestinationBox.IsEnabled = BrowseDestinationButton.IsEnabled = _dialog.ToUrl;
        DestinationBox.Text = _dialog.Destination;
        BranchBox.SelectedItem = _dialog.Branch;
        RemoteBranchBox.ItemsSource = _dialog.RemoteBranches;
        RemoteBranchBox.Text = _dialog.RemoteBranch;
        RemoteBranchBox.IsEnabled = _dialog.Branch != PushDialog.AllRefs;
        ForceWithLeaseCheck.IsChecked = ForcePushTagsCheck.IsChecked = _dialog.ForceWithLease;
        ForcePushCheck.IsChecked = _dialog.Force;
        ReplaceTrackingCheck.IsChecked = _dialog.ReplaceTrackingReference;
        CreatePullRequestCheck.IsChecked = _dialog.CreatePullRequest;
        RecursiveSubmodulesBox.SelectedIndex = _dialog.RecursiveSubmodules;
        TagBox.SelectedItem = _dialog.Tag.Length > 0 ? _dialog.Tag : null;
        _showing = false;
    }

    // Upstream's OpenRemotesDialogAndRefreshList.
    private async Task ManageRemotesAsync(string? remote)
    {
        if (_manageRemotes is null)
        {
            return;
        }

        KeepTypedValues();
        _dialog.UpdateRepository(await _manageRemotes(this, remote), remote);
        RemotesBox.ItemsSource = _dialog.Remotes;
        BranchBox.ItemsSource = _dialog.Branches;
        TagBox.ItemsSource = _dialog.Tags;
        _rowsRemote = null;
        ShowDialogState();
    }

    private async Task BrowseDestinationAsync()
    {
        IReadOnlyList<IStorageFolder> folders =
            await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { AllowMultiple = false });
        if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } path)
        {
            DestinationBox.Text = new Uri(path).AbsoluteUri;
        }
    }

    // Upstream's PushChanges, up to running the push.
    private async Task PushAsync()
    {
        KeepTypedValues();

        // Upstream's CheckIfRemoteExist: without a remote, the remotes window is offered first.
        if (_dialog.Remotes.Count == 0 && _manageRemotes is not null && !_dialog.ToUrl)
        {
            MessageBoxWindow configure = new(UpstreamTranslation.ByText(PushDialog.ConfigureRemote),
                UpstreamTranslation.ByText(PushDialog.ErrorPushToRemoteCaption), MessageBoxButtons.YesNo);
            await configure.ShowDialog(this);
            if (configure.Result != DialogResult.Yes)
            {
                return;
            }

            await ManageRemotesAsync(null);
            if (_dialog.Remotes.Count == 0)
            {
                return;
            }
        }

        if (_dialog.ValidationError() is { } error)
        {
            await new MessageBoxWindow(UpstreamTranslation.ByText(error), UpstreamTranslation.ByText(
                    error == PushDialog.ConfigureRemote ? PushDialog.ErrorPushToRemoteCaption : "Error"),
                MessageBoxButtons.OK).ShowDialog(this);
            return;
        }

        if (_dialog.IsNewBranchForRemote &&
            !await ConfirmWindow.AskAsync(this, _preferences, Confirmation.PushNewBranch,
                UpstreamTranslation.ByText(Confirmations.PushNewBranchQuestion), UpstreamTranslation.ByText("Push"),
                UpstreamTranslation.ByText(PushDialog.PushCaption)))
        {
            return;
        }

        _preferences.RecursiveSubmodules = _dialog.RecursiveSubmodules;
        bool track = false;
        if (_dialog.TrackingQuestion is { } question)
        {
            track = true;
            if (_preferences.Asks(Confirmation.AddTrackingRef))
            {
                MessageBoxWindow box = new(UpstreamTranslation.ByText(question),
                    UpstreamTranslation.ByText(PushDialog.PushCaption), MessageBoxButtons.YesNoCancel);
                await box.ShowDialog(this);
                if (box.Result == DialogResult.Cancel)
                {
                    return;
                }

                track = box.Result == DialogResult.Yes;
            }
        }

        if (_dialog.Tab == PushTab.Branches && _dialog.Force)
        {
            MessageBoxWindow box = new(UpstreamTranslation.ByText(PushDialog.UseForceWithLeaseInstead), "Question",
                MessageBoxButtons.YesNoCancel);
            await box.ShowDialog(this);
            if (box.Result == DialogResult.Cancel)
            {
                return;
            }

            if (box.Result == DialogResult.Yes)
            {
                _dialog.ForceWithLease = true;
                ShowDialogState();
            }
        }

        Close(_dialog.Request(track));
    }
}
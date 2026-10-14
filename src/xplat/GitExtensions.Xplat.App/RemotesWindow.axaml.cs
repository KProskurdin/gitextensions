using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using GitCommands;
using GitCommands.Config;
using GitCommands.Remotes;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;
using GitExtensions.Xplat.Ui;
using DialogResult = System.Windows.Forms.DialogResult;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;
using MessageBoxDefaultButton = System.Windows.Forms.MessageBoxDefaultButton;
using UserRepository = GitCommands.UserRepositoryHistory.Repository;

namespace GitExtensions.Xplat.App;

/// <summary>
///  A row of the remotes list: a group's header ("Active", "Inactive") or a remote.
/// </summary>
public sealed record RemoteListRow(string Text, ConfigFileRemote? Remote)
{
    public bool IsHeader => Remote is null;

    public bool IsSelectable => Remote is not null;

    public bool IsInactive => Remote?.Disabled == true;
}

/// <summary>
///  A row of the "Default pull behavior" tab: a local branch, the remote it pulls from and the branch it merges.
/// </summary>
public sealed record PullBehaviorRow(IGitRef Head, string LocalName, string TrackingRemote, string MergeWith);

/// <summary>
///  Upstream's <c>FormRemotes</c> in the new shell: the remotes (active and inactive) with the details of the selected one,
///  and the default pull behavior of the local branches. A saved remote is fetched, as upstream does.
/// </summary>
public partial class RemotesWindow : Window
{
    // WinForms' ColorDialog basic colors, which upstream's "Set color" offers first.
    private static readonly string[] _basicColors =
    [
        "#FF8080", "#FFFF80", "#80FF80", "#00FF80", "#80FFFF", "#0080FF", "#FF80C0", "#FF80FF",
        "#FF0000", "#FFFF00", "#80FF00", "#00FF40", "#00FFFF", "#0080C0", "#8080C0", "#FF00FF",
        "#804040", "#FF8040", "#00FF00", "#008080", "#004080", "#8080FF", "#800040", "#FF0080",
        "#800000", "#FF8000", "#008000", "#008040", "#0000FF", "#0000A0", "#800080", "#8000FF",
        "#400000", "#804000", "#004000", "#004040", "#000080", "#000040", "#400040", "#400080",
        "#000000", "#808000", "#808040", "#808080", "#408080", "#C0C0C0", "#400040", "#FFFFFF",
    ];

    private readonly RemotesDialog _dialog;
    private readonly GitModule _module;
    private readonly IAppPreferences _preferences;
    private readonly Func<string, Task>? _fetchRemote;
    private readonly TaskCompletionSource _loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private IList<UserRepository> _history = [];
    private bool _showing;

    /// <param name="fetchRemote">Fetches a saved remote, as upstream pulls with the fetch action after a save.</param>
    /// <param name="preselectRemote">Upstream's <c>PreselectRemoteOnLoad</c>.</param>
    /// <param name="preselectLocal">Upstream's <c>PreselectLocalOnLoad</c>: opens the pull behavior tab on this branch.</param>
    public RemotesWindow(string repositoryPath, IAppPreferences preferences, Func<string, Task>? fetchRemote = null,
        string? preselectRemote = null, string? preselectLocal = null)
    {
        if (preselectRemote is not null && preselectLocal is not null)
        {
            throw new ArgumentException("Only one option allowed: either preselectRemote or preselectLocal");
        }

        _module = GitModules.Open(repositoryPath);
        _dialog = new RemotesDialog(new ConfigFileRemoteSettingsManager(() => _module), AppSettings.CustomGenericRemoteNames);
        _preferences = preferences;
        _fetchRemote = fetchRemote;
        InitializeComponent();

        ToolTip.SetTip(NewButton, UpstreamTranslation.ByText(RemotesDialog.NewTooltip));
        ToolTip.SetTip(DeleteButton, UpstreamTranslation.ByText(RemotesDialog.DeleteTooltip));
        PrefixLabel.IsVisible = PrefixBox.IsVisible = preferences.AlwaysShowAdvOpt;
        ColorLabel.IsVisible = ColorPanel.IsVisible = preferences.AlwaysShowAdvOpt;
        PuttyPanel.IsVisible = GitSshHelpers.IsPlink;
        foreach (string color in _basicColors)
        {
            Button swatch = new()
            {
                Width = 26, Height = 22, Margin = new Avalonia.Thickness(1), Padding = new Avalonia.Thickness(0),
                Background = Brush.Parse(color),
            };
            swatch.Click += (_, _) => ChooseColor(color);
            ColorSwatches.Children.Add(swatch);
        }

        RemotesList.SelectionChanged += (_, _) => Edit(() =>
        {
            if (RemotesList.SelectedItem is RemoteListRow { Remote: { } remote })
            {
                _dialog.Select(remote);
            }
        });
        NewButton.Click += (_, _) =>
        {
            Edit(() => _dialog.Select(null));
            NameBox.Focus();
        };
        DeleteButton.Click += (_, _) => Run(DeleteAsync);
        ToggleStateButton.Click += (_, _) => Run(async () =>
        {
            await Task.Run(_dialog.ToggleState);
            ShowRemotes();
        });
        SaveButton.Click += (_, _) => Run(SaveAsync);
        NameBox.TextChanged += (_, _) =>
        {
            if (!_showing)
            {
                _dialog.Name = NameBox.Text ?? "";
                SaveButton.IsEnabled = _dialog.CanSave;
            }
        };
        NameBox.GotFocus += (_, _) =>
        {
            KeepTypedValues();
            if (_dialog.NameFromUrl() is { } owner)
            {
                NameBox.Text = owner;
                NameBox.SelectAll();
            }
        };
        UrlBox.GotFocus += (_, _) => ProposeUrls(UrlBox, pushUrl: false);
        PushUrlBox.GotFocus += (_, _) => ProposeUrls(PushUrlBox, pushUrl: true);
        PrefixBox.LostFocus += (_, _) =>
            PrefixBox.Text = BranchNames.NormalisePrefix(PrefixBox.Text ?? "", _preferences.AutoNormaliseSymbol);
        SeparatePushUrlCheck.IsCheckedChanged += (_, _) => Edit(() =>
            _dialog.SeparatePushUrl = SeparatePushUrlCheck.IsChecked == true);
        ColorResetButton.Click += (_, _) => ChooseColor(null);
        ColorHtmlBox.KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Enter && Color.TryParse(ColorHtmlBox.Text, out Color color))
            {
                ChooseColor($"#{color.R:X2}{color.G:X2}{color.B:X2}");
            }
        };
        UrlBrowseButton.Click += (_, _) => Run(() => BrowseFolderAsync(UrlBox));
        PushUrlBrowseButton.Click += (_, _) => Run(() => BrowseFolderAsync(PushUrlBox));
        PuttyKeyBrowseButton.Click += (_, _) => Run(BrowsePuttyKeyAsync);

        BranchesList.SelectionChanged += (_, _) => ShowSelectedBranch();
        RemoteRepositoryBox.LostFocus += (_, _) => Run(() => SetOnSelectedHeadAsync(head =>
            head.TrackingRemote = RemoteRepositoryBox.SelectedItem as string ?? ""));
        MergeWithBox.LostFocus += (_, _) => Run(() => SetOnSelectedHeadAsync(head =>
            head.MergeWith = MergeWithBox.Text ?? ""));
        MergeWithBox.DropDownOpened += (_, _) => Run(FillMergeWithChoicesAsync);
        SavePullBehaviorButton.Click += (_, _) => Run(() => LoadPullBehaviorAsync(SelectedHead?.LocalName));

        Opened += (_, _) => Run(() => LoadAsync(preselectRemote, preselectLocal));
        KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Escape)
            {
                Close();
            }
        };
    }

    /// <summary>
    ///  The dialog state, for tests.
    /// </summary>
    public RemotesDialog Dialog => _dialog;

    /// <summary>
    ///  Completes when the remotes and branches shown first are read, for tests.
    /// </summary>
    public Task Loaded => _loaded.Task;

    private IGitRef? SelectedHead => (BranchesList.SelectedItem as PullBehaviorRow)?.Head;

    private async Task LoadAsync(string? preselectRemote, string? preselectLocal)
    {
        _history = await AppServices.RemoteUrls.LoadAsync();
        await Task.Run(() => _dialog.Load(preselectRemote));
        ShowRemotes();
        await LoadPullBehaviorAsync(preselectLocal);
        if (preselectLocal is not null && _dialog.Remotes.Count > 0)
        {
            Tabs.SelectedItem = PullBehaviorTab;
        }

        _loaded.TrySetResult();
    }

    private void ShowRemotes()
    {
        _showing = true;
        List<RemoteListRow> rows = [];
        AddGroup(RemotesDialog.ActiveGroup, _dialog.ActiveRemotes);
        AddGroup(RemotesDialog.InactiveGroup, _dialog.InactiveRemotes);
        RemotesList.ItemsSource = rows;
        RemotesList.SelectedItem = rows.FirstOrDefault(row => row.Remote is not null && row.Remote == _dialog.Selected);
        UrlBox.ItemsSource = _history.Select(entry => entry.Path).ToList();
        PushUrlBox.ItemsSource = _history.Select(entry => entry.Path).ToList();
        _showing = false;
        ShowDetails();

        void AddGroup(string header, IEnumerable<ConfigFileRemote> remotes)
        {
            List<ConfigFileRemote> group = [.. remotes];
            if (group.Count > 0)
            {
                rows.Add(new RemoteListRow(UpstreamTranslation.ByText(header), null));
                rows.AddRange(group.Select(remote => new RemoteListRow(remote.Name ?? "", remote)));
            }
        }
    }

    private void ShowDetails()
    {
        _showing = true;
        DetailsGroup.Header = UpstreamTranslation.ByText(_dialog.Header);
        UrlLabel.Text = UpstreamTranslation.ByText(_dialog.UrlLabel).Replace('&', '_');
        NameBox.Text = _dialog.Name;
        UrlBox.Text = _dialog.Url;
        PushUrlBox.Text = _dialog.PushUrl;
        SeparatePushUrlCheck.IsChecked = _dialog.SeparatePushUrl;
        PushUrlLabel.IsVisible = PushUrlBox.IsVisible = PushUrlBrowseButton.IsVisible = _dialog.SeparatePushUrl;
        PuttyKeyBox.Text = _dialog.PuttySshKey;
        PrefixBox.Text = _dialog.Prefix;
        ShowColor();
        DeleteButton.IsEnabled = ToggleStateButton.IsEnabled = _dialog.CanDelete;
        bool disabled = _dialog.Selected?.Disabled == true;
        ToggleStateImage.Source = UpstreamImages.Named(disabled ? "EyeOpened" : "EyeClosed");
        ToolTip.SetTip(ToggleStateButton, UpstreamTranslation.ByText(
            disabled ? RemotesDialog.ActivateTooltip : RemotesDialog.DeactivateTooltip).Trim());

        // As upstream, an inactive remote's details are shown but cannot be edited.
        DetailsGrid.IsEnabled = PuttyPanel.IsEnabled = _dialog.IsEditable;
        SaveButton.IsEnabled = _dialog.IsEditable && _dialog.CanSave;
        _showing = false;
    }

    private void ShowColor()
    {
        ColorButton.Background = _dialog.Color is { } html && Color.TryParse(html, out Color color)
            ? new SolidColorBrush(color)
            : null;
        ColorButton.Content = _dialog.Color is null ? UpstreamTranslation.ByText("Set &color").Replace('&', '_') : "";
        ColorResetButton.IsVisible = _dialog.Color is not null;
        ColorHtmlBox.Text = _dialog.Color ?? "";
    }

    private void ChooseColor(string? html)
    {
        _dialog.Color = html;
        ColorButton.Flyout?.Hide();
        ShowColor();
    }

    private void Edit(Action change)
    {
        if (_showing)
        {
            return;
        }

        KeepTypedValues();
        change();
        ShowDetails();
    }

    private void KeepTypedValues()
    {
        _dialog.Name = NameBox.Text ?? "";
        _dialog.Url = UrlBox.Text ?? "";
        _dialog.PushUrl = PushUrlBox.Text ?? "";
        _dialog.PuttySshKey = PuttyKeyBox.Text ?? "";
        _dialog.Prefix = PrefixBox.Text ?? "";
    }

    // Upstream's FillWithSomeGeneratedRemoteUrls, when a URL box gets the focus.
    private void ProposeUrls(ComboBox box, bool pushUrl)
    {
        KeepTypedValues();
        IReadOnlyList<string> proposed = _dialog.ProposedUrls(pushUrl, box.Text ?? "",
            [.. _history.Select(entry => entry.Path)], out bool clearText);
        if (proposed.Count == 0)
        {
            return;
        }

        string text = box.Text ?? "";
        box.ItemsSource = proposed;
        box.Text = clearText ? "" : text;
    }

    private async Task SaveAsync()
    {
        KeepTypedValues();
        ConfigFileRemote? before = _dialog.Selected;
        string url = _dialog.Url.Trim();
        string pushUrl = _dialog.PushUrl.Trim();
        RemoteSaveOutcome outcome = await Task.Run(_dialog.Save);
        try
        {
            if (outcome.Message is { } message)
            {
                await new MessageBoxWindow(message, UpstreamTranslation.ByText(RemotesDialog.GitMessage),
                    MessageBoxButtons.OK).ShowDialog(this);
                return;
            }

            if (outcome.SavedName is not { } name)
            {
                return;
            }

            _dialog.UpdateUrlHistory(_history, before, url, _dialog.SeparatePushUrl ? pushUrl : null);
            await AppServices.RemoteUrls.SaveAsync(_history);
            if (_fetchRemote is not null)
            {
                await _fetchRemote(name);
            }
        }
        finally
        {
            // As upstream, the list is read again whatever happened, with the remote typed selected.
            string selected = _dialog.Name.Trim();
            await Task.Run(() => _dialog.Load(selected));
            ShowRemotes();
            await LoadPullBehaviorAsync(SelectedHead?.LocalName);
        }
    }

    private async Task DeleteAsync()
    {
        if (_dialog.Selected is null)
        {
            return;
        }

        MessageBoxWindow question = new(UpstreamTranslation.ByText(RemotesDialog.QuestionDeleteRemote),
            UpstreamTranslation.ByText(RemotesDialog.QuestionDeleteRemoteCaption), MessageBoxButtons.YesNo,
            MessageBoxDefaultButton.Button2);
        await question.ShowDialog(this);
        if (question.Result != DialogResult.Yes)
        {
            return;
        }

        string output = await Task.Run(_dialog.Delete);
        if (!string.IsNullOrEmpty(output))
        {
            await new MessageBoxWindow(output, UpstreamTranslation.ByText(RemotesDialog.GitMessage), MessageBoxButtons.OK)
                .ShowDialog(this);
        }

        await Task.Run(() => _dialog.Load());
        ShowRemotes();
        await LoadPullBehaviorAsync(SelectedHead?.LocalName);
    }

    private async Task BrowseFolderAsync(ComboBox box)
    {
        IReadOnlyList<IStorageFolder> folders =
            await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { AllowMultiple = false });
        if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } path)
        {
            box.Text = path;
        }
    }

    private async Task BrowsePuttyKeyAsync()
    {
        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = UpstreamTranslation.ByText(RemotesDialog.SshKeyOpenCaption),
            FileTypeFilter = [new FilePickerFileType(UpstreamTranslation.ByText(RemotesDialog.SshKeyOpenFilter))
            {
                Patterns = ["*.ppk"],
            }],
        });
        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path)
        {
            PuttyKeyBox.Text = path;
        }
    }

    // Upstream's InitialiseTabDefaultPullBehaviors: the local branches by name, with their remote and merge branch.
    private async Task LoadPullBehaviorAsync(string? preselectLocal)
    {
        List<PullBehaviorRow> rows = await Task.Run(() => _module.GetRefs(RefsFilter.Heads)
            .OrderBy(head => head.LocalName)
            .Select(head => new PullBehaviorRow(head, head.LocalName, head.TrackingRemote, head.MergeWith))
            .ToList());
        RemoteRepositoryBox.ItemsSource = new[] { "" }.Concat(_dialog.Remotes.Select(remote => remote.Name ?? ""))
            .ToList();
        BranchesList.ItemsSource = rows;
        BranchesList.SelectedItem = rows.FirstOrDefault(row => row.LocalName == preselectLocal) ?? rows.FirstOrDefault();
    }

    private void ShowSelectedBranch()
    {
        if (BranchesList.SelectedItem is not PullBehaviorRow row)
        {
            return;
        }

        LocalBranchBox.Text = row.Head.Name;
        RemoteRepositoryBox.SelectedItem = _dialog.Remotes
            .FirstOrDefault(remote => StringComparer.OrdinalIgnoreCase.Equals(remote.Name, row.TrackingRemote))?.Name ?? "";
        MergeWithBox.Text = row.MergeWith;
    }

    // As upstream, which writes a branch's remote and merge branch when their box is left; Save only reads them again.
    private async Task SetOnSelectedHeadAsync(Action<IGitRef> set)
    {
        if (SelectedHead is { } head)
        {
            await Task.Run(() => set(head));
        }
    }

    private async Task FillMergeWithChoicesAsync()
    {
        if (SelectedHead is not { } head)
        {
            return;
        }

        string remote = RemoteRepositoryBox.SelectedItem as string ?? "";
        string text = MergeWithBox.Text ?? "";
        IReadOnlyList<string> choices = await Task.Run(() => RemotesDialog.MergeWithChoices(head, remote,
            remote.Length == 0 ? null : _module.GetSetting(string.Format(SettingKeyString.RemoteUrl, remote)),
            _module.GetRefs(RefsFilter.Remotes)));
        MergeWithBox.ItemsSource = choices;
        MergeWithBox.Text = text;
    }

    private void Run(Func<Task> action)
        => UiActions.Run(action, ex => Title = ex.Message);
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.VisualTree;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Plugins;
using GitExtensions.Xplat.App.RepositoryHosts;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Settings;
using GitExtensions.Xplat.Ui;
using GitUIPluginInterfaces.RepositoryHosts;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>BlameControl</c>: for each line of a file as of a commit, the commit and author that last changed it, with
///  upstream's blame settings in the menu; bound to <see cref="BlameViewModel"/>. Shared by the blame window and the file
///  history's Blame tab.
/// </summary>
public partial class BlameView : UserControl
{
    /// <summary>
    ///  The blame's state and commands. A view model that owns the view's (a window's) binds it here; without one the view
    ///  makes its own.
    /// </summary>
    public static readonly StyledProperty<BlameViewModel?> ViewModelProperty =
        AvaloniaProperty.Register<BlameView, BlameViewModel?>(nameof(ViewModel));

    private const string SameCommitClass = "sameCommit";

    private readonly List<object> _hostMenuItems = [];
    private BlameViewModel _viewModel;

    // Upstream's _highlightedCommit: the short hash of the commit under the mouse.
    private string? _highlightedHash;

    public BlameView()
    {
        InitializeComponent();
        _viewModel = NewViewModel();
        Attach(_viewModel);
        BlameList.SelectionChanged += (_, _) => ViewModel.SetSelection(BlameList.SelectedIndex);
        BlameList.PointerMoved += (_, e) => HighlightCommit(
            ((e.Source as Visual)?.FindAncestorOfType<ListBoxItem>(includeSelf: true)?.DataContext as BlameRow)?.Line.Hash);
        BlameList.ContainerPrepared += (_, e) => ShowHighlight(e.Container);
        BlameList.DoubleTapped += (_, _) => ViewModel.BlameRevisionCommand.Execute(null);
        BlameList.KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Enter)
            {
                e.Handled = true;
                ViewModel.BlameRevisionCommand.Execute(null);
            }
            else if (RunFindHotkey(e))
            {
                e.Handled = true;
            }
        };
        BlameContextMenu.Opening += (_, _) =>
        {
            ViewModel.SetSelection(BlameList.SelectedIndex);
            ViewModel.RefreshMenu();
            ShowRepositoryHostItems();
        };
    }

    /// <summary>
    ///  A line's commit was asked for (double-click, Enter or the menu's blame items); lines not committed yet ask nothing.
    /// </summary>
    public event EventHandler<BlameCommitRequest>? CommitRequested;

    /// <summary>
    ///  The blame settings changed, so a view that shows them too can show the new ones.
    /// </summary>
    public event EventHandler? OptionsChanged;

    /// <summary>
    ///  A loading or option error, for the view's window to show.
    /// </summary>
    public event EventHandler<string>? ErrorOccurred;

    /// <inheritdoc cref="ViewModelProperty"/>
    public BlameViewModel ViewModel
    {
        get => _viewModel;
        set => SetValue(ViewModelProperty, value);
    }

    /// <summary>
    ///  The repository host plugin of the repository, whose items the menu shows, as upstream's blame menu shows the
    ///  GitHub plugin's "View in GitHub".
    /// </summary>
    public Func<IRepositoryHostPlugin?>? RepositoryHost { get; set; }

    /// <inheritdoc cref="BlameViewModel.GridRevision"/>
    public Func<string, CommitRow?>? GridRevision
    {
        get => ViewModel.GridRevision;
        set => ViewModel.GridRevision = value;
    }

    /// <inheritdoc cref="BlameViewModel.GridChildren"/>
    public Func<string, IReadOnlyList<string>>? GridChildren
    {
        get => ViewModel.GridChildren;
        set => ViewModel.GridChildren = value;
    }

    /// <summary>
    ///  The commit information above the lines, whose links the host may handle.
    /// </summary>
    public CommitInfoView Info => CommitInfo;

    /// <summary>
    ///  The lines shown, for tests.
    /// </summary>
    public IReadOnlyList<BlameLine> Lines => ViewModel.Lines;

    /// <inheritdoc cref="BlameViewModel.CurrentFileLine"/>
    public int? CurrentFileLine
    {
        get
        {
            ViewModel.SetSelection(BlameList.SelectedIndex);
            return ViewModel.CurrentFileLine;
        }
    }

    /// <summary>
    ///  A blame view model with the app's preferences, git and translations, for a view model that owns a blame view.
    /// </summary>
    public static BlameViewModel NewViewModel()
    {
        IAppPreferences preferences = AppServices.Preferences;
        return new BlameViewModel(
            new GitCommitHistory(() => new CommitDateStyle(preferences.RelativeDate, preferences.ShowAuthorDate),
                links: AppServices.RevisionLinks),
            preferences,
            text => UpstreamTranslation.WithAccessKey(UpstreamTranslation.ByText(text)));
    }

    /// <inheritdoc cref="BlameViewModel.GoToLineWhenBlamed"/>
    public void GoToLineWhenBlamed(int? line) => ViewModel.GoToLineWhenBlamed(line);

    /// <inheritdoc cref="BlameViewModel.LoadAsync"/>
    public Task LoadAsync(string repositoryPath, string hash, string filePath)
    {
        ViewModel.SetSelection(BlameList.SelectedIndex);
        return ViewModel.LoadAsync(repositoryPath, hash, filePath);
    }

    /// <inheritdoc cref="BlameViewModel.ChangeOptionsAsync"/>
    public Task ChangeOptionsAsync(BlameOptions options)
    {
        ViewModel.SetSelection(BlameList.SelectedIndex);
        return ViewModel.ChangeOptionsAsync(options);
    }

    /// <summary>
    ///  Upstream's <c>HideCommitInfo</c>, for a host that shows the commit's information itself.
    /// </summary>
    public void HideCommitInfo() => ViewModel.HideCommitInfo();

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != ViewModelProperty || change.NewValue is not BlameViewModel viewModel
                                                 || viewModel == _viewModel)
        {
            return;
        }

        Detach(_viewModel);
        _viewModel = viewModel;
        Attach(viewModel);
    }

    // What the view does for its view model: the clipboard, the changes window, selecting rows, the commit information and
    // the events it passes on.
    private void Attach(BlameViewModel viewModel)
    {
        viewModel.SetClipboard = text => TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard
            ? clipboard.SetTextAsync(text)
            : Task.CompletedTask;
        viewModel.ShowCommitChanges = (repositoryPath, hash) =>
        {
            if (TopLevel.GetTopLevel(this) is Window owner)
            {
                new CommitDiffWindow(repositoryPath, hash).Show(owner);
            }
        };
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        viewModel.SelectLineRequested += OnSelectLineRequested;
        viewModel.CommitRequested += OnCommitRequested;
        viewModel.OptionsChanged += OnOptionsChanged;
        viewModel.ErrorOccurred += OnErrorOccurred;
        Layout.DataContext = viewModel;

        // A context menu gets its data context only when it opens (MVVM.md).
        BlameContextMenu.DataContext = viewModel;
        ShowCommitInfoArea();
    }

    private void Detach(BlameViewModel viewModel)
    {
        viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        viewModel.SelectLineRequested -= OnSelectLineRequested;
        viewModel.CommitRequested -= OnCommitRequested;
        viewModel.OptionsChanged -= OnOptionsChanged;
        viewModel.ErrorOccurred -= OnErrorOccurred;
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(BlameViewModel.Info):
                CommitInfo.RepositoryPath = ViewModel.RepositoryPath;
                CommitInfo.Show(ViewModel.Info.Details, ViewModel.Info.Error, ViewModel.Info.Children ?? []);
                break;

            // A new list of rows drops the highlight and the find's last occurrence.
            case nameof(BlameViewModel.Rows):
                _highlightedHash = null;
                _found = null;
                break;
            case nameof(BlameViewModel.IsCommitInfoVisible):
                ShowCommitInfoArea();
                break;
        }
    }

    private void OnSelectLineRequested(object? sender, int index)
    {
        BlameList.SelectedIndex = index;
        BlameList.ScrollIntoView(index);
    }

    private void OnCommitRequested(object? sender, BlameCommitRequest request) => CommitRequested?.Invoke(this, request);

    private void OnOptionsChanged(object? sender, EventArgs e) => OptionsChanged?.Invoke(this, e);

    private void OnErrorOccurred(object? sender, string message) => ErrorOccurred?.Invoke(this, message);

    // The rows of the commit information and its splitter take no room while it is hidden.
    private void ShowCommitInfoArea()
    {
        if (!ViewModel.IsCommitInfoVisible)
        {
            Layout.RowDefinitions[0].Height = new GridLength(0);
            Layout.RowDefinitions[1].Height = new GridLength(0);
        }
    }

    private void HighlightCommit(string? hash)
    {
        if (hash != _highlightedHash)
        {
            _highlightedHash = hash;
            foreach (Control container in BlameList.GetRealizedContainers())
            {
                ShowHighlight(container);
            }
        }
    }

    private void ShowHighlight(Control container)
        => container.Classes.Set(SameCommitClass, _highlightedHash is not null
            && BlameList.ItemFromContainer(container) is BlameRow row && row.Line.Hash == _highlightedHash);

    // Upstream's ConfigureRepositoryHostPlugin and the menu's tag: the host plugin's items for the line under the menu, after
    // the blame settings. A line not committed yet has no commit to show.
    private void ShowRepositoryHostItems()
    {
        foreach (object item in _hostMenuItems)
        {
            BlameContextMenu.Items.Remove(item);
        }

        _hostMenuItems.Clear();
        if (RepositoryHost?.Invoke() is not { } host
            || ViewModel.SelectedCommitLine?.Commit?.ObjectId is not { } blameId)
        {
            return;
        }

        int lineIndex = BlameList.SelectedIndex;
        IReadOnlyList<MenuItem> items =
            RepositoryHostMenus.ForBlame(host, new GitBlameContext(ViewModel.FilePath, lineIndex, lineIndex, blameId));
        if (items.Count > 0)
        {
            _hostMenuItems.Add(new Separator());
            _hostMenuItems.AddRange(items);
            foreach (object item in _hostMenuItems)
            {
                BlameContextMenu.Items.Add(item);
            }
        }
    }
}

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using GitCommands;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Platform;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormResolveConflicts</c> in the new shell, bound to <see cref="ConflictsViewModel"/> (rules in
///  <see cref="ConflictsDialog"/>): the unresolved files, merged with the configured merge tool, resolved by choosing a
///  side, deleted or kept when one side deleted them, and opened or saved per side; Rescan and Reset; once the last conflict
///  is resolved it offers to commit and closes.
/// </summary>
public partial class ConflictsWindow : Window
{
    private const string HelpSubfolder = "modify_history";
    private const string HelpAnchor = "handle-merge-conflicts";

    /// <summary>
    ///  The design-time constructor (<see cref="DesignTime"/>): the window's XAML only.
    /// </summary>
    [Obsolete(DesignTime.ConstructorOnly)]
#pragma warning disable CS8618 // The fields the window's data fills stay unset: no code of the window runs.
    public ConflictsWindow() => InitializeComponent();
#pragma warning restore CS8618

    /// <param name="showFileHistory">Opens the file history window on a file, over this one.</param>
    /// <param name="showCommit">Opens the commit window, as upstream offers once every conflict is resolved.</param>
    /// <param name="updateSubmodules">Upstream's <c>UpdateSubmodules</c> once the conflicts are resolved.</param>
    /// <param name="offerCommit">Upstream's <c>offerCommit</c>: false when the commit window opened this one.</param>
    /// <param name="checkoutSubmoduleBranch">The submodule conflict window's "Checkout Branch": upstream's
    ///  <c>StartCheckoutBranch</c> in a submodule's folder, limited to the branches that contain the given commits.</param>
    public ConflictsWindow(string repositoryPath, RepositoryViewModel repository, RepositoryOperationsViewModel actions,
        IAppPreferences preferences, Action<Window, string> showFileHistory, Action<Window>? showCommit = null,
        Func<string, Task<bool>>? updateSubmodules = null, bool offerCommit = true,
        Func<Window, string, IReadOnlyList<ObjectId>, Task<bool>>? checkoutSubmoduleBranch = null)
    {
        ViewModel = new ConflictsViewModel(new WindowDialogHost(this), GitModules.Open(repositoryPath), repositoryPath,
            actions, new ConflictsViewModelServices
            {
                RefreshRepository = () => repository.RefreshAsync(repositoryPath),
                ConfirmSuppressible = (confirmation, text, caption)
                    => ConfirmWindow.AskAsync(this, preferences, confirmation, text, "Yes", caption),
                ShowTaskDialog = async page =>
                {
                    MessageBoxWindow dialog = new(page);
                    await dialog.ShowDialog(this);
                    return dialog.ChosenButton;
                },
                MergeSubmodule = fileName => new MergeSubmoduleWindow(repositoryPath, fileName, checkoutSubmoduleBranch)
                    .ShowDialog<bool>(this),
                PickSaveFile = PickSaveFileAsync,
                LaunchFile = LaunchAsync,
                FileManager = new SystemFileManager(new SystemProcessLauncher(), HostPlatform.Current),
                ShowFileHistory = fileName => showFileHistory(this, fileName),
                ShowCommit = showCommit is null ? null : () => showCommit(Owner as Window ?? this),
                UpdateSubmodules = updateSubmodules,
                OfferCommit = offerCommit,
                AvailableMergeTools = () => AppServices.DiffMergeTools.GetAvailableAsync(diff: false),
            });
        DataContext = ViewModel;
        InitializeComponent();
        WindowPlacementTracker.Attach(this, "Xplat.ConflictsWindow");

        // A context menu gets its data context only when it opens (MVVM.md, Context menus).
        ConflictMenu.DataContext = ViewModel;
        ConflictList.SelectionChanged += (_, _) =>
            ViewModel.SelectedRows = [.. ConflictList.SelectedItems?.OfType<ConflictRow>() ?? []];
        ConflictList.DoubleTapped += (_, _) => ViewModel.MergeCommand.Execute(null);
        ConflictMenu.Opening += (_, e) => e.Cancel = ViewModel.SelectedRows.Count == 0;
        HelpLink.CommandParameter = GitUI.UserManual.UserManual.UrlFor(HelpSubfolder, HelpAnchor);
        KeyDown += OnWindowKeyDown;
        Opened += (_, _) => _ = LoadAsync();
    }

    public ConflictsViewModel ViewModel { get; }

    /// <summary>
    ///  The unresolved files on show.
    /// </summary>
    public IReadOnlyList<ConflictData> Conflicts => ViewModel.Conflicts;

    /// <summary>
    ///  Completes when the merge tool is read and the files are listed, for tests.
    /// </summary>
    public Task ConflictsLoaded => ViewModel.Loaded;

    private async Task LoadAsync()
    {
        await ViewModel.LoadAsync();
        MergeButton.Focus();
    }

    private async Task<string?> PickSaveFileAsync(string suggestedName, string folderPath)
    {
        IStorageFolder? folder = await StorageProvider.TryGetFolderFromPathAsync(folderPath);
        IStorageFile? target = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName = suggestedName,
            DefaultExtension = Path.GetExtension(suggestedName),
            SuggestedStartLocation = folder,
        });
        return target?.TryGetLocalPath();
    }

    private async Task LaunchAsync(string path)
    {
        if (GetTopLevel(this)?.Launcher is { } launcher)
        {
            await launcher.LaunchFileInfoAsync(new FileInfo(path));
        }
    }

    // Upstream's hotkeys (FormMergeConflicts) and the Choose items' Ctrl+1, Ctrl+2 and Ctrl+3.
    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        Action? action = Hotkeys.Conflicts.Match(e) switch
        {
            ConflictsCommand.Merge => () => ViewModel.MergeCommand.Execute(null),
            ConflictsCommand.Rescan => () => ViewModel.RescanCommand.Execute(null),
            ConflictsCommand.ChooseLocal => () => ViewModel.ChooseCommand.Execute(ConflictsDialog.Local),
            ConflictsCommand.ChooseRemote => () => ViewModel.ChooseCommand.Execute(ConflictsDialog.Remote),
            ConflictsCommand.ChooseBase => () => ViewModel.ChooseCommand.Execute(ConflictsDialog.Base),
            _ => null,
        };
        if (e.KeyModifiers == Hotkeys.CommandModifier)
        {
            action = e.Key switch
            {
                Key.D1 => () => ViewModel.ChooseCommand.Execute(ConflictsDialog.Local),
                Key.D2 => () => ViewModel.ChooseCommand.Execute(ConflictsDialog.Remote),
                Key.D3 => () => ViewModel.ChooseCommand.Execute(ConflictsDialog.Base),
                _ => action,
            };
        }
        else if (e.Key == Key.Enter && ConflictList.IsKeyboardFocusWithin)
        {
            action = () => ViewModel.MergeCommand.Execute(null);
        }

        if (action is not null)
        {
            e.Handled = true;
            action();
        }
    }
}

using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using GitCommands;
using GitCommands.Logging;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormGitCommandLog</c>, bound to <see cref="CommandLogViewModel"/>: the git commands the app ran
///  (upstream's <see cref="CommandLog"/>, which the shared engine fills for every git call), oldest first with the newest
///  followed, and upstream's cache of git output (<see cref="GitModule.GitCommandCache"/>), each with the selected entry's
///  detail; one window at a time.
/// </summary>
public partial class CommandLogWindow : Window
{
    private static CommandLogWindow? _instance;

    public CommandLogWindow()
    {
        ViewModel = new CommandLogViewModel(new WindowDialogHost(this), GitModule.GitCommandCache,
            AppSettings.LogCaptureCallStacks, value => AppSettings.LogCaptureCallStacks = value,
            action => Dispatcher.UIThread.Post(action, DispatcherPriority.Background))
        {
            PickSaveFile = PickSaveFileAsync,
            CopyText = text => Clipboard?.SetTextAsync(text) ?? Task.CompletedTask,
        };
        DataContext = ViewModel;
        InitializeComponent();
        UpstreamTranslation.Apply(this, CommandLogViewModel.Category);
        WindowPlacementTracker.Attach(this, "Xplat.CommandLogWindow");
        InitialFocus.Attach(this, () => LogItems);

        // View concerns: the menus' data context (a context menu gets it only when it opens), word wrap, the newest entry
        // scrolled into view and upstream's shortcuts, which WinForms runs while the tab page has the focus.
        contextMenu.DataContext = ViewModel;
        cmsCache.DataContext = ViewModel;
        ApplyWordWrap();
        ViewModel.PropertyChanged += OnViewModelChanged;
        tabPageCommandLog.KeyDown += OnCommandLogKeyDown;
        tabPageCommandCache.KeyDown += OnCommandCacheKeyDown;
        Opened += (_, _) => ViewModel.Attach();
        Closed += (_, _) => ViewModel.Detach();
    }

    public CommandLogViewModel ViewModel { get; }

    /// <summary>
    ///  The commands on show, oldest first.
    /// </summary>
    public IReadOnlyList<CommandLogEntry> Entries => ViewModel.Entries;

    /// <summary>
    ///  The cached commands on show.
    /// </summary>
    public IReadOnlyList<string> CachedCommands => ViewModel.CachedCommands;

    /// <summary>
    ///  Upstream's <c>ShowOrActivate</c>: shows the log, or restores or activates the open window.
    /// </summary>
    public static void ShowFor(Window owner)
    {
        if (_instance is { } open)
        {
            if (open.WindowState == WindowState.Minimized)
            {
                open.WindowState = WindowState.Normal;
            }
            else
            {
                open.Activate();
            }

            return;
        }

        _instance = new CommandLogWindow();
        _instance.Closed += (_, _) => _instance = null;
        _instance.Show(owner);
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(CommandLogViewModel.WordWrap):
                ApplyWordWrap();
                break;
            case nameof(CommandLogViewModel.SelectedLogIndex) when ViewModel.SelectedLogIndex >= 0:
                LogItems.ScrollIntoView(ViewModel.SelectedLogIndex);
                break;
            case nameof(CommandLogViewModel.SelectedCacheIndex) when ViewModel.SelectedCacheIndex >= 0:
                CommandCacheItems.ScrollIntoView(ViewModel.SelectedCacheIndex);
                break;
        }
    }

    private void ApplyWordWrap()
    {
        TextWrapping wrapping = ViewModel.WordWrap ? TextWrapping.Wrap : TextWrapping.NoWrap;
        LogOutput.TextWrapping = wrapping;
        commandCacheOutput.TextWrapping = wrapping;
    }

    // The detail box keeps its own copy.
    private void OnCommandLogKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || e.KeyModifiers != Hotkeys.CommandModifier)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.S:
                ViewModel.SaveToFileCommand.Execute(null);
                break;
            case Key.C:
                ViewModel.CopyCommandLineCommand.Execute(null);
                break;
            case Key.L:
                ViewModel.ClearCommand.Execute(null);
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    private void OnCommandCacheKeyDown(object? sender, KeyEventArgs e)
    {
        if (!e.Handled && e.KeyModifiers == Hotkeys.CommandModifier && e.Key == Key.L)
        {
            ViewModel.ClearCacheCommand.Execute(null);
            e.Handled = true;
        }
    }

    // Upstream's mnuSaveToFile_Click: a text or CSV file.
    private async Task<string?> PickSaveFileAsync()
    {
        IStorageFile? file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = Title,
            DefaultExtension = CommandLogDialog.TextExtension,
            FileTypeChoices =
            [
                new FilePickerFileType(CommandLogDialog.TextFiles) { Patterns = ["*.txt"] },
                new FilePickerFileType(CommandLogDialog.CsvFiles) { Patterns = ["*.csv"] },
                new FilePickerFileType(CommandLogDialog.AllFiles) { Patterns = ["*.*"] },
            ],
        });
        return file?.TryGetLocalPath();
    }
}

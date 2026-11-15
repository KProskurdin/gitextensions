using System.Runtime.Versioning;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using GitCommands;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Diff;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Platform;
using GitExtensions.Xplat.Core.Presentation;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Scripts;
using GitExtensions.Xplat.Ui;
using GitUI.ScriptsEngine;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream FileStatusList's item menu (<c>FileStatusList.ContextMenu.cs</c>) for a list of the files between two
///  revisions: the browse window's Diff tab and FormDiff. Its state and actions are <see cref="FileListMenuViewModel"/>'s
///  (rules in core <see cref="DiffFileMenu"/>); the view keeps the bold default items, the custom difftools, the scripts,
///  the hotkeys and the windows the actions open (its <see cref="IFileListMenuHost"/>). What a window adds is its
///  <see cref="FileStatusListMenuBinding"/>, as upstream's <c>BindContextMenu</c>.
/// </summary>
public partial class FileStatusListMenu : ContextMenu
{
    // Upstream FileStatusList texts.
    private const string SaveFileFilterCurrentFormat = "Current format";
    private const string SaveFileFilterAllFiles = "All files";
    private const string NewNameText = "New name";

    // Upstream FileStatusList's tooltips of "Skip worktree" and "Assume unchanged".
    private const string SkipWorktreeToolTip = "Hide already tracked files that will change but that you don't want to commit."
                                               + "\nSuitable for some config files modified locally.";
    private const string AssumeUnchangedToolTip = "Tell git to not check the status of this file for performance benefits."
                                                  + "\nUse this feature when a file is big and never change."
                                                  + "\nGit will never check if the file has changed that will improve status check performance.";

    private static readonly IFileManager FileManager =
        new SystemFileManager(new SystemProcessLauncher(), HostPlatform.Current);

    private FileStatusListMenuBinding? _binding;
    private bool _customDiffToolsLoaded;

    public FileStatusListMenu()
    {
        InitializeComponent();
        UpstreamTranslation.Apply(this, "FileStatusList");

        // After the translation pass, so the bound texts are the view model's (MVVM.md, Translation).
        ViewModel = new FileListMenuViewModel(AppServices.Preferences, OperatingSystem.IsWindows(),
            () => OperatingSystem.IsWindows() && VisualStudio.IsInstalled, UpstreamTranslation.WithAccessKey);
        DataContext = ViewModel;

        // Upstream FileStatusList's OnRuntimeLoad starts looking for Visual Studio.
        if (OperatingSystem.IsWindows())
        {
            _ = VisualStudio.InitAsync();
        }

        Opening += (_, _) =>
        {
            Refresh();
            if (!_customDiffToolsLoaded)
            {
                _customDiffToolsLoaded = true;
                Run(LoadCustomDifftoolsAsync);
            }
        };
        ToolTip.SetTip(tsmiSkipWorktree, UpstreamTranslation.ByText(SkipWorktreeToolTip));
        ToolTip.SetTip(tsmiAssumeUnchanged, UpstreamTranslation.ByText(AssumeUnchangedToolTip));

        // Upstream's ResetFile_Click: the parent item resets to the first revision when it can; its sub-items' clicks reach
        // it too, so it has no command.
        tsmiResetFileTo.Click += (_, e) =>
        {
            if (e.Source == tsmiResetFileTo && ViewModel.IsResetToFirstVisible)
            {
                ViewModel.ResetToParentCommand.Execute(null);
            }
        };

        // Upstream's OpenFilesWithDiffTool: the parent item runs First -> Second, as above.
        tsmiOpenWithDifftool.Click += (_, e) =>
        {
            if (e.Source == tsmiOpenWithDifftool)
            {
                Run(() => ViewModel.OpenWithDifftoolAsync(RevisionDiffKind.DiffAB));
            }
        };

        // The custom difftools' items: the item itself in git's difftool, its sub-items in theirs.
        foreach ((MenuItem item, Action<string?> open) in CustomDiffToolItems())
        {
            CustomDiffTools.OnClick(item, open);
        }

        tsmiCopyPaths.ItemsSource = CopyPathsMenu.Items(ViewModel.CopiedPaths, () => _binding?.RepositoryPath(),
            CopyTextAsync);
    }

    protected override Type StyleKeyOverride => typeof(ContextMenu);

    /// <summary>
    ///  The menu's state and actions.
    /// </summary>
    public FileListMenuViewModel ViewModel { get; }

    /// <summary>
    ///  What the window's list adds to the menu, as upstream's <c>BindContextMenu</c>.
    /// </summary>
    internal void Bind(FileStatusListMenuBinding binding)
    {
        _binding = binding;
        ViewModel.Bind(new FileListMenuSource
        {
            RepositoryPath = binding.RepositoryPath,
            Selection = binding.Selection,
            DescribeRevision = binding.DescribeRevision,
            Refresh = binding.Refresh,
            Actions = () => binding.Services()?.Actions,
            HasFileHistory = () => binding.Services()?.FileHistory is not null,
            ShowInFileTree = binding.ShowInFileTree,
            CanShowInFileTree = binding.CanShowInFileTree,
            FilterFileInGrid = binding.FilterFileInGrid,
            Blame = binding.Blame,
            BlameChecked = binding.BlameChecked,
            CurrentCheckout = binding.CurrentCheckout,
            Tree = binding.Tree,
            Viewer = binding.Viewer is { } viewer ? () => viewer()?.ViewModel : null,
            Submodules = binding.Submodules,
            GitGrep = binding.GitGrep,
            Stage = binding.Stage,
            Unstage = binding.Unstage,
            FindFile = binding.FindFile,
            FocusedPath = binding.FocusedPath,
        }, new MenuHost(this, binding));

        // Upstream's BindContextMenu with stage and unstage (FormCommit's lists) makes both items bold.
        if (ViewModel.HasOwnStage)
        {
            tsmiStageFile.FontWeight = FontWeight.Bold;
            tsmiUnstageFile.FontWeight = FontWeight.Bold;
        }
    }

    /// <summary>
    ///  The menu's items for the selection, or null without one; for the list's hotkeys.
    /// </summary>
    internal DiffFileMenuState? State() => ViewModel.State();

    /// <inheritdoc cref="FileListMenuViewModel.RunDoubleClick"/>
    public bool RunDoubleClick() => ViewModel.RunDoubleClick();

    /// <inheritdoc cref="FileListMenuViewModel.ResetAsync"/>
    internal Task ResetAsync(bool toFirst) => ViewModel.ResetAsync(toFirst);

    /// <inheritdoc cref="FileListMenuViewModel.MoveAsync"/>
    internal Task MoveAsync() => ViewModel.MoveAsync();

    /// <inheritdoc cref="FileListMenuViewModel.DeleteAsync"/>
    internal Task DeleteAsync() => ViewModel.DeleteAsync();

    /// <inheritdoc cref="FileListMenuViewModel.FindFileAsync"/>
    internal Task FindFileAsync() => ViewModel.FindFileAsync();

    /// <summary>
    ///  Upstream FileStatusList's <c>ExecuteCommand</c> for a BrowseDiff hotkey: the menu's items are brought up to date,
    ///  then the command's item is clicked when it is shown and enabled (WinForms' <c>PerformClick</c>), which runs its
    ///  command. False for a command the menu does not handle.
    /// </summary>
    internal bool ExecuteCommand(BrowseDiffCommand command)
    {
        Refresh();
        if (command is BrowseDiffCommand.OpenWithDifftool or BrowseDiffCommand.OpenWithDifftoolFirstToLocal
            or BrowseDiffCommand.OpenWithDifftoolSelectedToLocal)
        {
            RevisionDiffKind kind = command switch
            {
                BrowseDiffCommand.OpenWithDifftoolFirstToLocal => RevisionDiffKind.DiffALocal,
                BrowseDiffCommand.OpenWithDifftoolSelectedToLocal => RevisionDiffKind.DiffBLocal,
                _ => RevisionDiffKind.DiffAB,
            };
            Run(() => ViewModel.OpenWithDifftoolAsync(kind));
            return true;
        }

        // Upstream's ResetSelectedFilesWithConfirmation: to the first revision when the menu offers it; the key is taken
        // either way.
        if (command == BrowseDiffCommand.ResetSelectedFiles)
        {
            if (State() is { ResetFile: true, ResetToFirst: not null })
            {
                Run(() => ViewModel.ResetAsync(toFirst: true));
            }

            return true;
        }

        // Upstream's SelectFirstGroupChangesIfFocused.
        if (command == BrowseDiffCommand.SelectFirstGroupChanges)
        {
            if (_binding?.SelectFirstGroup is not { } selectFirstGroup)
            {
                return false;
            }

            selectFirstGroup();
            return true;
        }

        // Upstream's git-grep hotkeys: each tab's own.
        if ((command == BrowseDiffCommand.FindInCommitFilesUsingGitGrepDiffTab && _binding?.IsFileTreeMode == true)
            || (command == BrowseDiffCommand.FindInCommitFilesUsingGitGrepFileTreeTab && _binding?.IsFileTreeMode != true))
        {
            return false;
        }

        MenuItem? item = command switch
        {
            BrowseDiffCommand.DeleteSelectedFiles => tsmiDeleteFile,
            BrowseDiffCommand.ShowHistory => tsmiFileHistory,
            BrowseDiffCommand.Blame => tsmiBlame,
            BrowseDiffCommand.EditFile => tsmiEditWorkingDirectoryFile,
            BrowseDiffCommand.OpenInVisualStudio => tsmiOpenInVisualStudio,
            BrowseDiffCommand.StageSelectedFile => tsmiStageFile,
            BrowseDiffCommand.UnStageSelectedFile => tsmiUnstageFile,
            BrowseDiffCommand.ShowFileTree => tsmiShowInFileTree,
            BrowseDiffCommand.OpenWorkingDirectoryFile => tsmiOpenWorkingDirectoryFile,
            BrowseDiffCommand.RenameMove => tsmiMove,
            BrowseDiffCommand.FindFile => tsmiFindFile,
            BrowseDiffCommand.OpenAsTempFile => tsmiOpenRevisionFile,
            BrowseDiffCommand.OpenAsTempFileWith => tsmiOpenRevisionFileWith,
            BrowseDiffCommand.OpenWorkingDirectoryFileWith => tsmiOpenWorkingDirectoryFileWith,
            BrowseDiffCommand.FilterFileInGrid => tsmiFilterFileInGrid,
            BrowseDiffCommand.AddFileToGitIgnore => tsmiAddFileToGitIgnore,
            BrowseDiffCommand.FindInCommitFilesUsingGitGrepDiffTab
                or BrowseDiffCommand.FindInCommitFilesUsingGitGrepFileTreeTab => tsmiOpenFindInCommitFilesGitGrepDialog,
            _ => null
        };
        if (item is null)
        {
            return false;
        }

        // A closed menu's items do not ask their commands (their IsEffectivelyEnabled is false until the menu opens), so the
        // item's own state and its command's are read here.
        if (item.IsVisible && item.IsEnabled && item.Command is { } itemCommand
            && itemCommand.CanExecute(item.CommandParameter))
        {
            itemCommand.Execute(item.CommandParameter);
        }

        return true;
    }

    /// <summary>
    ///  Upstream FileStatusList's hotkeys (section "BrowseDiff", which every FileStatusList loads) while
    ///  <paramref name="list"/> has the focus, before the list's own keys (Ctrl+A selects all rows).
    /// </summary>
    internal void AttachHotkeys(ListBox list)
        => list.AddHandler(KeyDownEvent, (_, e) =>
        {
            if (Hotkeys.BrowseDiff.Match(e) is { } command && ExecuteCommand(command))
            {
                e.Handled = true;
            }
        }, RoutingStrategies.Tunnel);

    // Upstream's UpdateStatusOfMenuItems: the view model's state; the bold default items, Blame's check and the scripts are
    // the view's.
    private void Refresh()
    {
        ViewModel.Refresh();
        openSubmoduleMenuItem.FontWeight = ViewModel.IsOpenSubmoduleDefault ? FontWeight.Bold : FontWeight.Normal;
        tsmiFileHistory.FontWeight = ViewModel.IsFileHistoryDefault ? FontWeight.Bold : FontWeight.Normal;
        tsmiBlame.ToggleType = ViewModel.IsBlameCheckable ? MenuItemToggleType.CheckBox : MenuItemToggleType.None;
        ShowScripts();
    }

    // Upstream's LoadCustomDifftools: these items, each with what opens the selection in a given tool.
    private IReadOnlyList<(MenuItem Item, Action<string?> Open)> CustomDiffToolItems()
        =>
        [
            (tsmiDiffFirstToSelected, tool => Run(() => ViewModel.OpenWithDifftoolAsync(RevisionDiffKind.DiffAB, tool))),
            (tsmiDiffSelectedToLocal, tool => Run(() => ViewModel.OpenWithDifftoolAsync(RevisionDiffKind.DiffBLocal, tool))),
            (tsmiDiffFirstToLocal, tool => Run(() => ViewModel.OpenWithDifftoolAsync(RevisionDiffKind.DiffALocal, tool))),
            (tsmiDiffWithRemembered, tool => Run(() => ViewModel.DiffWithRememberedAsync(tool))),
            (tsmiDiffTwoSelected, tool => Run(() => ViewModel.DiffTwoSelectedAsync(tool))),
        ];

    private Task LoadCustomDifftoolsAsync()
        => CustomDiffTools.LoadAsync(CustomDiffToolItems(), diff: true, AppServices.Preferences);

    // Upstream's AddUserScripts: the scripts shown in file lists are items of the menu, the others under "Run script".
    private void ShowScripts()
    {
        tsmiRunScript.IsVisible = true;
        sepScripts.IsVisible = _binding?.RunScript is { } run
                               && ScriptMenus.Fill(this, tsmiRunScript,
                                   script => script.OnEvent == ScriptEvent.ShowInFileList, run);
        if (_binding?.RunScript is null)
        {
            tsmiRunScript.IsEnabled = false;
        }
    }

    private async Task CopyTextAsync(string text)
    {
        if (_binding?.Owner.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(text);
        }
    }

    private void ShowError(string message)
    {
        if (_binding?.Owner is { } owner)
        {
            _ = new ErrorWindow(message).ShowDialog(owner);
        }
    }

    private void Run(Func<Task> action) => UiActions.Run(action, ex => ShowError(ex.Message));

    // Upstream's RemoveMnemonicMarker for an Avalonia header: "Rena_me" is "Rename", "a__b" is "a_b".
    private static string WithoutAccessKey(string header)
        => header.Replace("__", "\0").Replace("_", "").Replace("\0", "_");

    /// <summary>
    ///  The item of <paramref name="list"/> selected last (the selection's anchor, as upstream's <c>FocusedItem</c>) while it
    ///  is selected, else null.
    /// </summary>
    internal static object? FocusedItem(ListBox list)
        => list.Selection.AnchorIndex is var anchor and >= 0 && list.Selection.IsSelected(anchor)
                                                              && anchor < list.ItemCount
            ? list.Items[anchor]
            : null;

    /// <summary>
    ///  Upstream FileStatusList's <c>GetDescriptionForRevision</c> where no <c>DescribeRevision</c> is bound (FormDiff,
    ///  FormStash): the working directory and the index by name, a commit by its short hash.
    /// </summary>
    internal static string DescribeByHash(ObjectId id)
        => id == ObjectId.WorkTreeId ? ResourceManager.TranslatedStrings.Workspace
            : id == ObjectId.IndexId ? ResourceManager.TranslatedStrings.Index
            : id.ToShortString();

    // What the view model asks of the window the menu is shown over.
    private sealed class MenuHost(FileStatusListMenu menu, FileStatusListMenuBinding binding) : IFileListMenuHost
    {
        private Window Owner => binding.Owner;

        private ILauncher Launcher => Owner.Launcher;

        // Upstream's title of the move prompt and its errors: the item's text.
        private string MoveTitle => WithoutAccessKey(menu.tsmiMove.Header as string ?? "");

        public string Translate(string english) => UpstreamTranslation.ByText(english);

        public void ShowError(string message) => menu.ShowError(message);

        public Task ShowMoveErrorAsync(string message)
            => new ErrorWindow(message) { Title = MoveTitle }.ShowDialog(Owner);

        public Task<ResetChangesAction> AskResetAsync(bool hasExistingFiles, bool hasNewFiles, string question)
            => ResetChangesWindow.ShowResetDialogAsync(Owner, hasExistingFiles, hasNewFiles, question);

        public async Task<string?> AskNewNameAsync(string initialValue)
            => (await new PromptWindow(MoveTitle, UpstreamTranslation.ByText(NewNameText), "OK", initialValue: initialValue,
                    allowEmpty: true)
                .ShowDialog<PromptResult?>(Owner))?.Value;

        public Task<bool> ConfirmAsync(string text, string caption)
            => new ConfirmWindow(text, "Yes", caption, cancelText: "No").ShowDialog<bool>(Owner);

        public async Task<string?> PickSaveFileAsync(string suggestion)
        {
            IStorageProvider storage = Owner.StorageProvider;
            string extension = Path.GetExtension(suggestion).TrimStart('.');
            IStorageFile? file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                SuggestedStartLocation = await storage.TryGetFolderFromPathAsync(Path.GetDirectoryName(suggestion) ?? ""),
                SuggestedFileName = Path.GetFileName(suggestion),
                DefaultExtension = extension,
                FileTypeChoices = extension.Length > 0
                    ?
                    [
                        new FilePickerFileType($"{UpstreamTranslation.ByText(SaveFileFilterCurrentFormat)}(*.{extension})")
                        {
                            Patterns = [$"*.{extension}"]
                        },
                        new FilePickerFileType($"{UpstreamTranslation.ByText(SaveFileFilterAllFiles)}(*.*)")
                        {
                            Patterns = ["*"]
                        }
                    ]
                    : null
            });
            return file?.TryGetLocalPath();
        }

        public async Task<string?> PickFolderAsync(string start)
        {
            IStorageProvider storage = Owner.StorageProvider;
            IReadOnlyList<IStorageFolder> folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                SuggestedStartLocation = await storage.TryGetFolderFromPathAsync(start),
                AllowMultiple = false
            });
            return folders.FirstOrDefault()?.TryGetLocalPath();
        }

        public Task OpenFileAsync(string path) => Launcher.LaunchFileInfoAsync(new FileInfo(path));

        public void OpenWith(string path)
        {
            if (OperatingSystem.IsWindows())
            {
                OsShellUtil.OpenAs(path);
            }
        }

        public void EditFile(string path) => new EditorWindow(path).Show(Owner);

        public Task OpenInVisualStudioAsync(string path)
            => OperatingSystem.IsWindows() ? OpenInVisualStudioOnWindowsAsync(path) : Task.CompletedTask;

        public void OpenFolder(string folder) => FileManager.OpenFolder(folder);

        public void ShowFileHistory(string repositoryPath, string file, string? revision, bool showBlame)
            => binding.Services()?.FileHistory?.Invoke(repositoryPath, file, revision, showBlame).Show(Owner);

        public Task<string?> FindFileAsync(Func<string, IEnumerable<string>> candidates)
            => new SearchWindow(candidates).ShowAsync(Owner);

        public Task<bool> AddToIgnoreAsync(string repositoryPath, bool localExclude, IReadOnlyList<string> patterns)
            => AddToGitIgnoreWindow.ShowAsync(Owner, repositoryPath, localExclude, patterns);

        public Task CherryPickAsync(string repositoryPath, (string Text, int Start, int Length) range, string status)
            => binding.Services() is { } services
                ? DiffLinePatching.ApplyAsync(Owner, services, repositoryPath, range, reverse: false, status)
                : Task.CompletedTask;

        public Task RunInteractivePatchAsync(string repositoryPath, string path, bool reset)
            => InteractivePatch.RunAsync(repositoryPath, path, reset, HostPlatform.Current);

        public Task SetClipboardAsync(string text) => menu.CopyTextAsync(text);

        // Upstream's OpenInVisualStudio_Click: the working directory file at the viewer's line, after upstream's question
        // when Visual Studio rejects the call.
        [SupportedOSPlatform("windows")]
        private async Task OpenInVisualStudioOnWindowsAsync(string path)
        {
            if (!VisualStudio.IsInstalled)
            {
                return;
            }

            await VisualStudio.OpenFileAsync(path, binding.Viewer?.Invoke()?.CurrentFileLine ?? 0, async () =>
            {
                MessageBoxWindow question = new(UpstreamTranslation.ByText(VisualStudio.RetryOpenText),
                    UpstreamTranslation.ByText(VisualStudio.RetryCaption), System.Windows.Forms.MessageBoxButtons.YesNo);
                await question.ShowDialog(Owner);
                return question.Result == System.Windows.Forms.DialogResult.Yes;
            });
        }
    }
}

/// <summary>
///  What a window's file list adds to its <see cref="FileStatusListMenu"/>, as upstream's
///  <c>FileStatusList.BindContextMenu</c>.
/// </summary>
internal sealed class FileStatusListMenuBinding
{
    /// <summary>
    ///  The window the menu's dialogs open over.
    /// </summary>
    public required Window Owner { get; init; }

    public required Func<string?> RepositoryPath { get; init; }

    /// <summary>
    ///  The selected files with the revisions the list shows them between; null without a selection.
    /// </summary>
    public required Func<DiffMenuSelection?> Selection { get; init; }

    /// <summary>
    ///  Upstream's <c>DescribeRevision</c>, for the reset and difftool items.
    /// </summary>
    public required Func<ObjectId, string> DescribeRevision { get; init; }

    /// <summary>
    ///  The git actions and windows the items use; null leaves out the items that need them.
    /// </summary>
    public required Func<FileListMenuServices?> Services { get; init; }

    /// <summary>
    ///  Upstream's <c>RequestRefresh</c>: reads the list again after a change.
    /// </summary>
    public required Action Refresh { get; init; }

    /// <summary>
    ///  Upstream's <c>openInFileTreeTab</c>; null hides "Show in File tree" and keeps File history a normal item.
    /// </summary>
    public Action<string>? ShowInFileTree { get; init; }

    /// <summary>
    ///  Whether the file tree can show the selection's files.
    /// </summary>
    public Func<DiffMenuSelection, bool> CanShowInFileTree { get; init; } = _ => true;

    /// <summary>
    ///  Upstream's <c>filterFileInGrid</c> with the grid's path filter; null hides "Filter file in grid".
    /// </summary>
    public Action<string>? FilterFileInGrid { get; init; }

    /// <summary>
    ///  Upstream's <c>blame</c>; null opens the file history on its Blame tab.
    /// </summary>
    public Action? Blame { get; init; }

    /// <summary>
    ///  Whether Blame shows checked (upstream's <c>tsmiBlame.Checked</c>, in the File tree tab); null for a plain item.
    /// </summary>
    public Func<bool>? BlameChecked { get; init; }

    /// <summary>
    ///  Upstream's <c>getCurrentRevision</c>: the commit a folder's history starts from; null for the working directory.
    /// </summary>
    public Func<string?> CurrentCheckout { get; init; } = () => null;

    /// <summary>
    ///  The list's tree for upstream's tree items; null hides them.
    /// </summary>
    public FileListTree? Tree { get; init; }

    /// <summary>
    ///  The viewer of the selected file (upstream's <c>cherryPickChanges</c> and <c>getSupportLinePatching</c>); null hides
    ///  "Cherry pick changes".
    /// </summary>
    public Func<DiffView?>? Viewer { get; init; }

    /// <summary>
    ///  Upstream's submodule commands for the selected submodules; null hides the submodule items.
    /// </summary>
    public Func<FileListSubmodules?> Submodules { get; init; } = () => null;

    /// <summary>
    ///  The list's "Find in commit files using git-grep" (upstream's <c>CanUseFindInCommitFilesGitGrep</c>); null hides its
    ///  items.
    /// </summary>
    public GitGrepBox? GitGrep { get; init; }

    /// <summary>
    ///  Upstream's <c>stage</c> of <c>BindContextMenu</c> (FormCommit's working directory list): what "Stage selected" does
    ///  instead of staging the selected files itself; null for the menu's own.
    /// </summary>
    public Action? Stage { get; init; }

    /// <summary>
    ///  Upstream's <c>unstage</c> of <c>BindContextMenu</c> (FormCommit's staged list); null for the menu's own.
    /// </summary>
    public Action? Unstage { get; init; }

    /// <summary>
    ///  Upstream's <c>SelectFirstGroupChanges</c> hotkey (<c>FirstGroupItems</c>): selects the files of the list's first
    ///  diff group, or all its files when it shows no diff groups; null leaves the key to the list.
    /// </summary>
    public Action? SelectFirstGroup { get; init; }

    /// <summary>
    ///  Upstream's file tree mode (the File tree tab), for its own git-grep hotkey.
    /// </summary>
    public bool IsFileTreeMode { get; init; }

    /// <summary>
    ///  Runs a user script with the list's selection; null offers no scripts.
    /// </summary>
    public Action<ScriptDefinition>? RunScript { get; init; }

    /// <summary>
    ///  The list's files for "Find file" and how to select one; null hides the item.
    /// </summary>
    public FileListFind? FindFile { get; init; }

    /// <summary>
    ///  The path of the file selected last (upstream's <c>FocusedItem</c>), the second file of "Diff the selected files";
    ///  null when unknown.
    /// </summary>
    public Func<string?> FocusedPath { get; init; } = () => null;

    /// <summary>
    ///  The index of <see cref="FocusedPath"/> in the selection's files, or -1.
    /// </summary>
    public int FocusedIndex(DiffMenuSelection selection)
        => FocusedPath() is { } focused
            ? selection.Files.Select(file => file.Path).ToList().IndexOf(focused)
            : -1;
}

/// <summary>
///  The git actions and windows of the browse window that file list menus use (upstream's <c>UICommands</c>).
/// </summary>
/// <param name="Actions">Stage, unstage and stop tracking.</param>
/// <param name="FileHistory">A file history window: repository, file, revision (null for the working directory),
/// whether to show its blame; null leaves out File history and Blame.</param>
/// <param name="OfferConflicts">Upstream's <c>HandleMergeConflicts</c> over a window, after a patch left conflicts.</param>
internal sealed record FileListMenuServices(
    RepositoryOperationsViewModel Actions,
    Func<string, string, string?, bool, Window>? FileHistory = null,
    Func<Window, string, Task>? OfferConflicts = null);

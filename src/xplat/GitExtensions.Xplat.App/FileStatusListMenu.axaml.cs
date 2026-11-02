using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Diff;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Platform;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Scripts;
using GitExtensions.Xplat.Ui;
using GitUI.ScriptsEngine;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream FileStatusList's item menu (<c>FileStatusList.ContextMenu.cs</c>) for a list of the files between two
///  revisions: the browse window's Diff tab and FormDiff. Which items show comes from upstream's rules (core
///  <see cref="DiffFileMenu"/>); the actions are upstream's handlers. What a window adds is its
///  <see cref="FileStatusListMenuBinding"/>, as upstream's <c>BindContextMenu</c>.
/// </summary>
public partial class FileStatusListMenu : ContextMenu
{
    // Upstream FileStatusList texts.
    private const string DeleteSelectedFilesText = "Are you sure you want to delete the selected file(s)?";
    private const string DeleteSelectedFilesCaption = "Delete";
    private const string DeleteFailedText = "Delete file failed";
    private const string StopTrackingFailText = "Fail to stop tracking the file '{0}'.";
    private const string SaveFileFilterCurrentFormat = "Current format";
    private const string SaveFileFilterAllFiles = "All files";

    private static readonly IFileManager FileManager =
        new SystemFileManager(new SystemProcessLauncher(), HostPlatform.Current);

    private FileStatusListMenuBinding? _binding;

    public FileStatusListMenu()
    {
        InitializeComponent();
        UpstreamTranslation.Apply(this, "FileStatusList");
        Opening += (_, _) => UpdateStatusOfMenuItems();
        tsmiStageFile.Click += (_, _) => Run(() => StageAsync(stage: true));
        tsmiUnstageFile.Click += (_, _) => Run(() => StageAsync(stage: false));

        // Upstream's ResetFile_Click: the parent item resets to the first revision when it can.
        tsmiResetFileTo.Click += (_, e) =>
        {
            if (e.Source == tsmiResetFileTo && tsmiResetFileToParent.IsVisible)
            {
                Run(() => ResetAsync(toFirst: true));
            }
        };
        tsmiResetFileToSelected.Click += (_, _) => Run(() => ResetAsync(toFirst: false));
        tsmiResetFileToParent.Click += (_, _) => Run(() => ResetAsync(toFirst: true));

        // Upstream's OpenFilesWithDiffTool: the parent item runs First -> Second.
        tsmiOpenWithDifftool.Click += (_, e) =>
        {
            if (e.Source == tsmiOpenWithDifftool)
            {
                Run(() => OpenWithDifftoolAsync(RevisionDiffKind.DiffAB));
            }
        };
        tsmiDiffFirstToSelected.Click += (_, _) => Run(() => OpenWithDifftoolAsync(RevisionDiffKind.DiffAB));
        tsmiDiffSelectedToLocal.Click += (_, _) => Run(() => OpenWithDifftoolAsync(RevisionDiffKind.DiffBLocal));
        tsmiDiffFirstToLocal.Click += (_, _) => Run(() => OpenWithDifftoolAsync(RevisionDiffKind.DiffALocal));
        tsmiOpenWorkingDirectoryFile.Click += (_, _) => OpenWorkingDirectoryFile();
        tsmiOpenRevisionFile.Click += (_, _) => Run(OpenRevisionFileAsync);
        tsmiEditWorkingDirectoryFile.Click += (_, _) => EditWorkingDirectoryFile();
        tsmiSaveAs.Click += (_, _) => Run(SaveAsAsync);
        tsmiDeleteFile.Click += (_, _) => Run(DeleteAsync);
        tsmiCopyPaths.ItemsSource = CopyPathsMenu.Items(SelectedPaths, () => _binding?.RepositoryPath(), CopyTextAsync);
        tsmiShowInFolder.Click += (_, _) => ShowInFolder();
        tsmiShowInFileTree.Click += (_, _) => WithFirstFile(_binding?.ShowInFileTree);
        tsmiFilterFileInGrid.Click += (_, _) => WithFirstFile(_binding?.FilterFileInGrid);
        tsmiFileHistory.Click += (_, _) => StartFileHistory(showBlame: false);
        tsmiBlame.Click += (_, _) => StartFileHistory(showBlame: true);
        tsmiAddFileToGitIgnore.Click += (_, _) => Run(() => AddToIgnoreFileAsync(localExclude: false));
        tsmiAddFileToGitInfoExclude.Click += (_, _) => Run(() => AddToIgnoreFileAsync(localExclude: true));
        tsmiStopTracking.Click += (_, _) => Run(StopTrackingAsync);
    }

    protected override Type StyleKeyOverride => typeof(ContextMenu);

    /// <summary>
    ///  What the window's list adds to the menu, as upstream's <c>BindContextMenu</c>.
    /// </summary>
    internal void Bind(FileStatusListMenuBinding binding) => _binding = binding;

    /// <summary>
    ///  The menu's items for the selection, or null without one; for the list's hotkeys.
    /// </summary>
    internal DiffFileMenuState? State()
        => _binding is { } binding && binding.RepositoryPath() is { } path && binding.Selection() is { } selection
            ? DiffFileMenu.For(selection, path, binding.DescribeRevision)
            : null;

    // Upstream's UpdateStatusOfMenuItems, InitResetFileToToolStripMenuItem and OpenWithDifftool_DropDownOpening.
    private void UpdateStatusOfMenuItems()
    {
        if (_binding is not { } binding || binding.RepositoryPath() is not { } path
                                        || binding.Selection() is not { } selection)
        {
            foreach (Control item in Items.OfType<Control>())
            {
                item.IsVisible = item == tsmiRunScript;
            }

            sepScripts.IsVisible = false;
            ShowScripts();
            return;
        }

        DiffFileMenuState state = DiffFileMenu.For(selection, path, binding.DescribeRevision);
        tsmiStageFile.IsVisible = state.Stage;
        tsmiUnstageFile.IsVisible = state.Unstage;
        tsmiResetFileTo.IsVisible = true;
        tsmiResetFileTo.IsEnabled = state.ResetFile;
        tsmiResetFileToSelected.IsVisible = state.ResetToSecond is not null;
        tsmiResetFileToSelected.Header = state.ResetToSecond;
        tsmiResetFileToParent.IsVisible = state.ResetToFirst is not null;
        tsmiResetFileToParent.Header = state.ResetToFirst;
        sepGit.IsVisible = true;
        tsmiOpenWithDifftool.IsVisible = true;
        tsmiOpenWithDifftool.IsEnabled = state.OpenWithDifftool;
        tsmiSecondDiffCaption.Header = state.DiffSecondCaption;
        tsmiFirstDiffCaption.Header = state.DiffFirstCaption;
        tsmiDiffFirstToSelected.IsEnabled = state.DiffFirstToSelected;
        tsmiDiffSelectedToLocal.IsEnabled = state.DiffSelectedToLocal;
        tsmiDiffFirstToLocal.IsEnabled = state.DiffFirstToLocal;
        tsmiDiffSelectedToLocal.IsVisible = tsmiDiffFirstToLocal.IsVisible = state.ShowToLocal;
        tsmiOpenWorkingDirectoryFile.IsVisible = state.OpenWorkingFile;
        tsmiOpenRevisionFile.IsVisible = state.OpenRevisionFile;
        tsmiOpenRevisionFile.IsEnabled = state.OpenRevisionFileEnabled;
        tsmiEditWorkingDirectoryFile.IsVisible = state.EditWorkingFile;
        tsmiSaveAs.IsVisible = state.SaveAs;
        tsmiDeleteFile.Header = state.DeleteFileText;
        tsmiDeleteFile.IsVisible = state.DeleteFile;
        sepFile.IsVisible = state.FileSeparator;
        tsmiCopyPaths.IsVisible = true;
        tsmiCopyPaths.IsEnabled = state.CopyPaths;
        tsmiShowInFolder.IsVisible = state.ShowInFolder;
        tsmiShowInFolder.IsEnabled = state.ShowInFolderEnabled;
        sepBrowse.IsVisible = true;

        // Upstream shows "Show in File tree" and "Filter file in grid" only where BindContextMenu gave them an action, and
        // makes File history the default item there.
        tsmiShowInFileTree.IsVisible = binding.ShowInFileTree is not null && state.ShowInFileTree
                                                                         && binding.CanShowInFileTree(selection);
        tsmiFilterFileInGrid.IsVisible = binding.FilterFileInGrid is not null;
        tsmiFilterFileInGrid.IsEnabled = state.FilterFileInGrid;
        tsmiFileHistory.IsVisible = true;
        tsmiFileHistory.IsEnabled = state.FileHistory && binding.Services()?.FileHistory is not null;
        tsmiFileHistory.FontWeight = binding.ShowInFileTree is null ? FontWeight.Normal : FontWeight.Bold;
        tsmiBlame.IsVisible = true;
        tsmiBlame.IsEnabled = state.Blame && binding.Services()?.FileHistory is not null;
        sepIgnore.IsVisible = state.IgnoreSeparator;
        tsmiAddFileToGitIgnore.IsVisible = state.AddToIgnore;
        tsmiAddFileToGitInfoExclude.IsVisible = state.AddToIgnore;
        tsmiStopTracking.IsVisible = state.StopTracking;
        ShowScripts();
    }

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

    private DiffMenuSelection? SelectedFiles() => _binding?.Selection();

    private IEnumerable<string> SelectedPaths() => SelectedFiles()?.Files.Select(file => file.Path) ?? [];

    // The selected files shown as changed in the working directory or in the index.
    private IEnumerable<string> FilesShownAt(ObjectId second)
        => SelectedFiles() is { } selection
            ? selection.Files.Where(file => DiffFileMenu.RevisionsOf(selection, file).Second == second)
                .Select(file => file.Path)
            : [];

    private Window? Owner => _binding?.Owner;

    // Upstream's StageFile_Click and UnstageFile_Click: the selected working directory files to the index, or the selected
    // index files back.
    private async Task StageAsync(bool stage)
    {
        if (_binding is not { } binding || binding.RepositoryPath() is not { } path
                                        || binding.Services() is not { } services)
        {
            return;
        }

        ObjectId from = stage ? ObjectId.WorkTreeId : ObjectId.IndexId;
        List<string> files = [.. FilesShownAt(from)];
        if (files.Count == 0)
        {
            return;
        }

        await (stage ? services.Actions.StageAsync(path, files) : services.Actions.UnstageAsync(path, files));
        binding.Refresh();
    }

    /// <summary>
    ///  Upstream's <c>ResetSelectedItemsWithConfirmation</c>: FormResetChanges with upstream's question, then the reset.
    /// </summary>
    internal async Task ResetAsync(bool toFirst)
    {
        if (_binding is not { } binding || binding.RepositoryPath() is not { } path
                                        || binding.Selection() is not { Files.Count: > 0 } selection)
        {
            return;
        }

        (string revision, bool hasExistingFiles, bool hasNewFiles) =
            DiffFileMenu.ResetQuestion(selection, toFirst, binding.DescribeRevision);
        ResetChangesWindow.ResetAction action = await ResetChangesWindow.ShowResetDialogAsync(binding.Owner,
            hasExistingFiles, hasNewFiles,
            string.Format(UpstreamTranslation.ByText(DiffFileMenu.ResetSelectedChangesText), revision));
        if (action == ResetChangesWindow.ResetAction.Cancel)
        {
            return;
        }

        try
        {
            string output = await DiffFileMenu.ResetAsync(selection, path, toFirst,
                resetAndDelete: action == ResetChangesWindow.ResetAction.ResetAndDelete);
            if (output.Length > 0)
            {
                ShowError(output);
            }
        }
        finally
        {
            binding.Refresh();
        }
    }

    private async Task OpenWithDifftoolAsync(RevisionDiffKind kind)
    {
        if (_binding?.RepositoryPath() is { } path && SelectedFiles() is { Files.Count: > 0 } selection
                                                   && await DiffFileMenu.OpenWithDifftoolAsync(selection, path, kind) is
                                                       { } error)
        {
            ShowError(error);
        }
    }

    // Upstream's OpenWorkingDirectoryFile_Click: the file with the program the system opens it with.
    private void OpenWorkingDirectoryFile()
    {
        if (_binding?.RepositoryPath() is { } path && SelectedPaths().FirstOrDefault() is { } file)
        {
            Run(() => Launcher.LaunchFileInfoAsync(new FileInfo(Path.Combine(path, file))));
        }
    }

    // Upstream's EditWorkingDirectoryFile_Click: the file in Git Extensions' editor.
    private void EditWorkingDirectoryFile()
    {
        if (_binding?.RepositoryPath() is { } path && SelectedPaths().FirstOrDefault() is { } file && Owner is { } owner)
        {
            new EditorWindow(Path.Combine(path, file)).Show(owner);
        }
    }

    // Upstream's OpenRevisionFile_Click: the file as it is in the selected revision, from the temporary folder.
    private async Task OpenRevisionFileAsync()
    {
        if (_binding?.RepositoryPath() is { } path && SelectedFiles() is { Files.Count: > 0 } selection
                                                   && await DiffFileMenu.SaveToTempFileAsync(selection, path) is
                                                       { } file)
        {
            await Launcher.LaunchFileInfoAsync(new FileInfo(file));
        }
    }

    // Upstream's SaveAs_Click: a file name for one file, a folder for several.
    private async Task SaveAsAsync()
    {
        if (_binding is not { } binding || binding.RepositoryPath() is not { } path
                                        || binding.Selection() is not { Files.Count: > 0 } selection)
        {
            return;
        }

        IStorageProvider storage = binding.Owner.StorageProvider;
        string suggestion = DiffFileMenu.SaveSuggestion(selection, path);
        string? target;
        if (selection.Files.Count == 1)
        {
            string extension = Path.GetExtension(suggestion).TrimStart('.');
            IStorageFile? file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                SuggestedStartLocation =
                    await storage.TryGetFolderFromPathAsync(Path.GetDirectoryName(suggestion) ?? path),
                SuggestedFileName = Path.GetFileName(suggestion),
                DefaultExtension = extension,
                FileTypeChoices = extension.Length > 0
                    ?
                    [
                        new FilePickerFileType(
                            $"{UpstreamTranslation.ByText(SaveFileFilterCurrentFormat)}(*.{extension})")
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
            target = file?.TryGetLocalPath();
        }
        else
        {
            IReadOnlyList<IStorageFolder> folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                SuggestedStartLocation = await storage.TryGetFolderFromPathAsync(suggestion),
                AllowMultiple = false
            });
            target = folders.FirstOrDefault()?.TryGetLocalPath();
        }

        if (target is not null)
        {
            await DiffFileMenu.SaveAsync(selection, path, target);
        }
    }

    /// <summary>
    ///  Upstream's <c>DeleteFile_Click</c>: after the question, staged files leave the index, then the files leave the
    ///  disk.
    /// </summary>
    internal async Task DeleteAsync()
    {
        if (_binding is not { } binding || binding.RepositoryPath() is not { } path
                                        || binding.Selection() is not { Files.Count: > 0 } selection
                                        || !DiffFileMenu.RevisionsOf(selection, selection.Files[0]).Second.IsArtificial
                                        || binding.Services() is not { } services
                                        || !await new ConfirmWindow(UpstreamTranslation.ByText(DeleteSelectedFilesText),
                                                "Yes", UpstreamTranslation.ByText(DeleteSelectedFilesCaption),
                                                cancelText: "No")
                                            .ShowDialog<bool>(binding.Owner))
        {
            return;
        }

        try
        {
            if (FilesShownAt(ObjectId.IndexId).ToList() is { Count: > 0 } staged)
            {
                await services.Actions.UnstageAsync(path, staged);
            }

            foreach (string file in selection.Files.Select(file => Path.Combine(path, file.Path)))
            {
                if (Directory.Exists(file))
                {
                    Directory.Delete(file, recursive: true);
                }
                else
                {
                    File.Delete(file);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowError($"{UpstreamTranslation.ByText(DeleteFailedText)}{Environment.NewLine}{ex.Message}");
        }
        finally
        {
            binding.Refresh();
        }
    }

    // Upstream's FormBrowse.OpenContainingFolder: the folder of the first selected file that exists, or of its parent.
    private void ShowInFolder()
    {
        if (_binding?.RepositoryPath() is not { } path)
        {
            return;
        }

        foreach (string file in SelectedPaths().Select(file => Path.Combine(path, file)))
        {
            if (Path.GetDirectoryName(file) is { } folder && Directory.Exists(folder))
            {
                FileManager.OpenFolder(folder);
                return;
            }
        }
    }

    private void WithFirstFile(Action<string>? action)
    {
        if (action is not null && SelectedPaths().FirstOrDefault() is { } file)
        {
            action(file);
        }
    }

    // Upstream's StartFileHistoryDialog: a tracked file's history from the selected revision.
    private void StartFileHistory(bool showBlame)
    {
        if (_binding is not { } binding || binding.RepositoryPath() is not { } path
                                        || binding.Services()?.FileHistory is not { } fileHistory
                                        || binding.Selection() is not { Files: [{ IsTracked: true } file, ..] } selection)
        {
            return;
        }

        ObjectId second = DiffFileMenu.RevisionsOf(selection, file).Second;
        string? revision = second.IsArtificial ? null : second.ToString();
        fileHistory(path, file.Path, revision, showBlame).Show(binding.Owner);
    }

    // Upstream's AddFileToIgnoreFile: each selected file anchored at the root, added to .gitignore or the local excludes.
    private async Task AddToIgnoreFileAsync(bool localExclude)
    {
        List<string> patterns = [.. SelectedPaths().Select(GitIgnoreFile.PatternFor)];
        if (_binding is { } binding && binding.RepositoryPath() is { } path && patterns.Count > 0
            && await new GitIgnoreWindow(path, patterns, localExclude).ShowDialog<bool>(binding.Owner))
        {
            binding.Refresh();
        }
    }

    private async Task StopTrackingAsync()
    {
        if (_binding is not { } binding || binding.RepositoryPath() is not { } path
                                        || binding.Services() is not { } services
                                        || SelectedPaths().FirstOrDefault() is not { } file)
        {
            return;
        }

        if (!await services.Actions.StopTrackingAsync(path, file))
        {
            ShowError(string.Format(UpstreamTranslation.ByText(StopTrackingFailText), file));
        }

        binding.Refresh();
    }

    private async Task CopyTextAsync(string text)
    {
        if (Owner?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(text);
        }
    }

    private ILauncher Launcher => Owner?.Launcher ?? throw new InvalidOperationException();

    private void ShowError(string message)
    {
        if (Owner is { } owner)
        {
            _ = new ErrorWindow(message).ShowDialog(owner);
        }
    }

    private void Run(Func<Task> action) => UiActions.Run(action, ex => ShowError(ex.Message));

    /// <summary>
    ///  Upstream FileStatusList's <c>GetDescriptionForRevision</c> where no <c>DescribeRevision</c> is bound (FormDiff,
    ///  FormStash): the working directory and the index by name, a commit by its short hash.
    /// </summary>
    internal static string DescribeByHash(ObjectId id)
        => id == ObjectId.WorkTreeId ? ResourceManager.TranslatedStrings.Workspace
            : id == ObjectId.IndexId ? ResourceManager.TranslatedStrings.Index
            : id.ToShortString();
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
    ///  Upstream's <c>filterFileInGrid</c>; null hides "Filter file in grid".
    /// </summary>
    public Action<string>? FilterFileInGrid { get; init; }

    /// <summary>
    ///  Runs a user script with the list's selection; null offers no scripts.
    /// </summary>
    public Action<ScriptDefinition>? RunScript { get; init; }
}

/// <summary>
///  The git actions and windows of the browse window that file list menus use (upstream's <c>UICommands</c>).
/// </summary>
/// <param name="Actions">Stage, unstage and stop tracking.</param>
/// <param name="FileHistory">A file history window: repository, file, revision (null for the working directory),
/// whether to show its blame; null leaves out File history and Blame.</param>
internal sealed record FileListMenuServices(
    RepositoryOperationsViewModel Actions,
    Func<string, string, string?, bool, Window>? FileHistory = null);

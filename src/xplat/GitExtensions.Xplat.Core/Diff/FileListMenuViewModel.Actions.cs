using CommunityToolkit.Mvvm.Input;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Scripts;

namespace GitExtensions.Xplat.Core.Diff;

// Upstream FileStatusList's item menu actions (FileStatusList.ContextMenu.cs): each acts on the selection the window gives,
// reaching the window through the host for its questions, pickers and windows.
public sealed partial class FileListMenuViewModel
{
    // Upstream FileStatusList texts.
    public const string DeleteSelectedFilesText = "Are you sure you want to delete the selected file(s)?";
    public const string DeleteSelectedFilesCaption = "Delete";
    public const string DeleteFailedText = "Delete file failed";
    public const string StopTrackingFailText = "Fail to stop tracking the file '{0}'.";

    private FileListMenuSource? _source;
    private IFileListMenuHost? _host;

    /// <summary>
    ///  The window's list and the window the menu's questions and windows show over.
    /// </summary>
    public void Bind(FileListMenuSource source, IFileListMenuHost host)
    {
        _source = source;
        _host = host;
    }

    // Upstream's AddUserScripts items: the script on the list's selection.
    [RelayCommand]
    private void RunScript(ScriptDefinition script) => _source?.RunScript?.Invoke(script);

    /// <summary>
    ///  Whether the window gave a stage and unstage of its own (upstream makes both items bold then).
    /// </summary>
    public bool HasOwnStage => _source is { Stage: not null } or { Unstage: not null };

    /// <summary>
    ///  Upstream's <c>UpdateStatusOfMenuItems</c> for the window's selection.
    /// </summary>
    public void Refresh()
    {
        FileListTreeState tree = new(_source?.Tree?.HasSubnodes() ?? false, _source?.Tree?.AnyRootExpanded() ?? false);
        if (_source is not { } source || source.RepositoryPath() is not { } path || source.Selection() is not { } selection)
        {
            Refresh(null, null, new FileListMenuContext(), tree);
            return;
        }

        FileListMenuContext context = new(
            HasSubmodules: source.Submodules() is not null,
            HasOwnStage: source.Stage is not null,
            HasServices: source.Actions() is not null,
            HasFileHistory: source.HasFileHistory(),
            HasShowInFileTree: source.ShowInFileTree is not null,
            CanShowInFileTree: source.CanShowInFileTree(selection),
            HasFilterFileInGrid: source.FilterFileInGrid is not null,
            HasBlame: source.Blame is not null,
            BlameChecked: source.BlameChecked?.Invoke(),
            HasFindFile: source.FindFile is not null,
            HasGitGrep: source.GitGrep is not null,
            GitGrepBoxVisible: source.GitGrep?.BoxVisible ?? false,
            CanMove: DiffFileMenu.MoveTarget(selection) is not null);
        Refresh(MenuState(selection, path), DiffFileRemember.For(selection, path, source.FocusedIndex(selection)), context,
            tree);
    }

    /// <summary>
    ///  The menu's items for the selection, or null without one; for the list's hotkeys.
    /// </summary>
    public DiffFileMenuState? State()
        => _source is { } source && source.RepositoryPath() is { } path && source.Selection() is { } selection
            ? MenuState(selection, path)
            : null;

    /// <summary>
    ///  Upstream FileStatusList's <c>FileStatusListView_DoubleClick</c>: a submodule opens where
    ///  <c>OpenSubmoduleDiffInSeparateWindow</c> says so, anything else shows its history. True when something was done.
    /// </summary>
    public bool RunDoubleClick()
    {
        Refresh();
        if (IsOpenSubmoduleVisible && _preferences.OpenSubmoduleDiffInSeparateWindow)
        {
            OpenSubmoduleCommand.Execute(null);
            return true;
        }

        if (IsFileHistoryEnabled)
        {
            StartFileHistory(showBlame: false);
            return true;
        }

        return false;
    }

    /// <summary>
    ///  Upstream's <c>getSelectedFilePaths</c>: the selected folder, else the selected files; for "Copy path(s)".
    /// </summary>
    public IEnumerable<string> CopiedPaths()
        => _source?.Selection() is { } selection ? DiffFileMenu.CopiedPaths(selection) : [];

    /// <summary>
    ///  Upstream's <c>OpenFilesWithDiffTool</c>: the selection in the difftool, in <paramref name="tool"/> or git's.
    /// </summary>
    public async Task OpenWithDifftoolAsync(RevisionDiffKind kind, string? tool = null)
    {
        if (_source?.RepositoryPath() is { } path && _source.Selection() is { Files.Count: > 0 } selection
                                                  && await DiffFileMenu.OpenWithDifftoolAsync(selection, path, kind, tool) is
                                                      { } error)
        {
            _host?.ShowError(error);
        }
    }

    /// <summary>
    ///  Upstream's <c>DiffTwoSelected_Click</c>.
    /// </summary>
    public async Task DiffTwoSelectedAsync(string? tool)
    {
        if (_source is { } source && source.RepositoryPath() is { } path
                                  && source.Selection() is { Files.Count: 2 } selection)
        {
            await DiffFileRemember.DiffTwoSelectedAsync(selection, path, source.FocusedIndex(selection), tool);
        }
    }

    /// <summary>
    ///  Upstream's <c>DiffWithRemembered_Click</c>.
    /// </summary>
    public async Task DiffWithRememberedAsync(string? tool)
    {
        if (_source?.RepositoryPath() is { } path && _source.Selection() is { Files.Count: 1 } selection)
        {
            await DiffFileRemember.DiffWithRememberedAsync(selection, path, tool);
        }
    }

    /// <summary>
    ///  Upstream's <c>ResetSelectedItemsWithConfirmation</c>: FormResetChanges with upstream's question, then the reset.
    /// </summary>
    public async Task ResetAsync(bool toFirst)
    {
        if (_source is not { } source || _host is not { } host || source.RepositoryPath() is not { } path
            || source.Selection() is not { Files.Count: > 0 } selection)
        {
            return;
        }

        (string revision, bool hasExistingFiles, bool hasNewFiles) =
            DiffFileMenu.ResetQuestion(selection, toFirst, source.DescribeRevision);
        ResetChangesAction action = await host.AskResetAsync(hasExistingFiles, hasNewFiles,
            string.Format(System.Globalization.CultureInfo.CurrentCulture,
                host.Translate(DiffFileMenu.ResetSelectedChangesText), revision));
        if (action == ResetChangesAction.Cancel)
        {
            return;
        }

        try
        {
            string output = await DiffFileMenu.ResetAsync(selection, path, toFirst,
                resetAndDelete: action == ResetChangesAction.ResetAndDelete);
            if (output.Length > 0)
            {
                host.ShowError(output);
            }
        }
        finally
        {
            source.Refresh();
        }
    }

    /// <summary>
    ///  Upstream's <c>Move_Click</c>: asks for the file's (or the selected folder's) new name until git has moved it or the
    ///  user cancels; a name git's move does not take (empty, the old one) is asked for again, and so is one git failed with
    ///  after its error.
    /// </summary>
    public async Task MoveAsync()
    {
        if (_source is not { } source || _host is not { } host || source.RepositoryPath() is not { } path
            || source.Selection() is not { } selection
            || DiffFileMenu.MoveTarget(selection) is not var (oldName, isFolder))
        {
            return;
        }

        string newName = oldName;
        while (await host.AskNewNameAsync(newName) is { } answer)
        {
            newName = answer;
            if (!DiffFileMenu.IsValidMove(path, oldName, newName, isFolder))
            {
                continue;
            }

            try
            {
                await DiffFileMenu.MoveAsync(path, oldName, newName, isFolder);
                return;
            }
            catch (Exception ex)
            {
                await host.ShowMoveErrorAsync(ex.Message);
            }
            finally
            {
                source.Refresh();
            }
        }
    }

    /// <summary>
    ///  Upstream's <c>DeleteFile_Click</c>: after the question, staged files leave the index, then the files leave the
    ///  disk.
    /// </summary>
    public async Task DeleteAsync()
    {
        if (_source is not { } source || _host is not { } host || source.RepositoryPath() is not { } path
            || source.Selection() is not { Files.Count: > 0 } selection
            || !DiffFileMenu.RevisionsOf(selection, selection.Files[0]).Second.IsArtificial
            || source.Actions() is not { } actions
            || !await host.ConfirmAsync(host.Translate(DeleteSelectedFilesText), host.Translate(DeleteSelectedFilesCaption)))
        {
            return;
        }

        try
        {
            if (FilesShownAt(selection, ObjectId.IndexId).ToList() is { Count: > 0 } staged)
            {
                await actions.UnstageAsync(path, staged);
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
            host.ShowError($"{host.Translate(DeleteFailedText)}{Environment.NewLine}{ex.Message}");
        }
        finally
        {
            source.Refresh();
        }
    }

    /// <summary>
    ///  Upstream's <c>FindFile_Click</c>: the search window over the list's files; the file taken is selected.
    /// </summary>
    public async Task FindFileAsync()
    {
        if (_source is not { FindFile: { } find } source || _host is not { } host
                                                         || source.RepositoryPath() is not { } path)
        {
            return;
        }

        IReadOnlyList<(string Path, string? OldPath)> files = find.Files();
        if (await host.FindFileAsync(text => Repository.FindFile.Matches(files, text, path)) is { } file)
        {
            find.Select(file);
        }
    }

    // Upstream's getSupportLinePatching: the viewer shows the selected commit file's diff and can apply its lines.
    private bool SupportLinePatching()
        => _source?.Viewer?.Invoke() is { LineAction: DiffLineAction.Apply } viewer && viewer.WholeRange() is not null;

    private DiffFileMenuState MenuState(DiffMenuSelection selection, string path)
        => DiffFileMenu.For(selection, path, _source!.DescribeRevision, supportLinePatching: SupportLinePatching());

    // The selected files shown as changed in the working directory or in the index.
    private static IEnumerable<string> FilesShownAt(DiffMenuSelection selection, ObjectId second)
        => selection.Files.Where(file => DiffFileMenu.RevisionsOf(selection, file).Second == second)
            .SelectMany(file => second == ObjectId.IndexId && file.OldPath is { } oldPath ? [file.Path, oldPath] : new[] { file.Path });

    private IEnumerable<string> SelectedPaths() => _source?.Selection()?.Files.Select(file => file.Path) ?? [];

    // The first selected file in the working directory.
    private string? FirstWorkingFile()
        => _source?.RepositoryPath() is { } path && SelectedPaths().FirstOrDefault() is { } file ? Path.Combine(path, file) : null;

    // Upstream's submodule items: each acts on the selected submodules' paths (Distinct, as upstream's).
    private Task WithSubmodulesAsync(Func<FileListSubmodules, IReadOnlyList<string>, Task> action)
        => _source?.Submodules() is { } submodules && _source.Selection() is { } selection
           && selection.Files.Where(file => file.IsSubmodule).Select(file => file.Path).Distinct().ToList() is
               { Count: > 0 } paths
            ? GuardAsync(() => action(submodules, paths))
            : Task.CompletedTask;

    [RelayCommand]
    private Task OpenSubmoduleAsync() => WithSubmodulesAsync((submodules, paths) => submodules.Open(paths[0]));

    [RelayCommand]
    private Task UpdateSubmoduleAsync() => WithSubmodulesAsync((submodules, paths) => submodules.Update(paths));

    [RelayCommand]
    private Task ResetSubmoduleAsync() => WithSubmodulesAsync((submodules, paths) => submodules.Reset(paths));

    [RelayCommand]
    private Task StashSubmoduleAsync() => WithSubmodulesAsync((submodules, paths) => submodules.Stash(paths));

    [RelayCommand]
    private Task CommitSubmoduleAsync() => WithSubmodulesAsync((submodules, paths) => submodules.Commit(paths));

    // Upstream's StageFile_Click and UnstageFile_Click: the window's own stage or unstage when it gave one (FormCommit's),
    // else the selected working directory's or index's files.
    [RelayCommand]
    private Task StageAsync() => GuardAsync(() => StageFilesAsync(stage: true));

    [RelayCommand]
    private Task UnstageAsync() => GuardAsync(() => StageFilesAsync(stage: false));

    // Upstream's SkipWorktree_Click and AssumeUnchanged_Click: git's flag on the selected files, set when none had it;
    // then the list is read again.
    [RelayCommand]
    private Task SetIndexFlagAsync(IndexFlag flag) => GuardAsync(async () =>
    {
        if (_source is { } source && source.RepositoryPath() is { } path && source.Selection() is { } selection)
        {
            bool value = flag == IndexFlag.SkipWorktree
                ? !selection.Files.Any(file => file.IsSkipWorktree)
                : !selection.Files.Any(file => file.IsAssumeUnchanged);
            await DiffFileMenu.SetIndexFlagAsync(selection, path, flag, value);
            source.Refresh();
        }
    });

    [RelayCommand]
    private Task ResetToSelectedAsync() => GuardAsync(() => ResetAsync(toFirst: false));

    [RelayCommand]
    private Task ResetToParentAsync() => GuardAsync(() => ResetAsync(toFirst: true));

    // Upstream's ResetFile_Click: the parent item resets to the first revision when it can. Its sub-items' clicks reach it
    // too, but their own commands mark them handled first.
    [RelayCommand]
    private void ResetFileTo()
    {
        if (IsResetToFirstVisible)
        {
            ResetToParentCommand.Execute(null);
        }
    }

    // Upstream's CherryPickChanges_Click (the viewer's CherryPickAllChanges): the whole diff applied to the working
    // directory and the index.
    [RelayCommand]
    private Task CherryPickAsync() => GuardAsync(async () =>
    {
        if (_source is { } source && _host is { } host && source.RepositoryPath() is { } path && source.Actions() is not null
            && source.Viewer?.Invoke() is { } viewer && SupportLinePatching() && viewer.WholeRange() is { } range
            && source.Selection() is { Files: [{ } file] })
        {
            await host.CherryPickAsync(path, range, file.Status);
        }
    });

    // Upstream's InteractiveAdd_Click and ResetChunkOfFile_Click: git's interactive patch of the file in a window of its
    // own, then the list read again.
    [RelayCommand]
    private Task InteractivePatchAsync(bool reset) => GuardAsync(async () =>
    {
        if (_source is { } source && _host is { } host && source.RepositoryPath() is { } path
            && source.Selection() is { Files: [{ } file] })
        {
            await host.RunInteractivePatchAsync(path, file.Path, reset);
            source.Refresh();
        }
    });

    // Upstream's RememberSecondRevDiff_Click and RememberFirstRevDiff_Click.
    [RelayCommand]
    private void Remember(bool first)
    {
        if (_source?.RepositoryPath() is { } path && _source.Selection() is { Files.Count: 1 } selection)
        {
            DiffFileRemember.Remember(selection, path, first);
        }
    }

    // Upstream's OpenWorkingDirectoryFile_Click: the file with the program the system opens it with.
    [RelayCommand]
    private Task OpenWorkingFileAsync()
        => FirstWorkingFile() is { } file && _host is { } host ? GuardAsync(() => host.OpenFileAsync(file)) : Task.CompletedTask;

    // Upstream's OpenWorkingDirectoryFileWith_Click: the system's "Open with" dialog for the file.
    [RelayCommand]
    private void OpenWorkingFileWith()
    {
        if (FirstWorkingFile() is { } file)
        {
            _host?.OpenWith(Path.GetFullPath(file));
        }
    }

    // Upstream's OpenRevisionFile_Click: the file as it is in the selected revision, from the temporary folder.
    [RelayCommand]
    private Task OpenRevisionFileAsync() => GuardAsync(async () =>
    {
        if (await SaveToTempFileAsync() is { } file && _host is { } host)
        {
            await host.OpenFileAsync(file);
        }
    });

    // Upstream's OpenRevisionFileWith_Click: the revision's file saved to the temporary folder, then "Open with".
    [RelayCommand]
    private Task OpenRevisionFileWithAsync() => GuardAsync(async () =>
    {
        if (await SaveToTempFileAsync() is { } file)
        {
            _host?.OpenWith(file);
        }
    });

    // Upstream's EditWorkingDirectoryFile_Click: the file in Git Extensions' editor.
    [RelayCommand]
    private void EditWorkingFile()
    {
        if (FirstWorkingFile() is { } file)
        {
            _host?.EditFile(file);
        }
    }

    // Upstream's OpenInVisualStudio_Click.
    [RelayCommand]
    private Task OpenInVisualStudioAsync()
        => FirstWorkingFile() is { } file && _host is { } host
            ? GuardAsync(() => host.OpenInVisualStudioAsync(Path.GetFullPath(file)))
            : Task.CompletedTask;

    // Upstream's SaveAs_Click: a file name for one file, a folder for several.
    [RelayCommand]
    private Task SaveAsAsync() => GuardAsync(async () =>
    {
        if (_source is not { } source || _host is not { } host || source.RepositoryPath() is not { } path
            || source.Selection() is not { Files.Count: > 0 } selection)
        {
            return;
        }

        string suggestion = DiffFileMenu.SaveSuggestion(selection, path);
        string? target = selection.Files.Count == 1
            ? await host.PickSaveFileAsync(suggestion)
            : await host.PickFolderAsync(suggestion);
        if (target is not null)
        {
            await DiffFileMenu.SaveAsync(selection, path, target);
        }
    });

    [RelayCommand]
    private Task MoveFileAsync() => GuardAsync(MoveAsync);

    [RelayCommand]
    private Task DeleteFileAsync() => GuardAsync(DeleteAsync);

    // Upstream's FormBrowse.OpenContainingFolder: the folder of the first selected file that exists, or of its parent.
    [RelayCommand]
    private void ShowInFolder()
    {
        if (_source?.RepositoryPath() is not { } path)
        {
            return;
        }

        foreach (string file in SelectedPaths().Select(file => Path.Combine(path, file)))
        {
            if (Path.GetDirectoryName(file) is { } folder && Directory.Exists(folder))
            {
                _host?.OpenFolder(folder);
                return;
            }
        }
    }

    // Upstream's OpenInFileTreeTab: the selected folder, else the first selected file.
    [RelayCommand]
    private void ShowInFileTree()
    {
        if (_source?.ShowInFileTree is { } show
            && (_source.Selection()?.Folder is { Length: > 0 } folder ? folder : SelectedPaths().FirstOrDefault()) is { } path)
        {
            show(path);
        }
    }

    // Upstream RevisionDiffControl's FilterFileInGrid: the selected folder, else the selected files.
    [RelayCommand]
    private void FilterFileInGrid()
    {
        if (_source?.FilterFileInGrid is { } filter && _source.Selection() is { } selection
                                                    && (selection.Folder is not null || selection.Files.Count > 0))
        {
            filter(DiffFileMenu.GridPathFilter(selection));
        }
    }

    [RelayCommand]
    private void FileHistory() => StartFileHistory(showBlame: false);

    // Upstream's Blame: what the window gave (in the File tree tab, blame in place of the file's content), else the file
    // history on its Blame tab.
    [RelayCommand]
    private void Blame()
    {
        if (_source?.Blame is { } blame)
        {
            blame();
        }
        else
        {
            StartFileHistory(showBlame: true);
        }
    }

    [RelayCommand]
    private Task FindFileInListAsync() => GuardAsync(FindFileAsync);

    [RelayCommand]
    private void OpenGitGrep() => _source?.GitGrep?.ShowWindow("");

    // Upstream's ShowFindInCommitFilesGitGrep_Click.
    [RelayCommand]
    private void ToggleGitGrepBox()
    {
        if (_source?.GitGrep is { } grep)
        {
            bool show = !grep.BoxVisible;
            _preferences.ShowFindInCommitFilesGitGrep = show;
            _preferences.Save();
            grep.SetBoxVisible(show);
        }
    }

    // Upstream's AddFileToIgnoreFile: the selected folder or each selected file anchored at the root, added to .gitignore or
    // the local excludes.
    [RelayCommand]
    private Task AddToIgnoreFileAsync(bool localExclude) => GuardAsync(async () =>
    {
        List<string> patterns = _source?.Selection() is { } selection ? [.. DiffFileMenu.IgnorePatterns(selection)] : [];
        if (_source is { } source && _host is { } host && source.RepositoryPath() is { } path && patterns.Count > 0
            && await host.AddToIgnoreAsync(path, localExclude, patterns))
        {
            source.Refresh();
        }
    });

    [RelayCommand]
    private Task StopTrackingAsync() => GuardAsync(async () =>
    {
        if (_source is not { } source || _host is not { } host || source.RepositoryPath() is not { } path
            || source.Actions() is not { } actions || SelectedPaths().FirstOrDefault() is not { } file)
        {
            return;
        }

        if (!await actions.StopTrackingAsync(path, file))
        {
            host.ShowError(string.Format(System.Globalization.CultureInfo.CurrentCulture,
                host.Translate(StopTrackingFailText), file));
        }

        source.Refresh();
    });

    [RelayCommand]
    private void SelectAll() => _source?.Tree?.SelectAll();

    [RelayCommand]
    private void CollapseAll() => _source?.Tree?.CollapseAll();

    [RelayCommand]
    private void ExpandAll() => _source?.Tree?.ExpandAll();

    [RelayCommand]
    private void CollapseRootFolders() => _source?.Tree?.CollapseRootFolders();

    private async Task StageFilesAsync(bool stage)
    {
        if ((stage ? _source?.Stage : _source?.Unstage) is { } own)
        {
            own();
            return;
        }

        if (_source is not { } source || source.RepositoryPath() is not { } path || source.Actions() is not { } actions
            || source.Selection() is not { } selection)
        {
            return;
        }

        List<string> files = [.. FilesShownAt(selection, stage ? ObjectId.WorkTreeId : ObjectId.IndexId)];
        if (files.Count == 0)
        {
            return;
        }

        await (stage ? actions.StageAsync(path, files) : actions.UnstageAsync(path, files));
        source.Refresh();
    }

    // Upstream's StartFileHistoryDialog: a tracked file's history from the selected revision, or a folder's from the
    // current checkout.
    private void StartFileHistory(bool showBlame)
    {
        if (_source is { } source && source.HasFileHistory() && source.RepositoryPath() is { } path
            && source.Selection() is { } selection
            && DiffFileMenu.FileHistoryTarget(selection, source.CurrentCheckout()) is var (file, revision))
        {
            _host?.ShowFileHistory(path, file, revision, showBlame);
        }
    }

    private Task<string?> SaveToTempFileAsync()
        => _source?.RepositoryPath() is { } path && _source.Selection() is { Files.Count: > 0 } selection
            ? DiffFileMenu.SaveToTempFileAsync(selection, path)
            : Task.FromResult<string?>(null);

    // An exception that escapes an asynchronous command ends the app; it shows over the window instead, as the menu did.
    private async Task GuardAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (OperationCanceledException)
        {
            // Nothing to report.
        }
        catch (Exception ex)
        {
            _host?.ShowError(ex.Message);
        }
    }
}

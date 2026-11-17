using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;

namespace GitExtensions.Xplat.Core.Diff;

/// <summary>
///  What <see cref="DiffFilesSettingsViewModel"/> asks of the browse window.
/// </summary>
public sealed record DiffFilesSettingsServices
{
    /// <summary>
    ///  Shows the Diff tab's files again with the groups calculated again (upstream's <c>RequestRefresh</c>).
    /// </summary>
    public required Action RefreshFiles { get; init; }

    /// <summary>
    ///  Shows the selected commit's own files again, without calculating the groups.
    /// </summary>
    public required Action ShowFiles { get; init; }

    /// <summary>
    ///  Upstream's <c>ShowDiffForAllParents_Click</c>: the groups are calculated again.
    /// </summary>
    public required Action RecalculateGroups { get; init; }

    /// <summary>
    ///  Upstream's ignored files' editors: <c>.gitignore</c>, or the repository's own exclude file when true.
    /// </summary>
    public Func<bool, Task> EditIgnoredFiles { get; init; } = _ => Task.CompletedTask;

    public Action<Exception> ShowError { get; init; } = _ => { };

    /// <summary>
    ///  A repository's effective git config value; the default asks git.
    /// </summary>
    public Func<string, string, string> GitSetting { get; init; } =
        (path, key) => GitModules.Open(path).GetEffectiveSetting(key);
}

/// <summary>
///  Upstream FileStatusList's settings menu over the Diff tab's files, as <c>RevisionDiffControl</c> binds it
///  (<c>canAutoRefresh</c>): "Show skip-worktree files" and "Show untracked files" for the working directory's row (the
///  latter at first as git's <c>status.showuntrackedfiles</c> says), "Refresh artificial commits on form focus" and "Show
///  file differences for all parents", which are stored at once.
/// </summary>
public sealed partial class DiffFilesSettingsViewModel(IAppPreferences preferences, DiffFilesSettingsServices services)
    : ObservableObject
{
    // Git's setting of upstream's initial "Show untracked files", and its value that turns them off.
    public const string ShowUntrackedFilesSetting = "status.showuntrackedfiles";
    public const string NoUntrackedFiles = "no";

    private bool _reading;

    [ObservableProperty]
    public partial bool ShowSkipWorktree { get; set; }

    [ObservableProperty]
    public partial bool ShowUntracked { get; set; } = true;

    /// <summary>
    ///  Upstream's <c>UpdateToolbar(revisions)</c>: the untracked and skip-worktree files only for the working directory.
    /// </summary>
    [ObservableProperty]
    public partial bool IsWorkTreeSelected { get; private set; }

    public bool RefreshOnFormFocus
    {
        get => preferences.RefreshCommitDialogOnFocus;
        set
        {
            if (value != preferences.RefreshCommitDialogOnFocus)
            {
                preferences.RefreshCommitDialogOnFocus = value;
                preferences.Save();
                OnPropertyChanged();
            }
        }
    }

    public bool ShowDiffForAllParents
    {
        get => preferences.ShowDiffForAllParents;
        set
        {
            if (value != preferences.ShowDiffForAllParents)
            {
                preferences.ShowDiffForAllParents = value;
                preferences.Save();
                OnPropertyChanged();
                services.RecalculateGroups();
            }
        }
    }

    /// <summary>
    ///  The state as the menu opens: the selection and the stored choices, which the settings window may have changed.
    /// </summary>
    public void Refresh(bool workTreeSelected)
    {
        IsWorkTreeSelected = workTreeSelected;
        OnPropertyChanged(nameof(RefreshOnFormFocus));
        OnPropertyChanged(nameof(ShowDiffForAllParents));
    }

    /// <summary>
    ///  Upstream's <c>BindContextMenu</c>: "Show untracked files" starts as git says, read when a repository opens.
    /// </summary>
    public async Task ReadShowUntrackedAsync(string repositoryPath)
    {
        string value = await Task.Run(() => services.GitSetting(repositoryPath, ShowUntrackedFilesSetting));
        _reading = true;
        try
        {
            ShowUntracked = value != NoUntrackedFiles;
        }
        finally
        {
            _reading = false;
        }

        services.ShowFiles();
    }

    [RelayCommand]
    private Task EditGitIgnoreAsync() => EditIgnoredAsync(localExclude: false);

    [RelayCommand]
    private Task EditLocallyIgnoredAsync() => EditIgnoredAsync(localExclude: true);

    private async Task EditIgnoredAsync(bool localExclude)
    {
        try
        {
            await services.EditIgnoredFiles(localExclude);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            services.ShowError(ex);
        }
    }

    // Upstream's RequestRefresh for the two show items: skip-worktree files come only from the calculator's groups.
    partial void OnShowSkipWorktreeChanged(bool value) => services.RefreshFiles();

    partial void OnShowUntrackedChanged(bool value)
    {
        if (!_reading)
        {
            services.RefreshFiles();
        }
    }
}

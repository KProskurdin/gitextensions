using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Xplat.Core.Presentation;
using GitExtensions.Xplat.Core.Settings;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  The view model of upstream's <c>FormApplyPatch</c> (rules in <see cref="ApplyPatchDialog"/>): a patch file
///  (<c>git apply</c> for a raw diff, <c>git am</c> for a mail) or a directory of patches applied, and while <c>git am</c>
///  stopped, its patches with the one being applied, Conflicts resolved, Skip, Abort, Add files and Solve conflicts. Git
///  runs through the caller with its output shown, as upstream's FormProcess.
/// </summary>
public sealed partial class ApplyPatchViewModel : DialogViewModel
{
    /// <summary>
    ///  The upstream form whose texts the window shows, by their names.
    /// </summary>
    public const string Category = "FormApplyPatch";

    private readonly GitModule _module;
    private readonly IAppPreferences _preferences;
    private readonly Func<string, Task<bool>>? _runGit;
    private readonly Action? _showConflicts;
    private readonly Action? _changed;

    /// <param name="module">The repository.</param>
    /// <param name="preferences">Upstream's two apply patch options.</param>
    /// <param name="runGit">Runs git with the arguments, showing its output; true when it succeeded.</param>
    /// <param name="showConflicts">Upstream's StartResolveConflictsDialog.</param>
    /// <param name="changed">Tells the browse window the repository changed (upstream's RepoChangedNotifier).</param>
    public ApplyPatchViewModel(IDialogHost host, GitModule module, IAppPreferences preferences,
        Func<string, Task<bool>>? runGit, Action? showConflicts, Action? changed)
        : base(host)
    {
        _module = module;
        _preferences = preferences;
        _runGit = runGit;
        _showConflicts = showConflicts;
        _changed = changed;

        // Upstream's MergePatch_Load.
        Title = Host.Translate(ApplyPatchDialog.ApplyPatchCaption) + " (" + module.WorkingDir + ")";
        IgnoreWhitespace = preferences.ApplyPatchIgnoreWhitespace;
        SignOff = preferences.ApplyPatchSignOff;
    }

    public string Title { get; }

    /// <summary>
    ///  A patch file is applied; false for a directory of patches.
    /// </summary>
    [ObservableProperty]
    public partial bool IsFileMode { get; set; } = true;

    [ObservableProperty]
    public partial string? PatchFile { get; set; }

    [ObservableProperty]
    public partial string? PatchDir { get; set; }

    [ObservableProperty]
    public partial bool IgnoreWhitespace { get; set; }

    [ObservableProperty]
    public partial bool SignOff { get; set; }

    /// <summary>
    ///  Upstream's <c>EnableButtons</c>: which controls are enabled and which button is highlighted.
    /// </summary>
    [ObservableProperty]
    public partial ApplyPatchButtons Buttons { get; private set; } = ApplyPatchDialog.Buttons(false, false, true);

    /// <summary>
    ///  The patches of the <c>git am</c> in progress.
    /// </summary>
    [ObservableProperty]
    public partial IReadOnlyList<RebasePatch> Patches { get; private set; } = [];

    /// <summary>
    ///  The last refresh of the buttons and patches, for tests.
    /// </summary>
    public Task Refreshed { get; private set; } = Task.CompletedTask;

    /// <summary>
    ///  Picks a .patch file; the window sets it, as the picker is the platform's.
    /// </summary>
    public Func<Task<string?>>? PickPatchFile { get; set; }

    /// <summary>
    ///  Picks the directory of patches; the window sets it.
    /// </summary>
    public Func<Task<string?>>? PickPatchDir { get; set; }

    /// <summary>
    ///  Opens upstream's FormAddFiles; the window sets it, as it opens a window.
    /// </summary>
    public Func<Task>? ShowAddFiles { get; set; }

    /// <summary>
    ///  Upstream's <c>SetPatchFile</c>.
    /// </summary>
    public void SetPatchFile(string name)
    {
        IsFileMode = true;
        PatchFile = name;
    }

    /// <summary>
    ///  Upstream's <c>SetPatchDir</c>.
    /// </summary>
    public void SetPatchDir(string name)
    {
        IsFileMode = false;
        PatchDir = name;
    }

    /// <summary>
    ///  Reads the state of the <c>git am</c> again; the window calls it when it is activated, as upstream calls
    ///  <c>EnableButtons</c> when the conflicts window returns.
    /// </summary>
    public void Refresh() => Run(() => Refreshed = RefreshAsync());

    partial void OnIsFileModeChanged(bool value) => Refresh();

    partial void OnIgnoreWhitespaceChanged(bool value) => _preferences.ApplyPatchIgnoreWhitespace = value;

    partial void OnSignOffChanged(bool value) => _preferences.ApplyPatchSignOff = value;

    // Upstream's EnableButtons and the PatchGrid's refresh.
    private async Task RefreshAsync()
    {
        List<RebasePatch> skipped = [.. ApplyPatchDialog.Skipped];
        (bool inPatch, bool conflicted, IReadOnlyList<RebasePatch> patches) = await Task.Run(() =>
        {
            bool patching = _module.InTheMiddleOfPatch();
            return (patching, _module.InTheMiddleOfConflictedMerge(),
                patching ? RebasePatches.Read(_module, skipped) : (IReadOnlyList<RebasePatch>)[]);
        });
        Buttons = ApplyPatchDialog.Buttons(inPatch, conflicted, IsFileMode);
        Patches = patches;
    }

    // Upstream's BrowsePatch_Click: a .patch file, or the box as it was.
    [RelayCommand]
    private Task BrowsePatchAsync() => GuardAsync(async () =>
    {
        if (PickPatchFile is not null && await PickPatchFile() is { } path)
        {
            PatchFile = path;
        }
    });

    // Upstream's BrowseDir_Click.
    [RelayCommand]
    private Task BrowseDirAsync() => GuardAsync(async () =>
    {
        if (PickPatchDir is not null && await PickPatchDir() is { } folder)
        {
            PatchDir = folder;
        }
    });

    // Upstream's Apply_Click: the file through git (apply or am) or the directory's patches streamed to git am; the window
    // closes unless git am stopped.
    [RelayCommand]
    private Task ApplyAsync() => GuardAsync(async () =>
    {
        string patchFile = PatchFile ?? "";
        string directory = PatchDir ?? "";
        bool ignoreWhitespace = IgnoreWhitespace;
        bool signOff = SignOff;
        if (patchFile.Length == 0 && directory.Length == 0)
        {
            await Host.ShowMessageAsync(Host.Translate(ApplyPatchDialog.NoFileSelected),
                Host.Translate(ManageWorktreeDialog.Error));
            return;
        }

        ApplyPatchDialog.Skipped.Clear();
        if (IsFileMode)
        {
            ArgumentString arguments = await Task.Run(() =>
                ApplyPatchDialog.FileArguments(patchFile, signOff, ignoreWhitespace, _module.GetPathForGitExecution));
            if (_runGit is not null)
            {
                await _runGit(arguments.ToString());
            }
        }
        else
        {
            ArgumentString arguments = Commands.ApplyMailboxPatch(signOff, ignoreWhitespace);
            await Task.Run(() => _module.ApplyPatch(directory, arguments));
        }

        _changed?.Invoke();
        await (Refreshed = RefreshAsync());
        if (!await Task.Run(() => _module.InTheMiddleOfAction() || _module.InTheMiddleOfPatch()))
        {
            Host.Close(null);
        }
    });

    // Upstream's Skip_Click: the patch being applied is remembered as skipped.
    [RelayCommand]
    private Task SkipAsync() => GuardAsync(async () =>
    {
        if (Patches.FirstOrDefault(patch => patch.IsNext) is { } applying)
        {
            applying.IsSkipped = true;
            ApplyPatchDialog.Skipped.Add(applying);
        }

        await RunStepAsync(Commands.Skip());
    });

    // Upstream's Resolved_Click.
    [RelayCommand]
    private Task ResolvedAsync() => GuardAsync(() => RunStepAsync(Commands.Resolved()));

    // Upstream's Abort_Click.
    [RelayCommand]
    private Task AbortAsync() => GuardAsync(async () =>
    {
        await RunStepAsync(Commands.Abort());
        ApplyPatchDialog.Skipped.Clear();
        await (Refreshed = RefreshAsync());
    });

    // Upstream's AddFiles_Click.
    [RelayCommand]
    private Task AddFilesAsync() => GuardAsync(() => ShowAddFiles?.Invoke() ?? Task.CompletedTask);

    // Upstream's Mergetool_Click and SolveMergeConflicts_Click.
    [RelayCommand]
    private void SolveConflicts() => _showConflicts?.Invoke();

    private async Task RunStepAsync(ArgumentString arguments)
    {
        if (_runGit is not null)
        {
            await _runGit(arguments.ToString());
        }

        await (Refreshed = RefreshAsync());
    }
}

using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;
using GitExtensions.Xplat.Ui;
using GitExtUtils.GitUI.Theming;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormApplyPatch</c> (rules in <see cref="ApplyPatchDialog"/>): a patch file (<c>git apply</c> for a
///  raw diff, <c>git am</c> for a mail) or a directory of patches applied, and while <c>git am</c> stopped, its patches
///  with the one being applied, Conflicts resolved, Skip, Abort, Add files and Solve conflicts. Git runs through the
///  caller with its output shown, as upstream's FormProcess.
/// </summary>
public partial class ApplyPatchWindow : Window
{
    private readonly string _repositoryPath;
    private readonly IAppPreferences _preferences;
    private readonly Func<string, Task<bool>>? _runGit;
    private readonly Action? _showConflicts;
    private readonly Action? _changed;
    private readonly GitModule _module;
    private Task _refreshed = Task.CompletedTask;

    public ApplyPatchWindow()
        : this("", AppServices.Preferences, null, null, null)
    {
    }

    /// <param name="repositoryPath">The repository.</param>
    /// <param name="preferences">Upstream's two apply patch options.</param>
    /// <param name="runGit">Runs git with the arguments, showing its output; true when it succeeded.</param>
    /// <param name="showConflicts">Upstream's StartResolveConflictsDialog.</param>
    /// <param name="changed">Tells the browse window the repository changed (upstream's RepoChangedNotifier).</param>
    public ApplyPatchWindow(string repositoryPath, IAppPreferences preferences, Func<string, Task<bool>>? runGit,
        Action? showConflicts, Action? changed)
    {
        _repositoryPath = repositoryPath;
        _preferences = preferences;
        _runGit = runGit;
        _showConflicts = showConflicts;
        _changed = changed;
        _module = GitModules.Open(repositoryPath);
        InitializeComponent();
        UpstreamTranslation.Apply(this, "FormApplyPatch");
        InitialFocus.Attach(this, () => PatchFile);

        // Upstream's MergePatch_Load.
        Title = UpstreamTranslation.ByText(ApplyPatchDialog.ApplyPatchCaption) + " (" + _module.WorkingDir + ")";
        IgnoreWhitespace.IsChecked = preferences.ApplyPatchIgnoreWhitespace;
        SignOff.IsChecked = preferences.ApplyPatchSignOff;
        IgnoreWhitespace.IsCheckedChanged += (_, _) => _preferences.ApplyPatchIgnoreWhitespace = IgnoreWhitespace.IsChecked == true;
        SignOff.IsCheckedChanged += (_, _) => _preferences.ApplyPatchSignOff = SignOff.IsChecked == true;
        PatchFileMode.IsCheckedChanged += (_, _) => Refresh();
        BrowsePatch.Click += (_, _) => Run(BrowsePatchAsync);
        BrowseDir.Click += (_, _) => Run(BrowseDirAsync);
        Apply.Click += (_, _) => Run(ApplyAsync);
        Skip.Click += (_, _) => Run(SkipAsync);
        Resolved.Click += (_, _) => Run(() => RunStepAsync(Commands.Resolved()));
        Abort.Click += (_, _) => Run(AbortAsync);
        AddFiles.Click += (_, _) => Run(() => new AddFilesWindow(null, _runGit).ShowDialog(this));
        Mergetool.Click += (_, _) => _showConflicts?.Invoke();
        SolveMergeConflicts.Click += (_, _) => _showConflicts?.Invoke();

        // Upstream calls EnableButtons when the conflicts window returns; this window is told by being activated again.
        Activated += (_, _) => Refresh();
    }

    /// <summary>
    ///  The last refresh of the buttons and patches, for tests.
    /// </summary>
    public Task Refreshed => _refreshed;

    /// <summary>
    ///  Upstream's <c>SetPatchFile</c>.
    /// </summary>
    public void SetPatchFile(string name)
    {
        PatchFileMode.IsChecked = true;
        PatchFile.Text = name;
    }

    /// <summary>
    ///  Upstream's <c>SetPatchDir</c>.
    /// </summary>
    public void SetPatchDir(string name)
    {
        PatchDirMode.IsChecked = true;
        PatchDir.Text = name;
    }

    private void Run(Func<Task> action)
        => UiActions.Run(action, ex => _ = new ErrorWindow(ex.Message).ShowDialog(this));

    private void Refresh() => Run(() => _refreshed = RefreshAsync());

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
        ApplyPatchButtons buttons = ApplyPatchDialog.Buttons(inPatch, conflicted, PatchFileMode.IsChecked == true);
        Apply.IsEnabled = IgnoreWhitespace.IsEnabled = SignOff.IsEnabled = buttons.Apply;
        PatchFileMode.IsEnabled = PatchDirMode.IsEnabled = buttons.Apply;
        PatchFile.IsEnabled = BrowsePatch.IsEnabled = buttons.FileBox;
        PatchDir.IsEnabled = BrowseDir.IsEnabled = buttons.DirectoryBox;
        AddFiles.IsEnabled = buttons.AddFiles;
        Resolved.IsEnabled = buttons.Resolved;
        Mergetool.IsEnabled = buttons.Mergetool;
        Skip.IsEnabled = buttons.Skip;
        Abort.IsEnabled = buttons.Abort;
        SolveMergeConflicts.IsVisible = buttons.UnresolvedConflicts;

        string resolved = UpstreamTranslation.ByText(ApplyPatchDialog.ConflictResolved);
        string mergetool = UpstreamTranslation.ByText(ApplyPatchDialog.ConflictMergetool);
        ResolvedText.Text = UpstreamTranslation.WithAccessKey(buttons.Default == ApplyPatchDefault.Resolved
            ? ApplyPatchDialog.Highlighted(resolved)
            : resolved);
        MergetoolText.Text = UpstreamTranslation.WithAccessKey(buttons.Default == ApplyPatchDefault.Mergetool
            ? ApplyPatchDialog.Highlighted(mergetool)
            : mergetool);
        Resolved.IsDefault = buttons.Default == ApplyPatchDefault.Resolved;
        Mergetool.IsDefault = buttons.Default == ApplyPatchDefault.Mergetool;
        IBrush frame = (IBrush?)this.FindResource("SystemControlForegroundBaseHighBrush") ?? Brushes.Black;
        ContinuePanel.Background = buttons.Default == ApplyPatchDefault.Resolved ? frame : Brushes.Transparent;
        MergeToolPanel.Background = buttons.Default == ApplyPatchDefault.Mergetool ? frame : Brushes.Transparent;
        ShowPatches(patches);
    }

    // The PatchGrid's DisplayPatches: the patch being applied selected, in OrangeRed.
    private void ShowPatches(IReadOnlyList<RebasePatch> patches)
    {
        System.Drawing.Color red = System.Drawing.Color.OrangeRed.AdaptForeColor(ThemeBrushes.Current.IsDark
            ? System.Drawing.Color.FromArgb(0x20, 0x20, 0x20)
            : System.Drawing.Color.White);
        IBrush applying = new SolidColorBrush(Color.FromArgb(red.A, red.R, red.G, red.B));
        List<RebasePatchRow> rows = [.. patches.Select(patch => new RebasePatchRow(patch, patch.IsNext ? applying : null))];
        PatchGrid.ItemsSource = rows;
        PatchGrid.SelectedItem = rows.FirstOrDefault(row => row.Patch.IsNext);
    }

    // Upstream's BrowsePatch_Click: a .patch file, or the box as it was.
    private async Task BrowsePatchAsync()
    {
        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = UpstreamTranslation.ByText(ApplyPatchDialog.SelectPatchFileCaption),
            FileTypeFilter = [new FilePickerFileType(UpstreamTranslation.ByText(ApplyPatchDialog.SelectPatchFileFilter))
            {
                Patterns = ["*.patch"],
            }],
        });
        if (files.FirstOrDefault()?.TryGetLocalPath() is { } path)
        {
            PatchFile.Text = path;
        }
    }

    // Upstream's BrowseDir_Click.
    private async Task BrowseDirAsync()
    {
        IReadOnlyList<IStorageFolder> folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions());
        if (folders.FirstOrDefault()?.TryGetLocalPath() is { } folder)
        {
            PatchDir.Text = folder;
        }
    }

    // Upstream's Apply_Click: the file through git (apply or am) or the directory's patches streamed to git am; the window
    // closes unless git am stopped.
    private async Task ApplyAsync()
    {
        string patchFile = PatchFile.Text ?? "";
        string directory = PatchDir.Text ?? "";
        bool ignoreWhitespace = IgnoreWhitespace.IsChecked == true;
        bool signOff = SignOff.IsChecked == true;
        if (patchFile.Length == 0 && directory.Length == 0)
        {
            await new MessageBoxWindow(UpstreamTranslation.ByText(ApplyPatchDialog.NoFileSelected),
                UpstreamTranslation.ByText(ManageWorktreeDialog.Error), MessageBoxButtons.OK).ShowDialog(this);
            return;
        }

        ApplyPatchDialog.Skipped.Clear();
        if (PatchFileMode.IsChecked == true)
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
        await (_refreshed = RefreshAsync());
        if (!await Task.Run(() => _module.InTheMiddleOfAction() || _module.InTheMiddleOfPatch()))
        {
            Close();
        }
    }

    // Upstream's Skip_Click: the patch being applied is remembered as skipped.
    private async Task SkipAsync()
    {
        if (PatchGrid.ItemsSource is IEnumerable<RebasePatchRow> rows
            && rows.FirstOrDefault(row => row.Patch.IsNext) is { } applying)
        {
            applying.Patch.IsSkipped = true;
            ApplyPatchDialog.Skipped.Add(applying.Patch);
        }

        await RunStepAsync(Commands.Skip());
    }

    // Upstream's Abort_Click.
    private async Task AbortAsync()
    {
        await RunStepAsync(Commands.Abort());
        ApplyPatchDialog.Skipped.Clear();
        await (_refreshed = RefreshAsync());
    }

    private async Task RunStepAsync(ArgumentString arguments)
    {
        if (_runGit is not null)
        {
            await _runGit(arguments.ToString());
        }

        await (_refreshed = RefreshAsync());
    }
}

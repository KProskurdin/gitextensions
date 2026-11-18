using CommunityToolkit.Mvvm.ComponentModel;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Settings;

namespace GitExtensions.Xplat.Core.Diff;

/// <summary>
///  What a file list's window gives its menu, as upstream's <c>BindContextMenu</c> does: each flag says that the window
///  gave the action (or that it applies to the selection).
/// </summary>
public sealed record FileListMenuContext(
    bool HasSubmodules = false,
    bool HasOwnStage = false,
    bool HasServices = false,
    bool HasFileHistory = false,
    bool HasShowInFileTree = false,
    bool CanShowInFileTree = false,
    bool HasFilterFileInGrid = false,
    bool HasBlame = false,
    bool? BlameChecked = null,
    bool HasFindFile = false,
    bool HasGitGrep = false,
    bool GitGrepBoxVisible = false,
    bool CanMove = false);

/// <summary>
///  A file list shown as a tree, for upstream's tree items: whether a selected node has nodes below it, and whether a root
///  folder is expanded.
/// </summary>
public sealed record FileListTreeState(bool HasSubnodes = false, bool AnyRootExpanded = false);

/// <summary>
///  Upstream FileStatusList's item menu without a control (PLAN.md principle 6), its state: which items show, which are
///  enabled, their texts and checks, from the selection's <see cref="DiffFileMenuState"/> (upstream's
///  <c>UpdateStatusOfMenuItems</c>), the remembered file's items (<c>OpenWithDifftool_DropDownOpening</c>), what the window
///  gave the menu and the tree. Without a selection only the tree items and "Run script" show. The menu's actions are
///  still the view's.
/// </summary>
public sealed partial class FileListMenuViewModel : ObservableObject
{
    private readonly IAppPreferences _preferences;
    private readonly bool _isWindows;
    private readonly Func<bool> _visualStudioInstalled;
    private readonly Func<string, string> _withAccessKey;
    private DiffFileMenuState? _state;
    private DiffRememberState? _remember;
    private FileListMenuContext _context = new();
    private FileListTreeState _tree = new();

    /// <param name="isWindows">Upstream's "with..." and Visual Studio items show on Windows only.</param>
    /// <param name="withAccessKey">Upstream's menu text with its access key.</param>
    /// <param name="diffMergeTools">The diff tools offered under the difftool items.</param>
    public FileListMenuViewModel(IAppPreferences preferences, bool isWindows, Func<bool>? visualStudioInstalled = null,
        Func<string, string>? withAccessKey = null, IDiffMergeToolCatalog? diffMergeTools = null)
    {
        _preferences = preferences;
        _isWindows = isWindows;
        _visualStudioInstalled = visualStudioInstalled ?? (() => false);
        _withAccessKey = withAccessKey ?? (text => text.Replace('&', '_'));

        // Upstream's LoadCustomDifftools: these items, each with what opens the selection in a given tool.
        DiffTools = new CustomDiffToolsViewModel(preferences, diffMergeTools ?? new GitDiffMergeToolCatalog());
        DiffFirstToSelectedTools = DiffTools.Add(tool => GuardAsync(() => OpenWithDifftoolAsync(RevisionDiffKind.DiffAB, tool)));
        DiffSelectedToLocalTools = DiffTools.Add(tool => GuardAsync(() => OpenWithDifftoolAsync(RevisionDiffKind.DiffBLocal, tool)));
        DiffFirstToLocalTools = DiffTools.Add(tool => GuardAsync(() => OpenWithDifftoolAsync(RevisionDiffKind.DiffALocal, tool)));
        DiffWithRememberedTools = DiffTools.Add(tool => GuardAsync(() => DiffWithRememberedAsync(tool)));
        DiffTwoSelectedTools = DiffTools.Add(tool => GuardAsync(() => DiffTwoSelectedAsync(tool)));
    }

    /// <summary>
    ///  The diff tools under the difftool items, read when the menu first opens (<see cref="CustomDiffToolsViewModel.LoadAsync"/>).
    /// </summary>
    public CustomDiffToolsViewModel DiffTools { get; }

    /// <summary>
    ///  "First -> Second" and its tools; also what "Open with difftool" itself runs (upstream's <c>OpenFilesWithDiffTool</c>).
    /// </summary>
    public CustomDiffToolMenu DiffFirstToSelectedTools { get; }

    public CustomDiffToolMenu DiffSelectedToLocalTools { get; }

    public CustomDiffToolMenu DiffFirstToLocalTools { get; }

    public CustomDiffToolMenu DiffWithRememberedTools { get; }

    public CustomDiffToolMenu DiffTwoSelectedTools { get; }

    public bool HasSelection => _state is not null;

    public bool AreTreeItemsVisible => _tree.HasSubnodes;

    public bool IsCollapseRootFoldersVisible => _tree.AnyRootExpanded;

    public bool IsOpenSubmoduleVisible => _context.HasSubmodules && _state is { OpenSubmodule: true };

    /// <summary>
    ///  Upstream's <c>DisableSubmoduleMenuItemBold</c>: opening the submodule is the double-click's action, except where a
    ///  double-click stages (the commit window).
    /// </summary>
    public bool IsOpenSubmoduleDefault => OpensSubmodule && !_context.HasOwnStage;

    /// <summary>
    ///  A double-click on the selection opens its submodule (<c>OpenSubmoduleDiffInSeparateWindow</c>).
    /// </summary>
    public bool OpensSubmodule => _state is { OpenSubmodule: true } && _preferences.OpenSubmoduleDiffInSeparateWindow;

    public bool AreSubmoduleItemsVisible => _context.HasSubmodules && _state is { Submodule: true };

    public bool IsStageVisible => _state is { Stage: true };

    public bool IsUnstageVisible => _state is { Unstage: true };

    public bool IsResetFileToEnabled => _state is { ResetFile: true };

    public string? ResetToSecondHeader => _state?.ResetToSecond;

    public bool IsResetToSecondVisible => _state?.ResetToSecond is not null;

    public string? ResetToFirstHeader => _state?.ResetToFirst;

    public bool IsResetToFirstVisible => _state?.ResetToFirst is not null;

    public bool IsCherryPickVisible => _state is { CherryPick: true } && _context.HasServices;

    public bool IsInteractivePatchVisible => _state is { InteractiveAddReset: true };

    public bool IsOpenWithDifftoolEnabled => _state is { OpenWithDifftool: true };

    public string? DiffSecondCaption => _state?.DiffSecondCaption;

    public string? DiffFirstCaption => _state?.DiffFirstCaption;

    public bool IsDiffFirstToSelectedEnabled => _state is { DiffFirstToSelected: true };

    public bool IsDiffSelectedToLocalEnabled => _state is { DiffSelectedToLocal: true };

    public bool IsDiffFirstToLocalEnabled => _state is { DiffFirstToLocal: true };

    public bool IsToLocalVisible => _state is { ShowToLocal: true };

    public bool IsRememberSeparatorVisible => _remember is { Separator: true };

    public bool IsDiffTwoSelectedVisible => _remember is { DiffTwoSelected: true };

    public bool IsDiffTwoSelectedEnabled => _remember is { DiffTwoSelectedEnabled: true };

    public bool IsDiffWithRememberedVisible => _remember?.DiffWithRemembered is not null;

    public string? DiffWithRememberedHeader => _remember?.DiffWithRemembered is { } text ? _withAccessKey(text) : null;

    public bool IsDiffWithRememberedEnabled => _remember is { DiffWithRememberedEnabled: true };

    public bool IsRememberSecondVisible => _remember is { RememberSecond: true };

    public bool IsRememberSecondEnabled => _remember is { RememberSecondEnabled: true };

    public bool IsRememberFirstVisible => _remember is { RememberFirst: true };

    public bool IsRememberFirstEnabled => _remember is { RememberFirstEnabled: true };

    public bool IsOpenWorkingFileVisible => _state is { OpenWorkingFile: true };

    public bool IsOpenRevisionFileVisible => _state is { OpenRevisionFile: true };

    public bool IsOpenRevisionFileEnabled => _state is { OpenRevisionFileEnabled: true };

    // Upstream's "with..." items show the system's "Open with" dialog, which Windows has.
    public bool IsOpenWorkingFileWithVisible => _isWindows && IsOpenWorkingFileVisible;

    public bool IsOpenRevisionFileWithVisible => _isWindows && IsOpenRevisionFileVisible;

    public bool IsEditWorkingFileVisible => _state is { EditWorkingFile: true };

    public bool IsOpenInVisualStudioVisible => IsEditWorkingFileVisible && _isWindows && _visualStudioInstalled();

    public bool IsSaveAsVisible => _state is { SaveAs: true };

    public bool IsMoveVisible => _state is { RenameMove: true } && _context.CanMove;

    public string? DeleteFileHeader => _state?.DeleteFileText;

    public bool IsDeleteFileVisible => _state is { DeleteFile: true };

    public bool IsFileSeparatorVisible => _state is { FileSeparator: true };

    public bool IsCopyPathsEnabled => _state is { CopyPaths: true };

    public bool IsShowInFolderVisible => _state is { ShowInFolder: true };

    public bool IsShowInFolderEnabled => _state is { ShowInFolderEnabled: true };

    // Upstream shows "Show in File tree" and "Filter file in grid" only where BindContextMenu gave them an action, and makes
    // File history the default item there.
    public bool IsShowInFileTreeVisible
        => _context.HasShowInFileTree && _state is { ShowInFileTree: true } && _context.CanShowInFileTree;

    public bool IsFilterFileInGridVisible => HasSelection && _context.HasFilterFileInGrid;

    public bool IsFilterFileInGridEnabled => _state is { FilterFileInGrid: true };

    public bool IsFileHistoryEnabled => _state is { FileHistory: true } && _context.HasFileHistory;

    public bool IsFileHistoryDefault => HasSelection && _context.HasShowInFileTree && !OpensSubmodule;

    public bool IsBlameEnabled => _state is { Blame: true } && (_context.HasBlame || _context.HasFileHistory);

    /// <summary>
    ///  Upstream's <c>tsmiBlame.Checked</c> (the File tree tab): Blame is a check item there.
    /// </summary>
    public bool IsBlameCheckable => _context.BlameChecked is not null;

    public bool IsBlameChecked => _context.BlameChecked ?? false;

    public bool IsFindFileVisible => HasSelection && _context.HasFindFile;

    public bool IsGitGrepVisible => HasSelection && _context.HasGitGrep;

    public bool IsGitGrepBoxShown => _context.GitGrepBoxVisible;

    public bool IsIgnoreSeparatorVisible => _state is { IgnoreSeparator: true };

    public bool IsAddToIgnoreVisible => _state is { AddToIgnore: true };

    public bool IsStopTrackingVisible => _state is { StopTracking: true };

    public bool AreIndexFlagsVisible => _state is { IndexFlags: true };

    public bool IsSkipWorktreeChecked => _state is { SkipWorktreeChecked: true };

    public bool IsAssumeUnchangedChecked => _state is { AssumeUnchangedChecked: true };

    /// <summary>
    ///  Upstream's <c>UpdateStatusOfMenuItems</c>: the items for the selection's state (null without a selection), the
    ///  remembered file's items, what the window gave and the tree.
    /// </summary>
    public void Refresh(DiffFileMenuState? state, DiffRememberState? remember, FileListMenuContext context,
        FileListTreeState tree)
    {
        _state = state;
        _remember = state is null ? null : remember;
        _context = context;
        _tree = tree;

        // Every property above follows these.
        OnPropertyChanged(string.Empty);
    }
}

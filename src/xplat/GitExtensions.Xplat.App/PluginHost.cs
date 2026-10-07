using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Plugins;
using GitExtensions.Extensibility.Settings;
using GitExtensions.Xplat.Core.Operations;
using GitUIPluginInterfaces;
using Form = System.Windows.Forms.Form;
using Image = System.Drawing.Image;
using IWin32Window = System.Windows.Forms.IWin32Window;

namespace GitExtensions.Xplat.App;

/// <summary>
///  What the plugins see of the app: the new shell's <see cref="IGitUICommands"/> (upstream's <c>GitUICommands</c>) for one
///  window. It keeps the current repository's module, raises upstream's events where the shell has the matching action,
///  and registers the plugins with the repository, as upstream's <c>FormBrowse</c> does. Dialogs the shell does not have
///  yet throw <see cref="NotSupportedException"/>, which the Plugins menu shows as an error.
/// </summary>
internal sealed class PluginHost : IGitUICommands
{
    /// <summary>
    ///  The name of the embedded PNG icon of the plugins built for the new shell (src/xplat/plugins).
    /// </summary>
    public const string IconResourceName = "GitExtensions.Xplat.PluginIcon.png";

    private readonly Window _owner;
    private readonly RepositoryOperationsViewModel _actions;
    private readonly Action<IGitPlugin?> _showSettings;
    private readonly Action _showCommit;
    private IReadOnlyList<IGitPlugin> _registered = [];

    public PluginHost(Window owner, RepositoryOperationsViewModel actions, Action<IGitPlugin?> showSettings,
        Action showCommit, IGitModule? module = null)
    {
        _owner = owner;
        _actions = actions;
        _showSettings = showSettings;
        _showCommit = showCommit;
        Module = module ?? CreateModule("");

        // As upstream: a change notice from any thread (a plugin's background fetch) refreshes the window on the UI thread.
        RepoChangedNotifier = new ActionNotifier(() =>
            Dispatcher.UIThread.Post(() => PostRepositoryChanged?.Invoke(this, new GitUIEventArgs(Owner, this))));
    }

    public event EventHandler<GitUIEventArgs>? PostBrowseInitialize;

    public event EventHandler<GitUIPostActionEventArgs>? PostCheckoutBranch;

    public event EventHandler<GitUIPostActionEventArgs>? PostCheckoutRevision;

    public event EventHandler<GitUIPostActionEventArgs>? PostCommit;

    public event EventHandler<GitUIPostActionEventArgs>? PostEditGitIgnore;

    public event EventHandler<GitUIEventArgs>? PostRegisterPlugin;

    public event EventHandler<GitUIEventArgs>? PostRepositoryChanged;

    public event EventHandler<GitUIPostActionEventArgs>? PostSettings;

    public event EventHandler<GitUIPostActionEventArgs>? PostUpdateSubmodules;

    public event EventHandler<GitUIEventArgs>? PreCheckoutBranch;

    public event EventHandler<GitUIEventArgs>? PreCheckoutRevision;

    public event EventHandler<GitUIEventArgs>? PreCommit;

    public IBrowseRepo? BrowseRepo { get; set; }

    public IGitModule Module { get; private set; }

    public ILockableNotifier RepoChangedNotifier { get; }

    /// <summary>
    ///  The window, as the owner upstream's plugins pass to message boxes and dialogs.
    /// </summary>
    public IWin32Window Owner => new WindowOwner(_owner);

    /// <summary>
    ///  The plugins registered with the current repository.
    /// </summary>
    public IReadOnlyList<IGitPlugin> Registered => _registered;

    /// <summary>
    ///  Registers <paramref name="plugins"/> with the repository at <paramref name="repositoryPath"/> (none: the dashboard),
    ///  after unregistering them from the previous one, as upstream's <c>FormBrowse.SetGitModule</c> and
    ///  <c>RegisterPlugins</c>. A plugin that throws is skipped, as upstream skips it.
    /// </summary>
    public void Register(IReadOnlyList<IGitPlugin> plugins, string? repositoryPath)
    {
        Unregister();
        Module = CreateModule(repositoryPath ?? "");
        List<IGitPlugin> registered = [];
        foreach (IGitPlugin plugin in plugins)
        {
            if (TryCall(() => plugin.Register(this)))
            {
                registered.Add(plugin);
            }
        }

        _registered = registered;
        PostRegisterPlugin?.Invoke(this, new GitUIEventArgs(Owner, this));
    }

    public void Unregister()
    {
        foreach (IGitPlugin plugin in _registered)
        {
            TryCall(() => plugin.Unregister(this));
        }

        _registered = [];
    }

    /// <summary>
    ///  Runs a plugin from the Plugins menu; true when the plugin asks for a refresh.
    /// </summary>
    public bool Execute(IGitPlugin plugin) => plugin.Execute(new GitUIEventArgs(Owner, this));

    /// <summary>
    ///  Runs the registered plugin called <paramref name="name"/> for a script's plugin command, as upstream's script runner:
    ///  the name is matched ignoring case, a plugin that asks for a refresh gets one, and false means no plugin has the name.
    /// </summary>
    public bool ExecuteByName(string name)
    {
        IGitPlugin? plugin = _registered.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, name, StringComparison.CurrentCultureIgnoreCase));
        if (plugin is null)
        {
            return false;
        }

        if (Execute(plugin))
        {
            RepoChangedNotifier.Notify();
        }

        return true;
    }

    /// <summary>
    ///  Raises a "before" event; false when a plugin cancelled the action.
    /// </summary>
    public bool RaisePre(PluginEvent pluginEvent)
    {
        GitUIEventArgs args = new(Owner, this);
        (pluginEvent switch
        {
            PluginEvent.Commit => PreCommit,
            PluginEvent.CheckoutBranch => PreCheckoutBranch,
            PluginEvent.CheckoutRevision => PreCheckoutRevision,
            _ => null,
        })?.Invoke(this, args);
        return !args.Cancel;
    }

    /// <summary>
    ///  Raises an "after" event, with whether the action was done.
    /// </summary>
    public void RaisePost(PluginEvent pluginEvent, bool actionDone)
    {
        GitUIPostActionEventArgs args = new(Owner, this, actionDone);
        (pluginEvent switch
        {
            PluginEvent.Commit => PostCommit,
            PluginEvent.CheckoutBranch => PostCheckoutBranch,
            PluginEvent.CheckoutRevision => PostCheckoutRevision,
            PluginEvent.Settings => PostSettings,
            PluginEvent.UpdateSubmodules => PostUpdateSubmodules,
            PluginEvent.EditGitIgnore => PostEditGitIgnore,
            _ => null,
        })?.Invoke(this, args);
    }

    /// <summary>
    ///  The icon of a plugin built for the new shell, or null.
    /// </summary>
    public static Bitmap? LoadIcon(IGitPlugin plugin)
    {
        try
        {
            using Stream? stream = plugin.GetType().Assembly.GetManifestResourceStream(IconResourceName);
            return stream is null ? null : new Bitmap(stream);
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    public object? GetService(Type serviceType) => null;

    public void RaisePostBrowseInitialize(IWin32Window? owner)
        => PostBrowseInitialize?.Invoke(this, new GitUIEventArgs(owner, this));

    public void RaisePostRegisterPlugin(IWin32Window? owner)
        => PostRegisterPlugin?.Invoke(this, new GitUIEventArgs(owner, this));

    // Upstream wraps the action in its pre and post events only when given; here there are none to raise.
    public bool DoActionOnRepo(Func<bool> action) => action();

    public bool StartSettingsDialog(IGitPlugin gitPlugin)
    {
        _showSettings(gitPlugin);
        return true;
    }

    public bool StartSettingsDialog(IWin32Window? owner, SettingsPageReference? initialPage = null)
    {
        _showSettings(null);
        return true;
    }

    public bool StartSettingsDialog(Type pageType)
    {
        _showSettings(null);
        return true;
    }

    public bool StartGeneralSettingsDialog(IWin32Window? owner) => StartSettingsDialog(owner);

    public bool StartPluginSettingsDialog(IWin32Window? owner) => StartSettingsDialog(owner);

    public bool StartCommitDialog(IWin32Window? owner, string? commitMessage = null, bool showOnlyWhenChanges = false)
    {
        _showCommit();
        return true;
    }

    // Upstream waits for the process dialog to close; the new shell's process window runs on, and the result is not known
    // here, so the call reports that the command was started.
    public bool StartCommandLineProcessDialog(IWin32Window? owner, string? command, ArgumentString arguments)
    {
        if (string.IsNullOrEmpty(command))
        {
            return false;
        }

        string workingDir = Module.WorkingDir.Length > 0 ? Module.WorkingDir : Environment.CurrentDirectory;
        UiActions.Run(() => _actions.RunProgramAsync(command, command, arguments.ToString(), workingDir),
            ex => ShowError(ex.Message));
        return true;
    }

    public bool StartCommandLineProcessDialog(IWin32Window? owner, IGitCommand command)
        => StartGitCommandProcessDialog(owner, command.Arguments);

    public bool StartGitCommandProcessDialog(IWin32Window? owner, ArgumentString arguments)
        => StartCommandLineProcessDialog(owner, AppSettings.GitCommand, arguments);

    public IGitUICommands WithGitModule(IGitModule module)
        => new PluginHost(_owner, _actions, _showSettings, _showCommit, module);

    public IGitUICommands WithWorkingDirectory(string? workingDirectory)
        => WithGitModule(CreateModule(workingDirectory ?? ""));

    // Commit templates from plugins need the commit window's template menu, which the new shell does not have yet.
    public void AddCommitTemplate(string key, Func<string> addingText, Image? icon, bool isRegex = false)
    {
    }

    public void RemoveCommitTemplate(string key)
    {
    }

    public bool RunCommand(IReadOnlyList<string> args) => false;

    public void AddUpstreamRemote(IWin32Window? owner, IRepositoryHostPlugin gitHoster) => throw NotAvailable();

    public IGitRemoteCommand CreateRemoteCommand() => throw NotAvailable();

    public void OpenWithDifftool(IWin32Window? owner, IReadOnlyList<GitRevision?> revisions, string fileName,
        string? oldFileName, RevisionDiffKind diffKind, bool isTracked, string? customTool = null)
        => throw NotAvailable();

    public void ShowModelessForm(IWin32Window? owner, bool requiresValidWorkingDir,
        EventHandler<GitUIEventArgs>? preEvent, EventHandler<GitUIPostActionEventArgs>? postEvent,
        Func<Form> provideForm)
        => throw NotAvailable();

    public bool StartAddFilesDialog(IWin32Window? owner, string? addFiles = null) => throw NotAvailable();

    public bool StartAddToGitIgnoreDialog(IWin32Window? owner, bool localExclude, params string[] filePattern)
        => throw NotAvailable();

    public bool StartAmendCommitDialog(IWin32Window? owner, GitRevision revision) => throw NotAvailable();

    public bool StartApplyPatchDialog(IWin32Window? owner, string? patchFile = null) => throw NotAvailable();

    public bool StartArchiveDialog(IWin32Window? owner = null, GitRevision? revision = null,
        GitRevision? revision2 = null, string? path = null) => throw NotAvailable();

    public void StartBatchFileProcessDialog(string batchFile) => throw NotAvailable();

    public bool StartBrowseDialog(IWin32Window? owner, BrowseArguments? args = null) => throw NotAvailable();

    public bool StartCheckoutBranch(IWin32Window? owner, IReadOnlyList<ObjectId>? containObjectIds)
        => throw NotAvailable();

    public bool StartCheckoutBranch(IWin32Window? owner, string branch = "", bool remote = false,
        IReadOnlyList<ObjectId>? containObjectIds = null) => throw NotAvailable();

    public bool StartCheckoutRemoteBranch(IWin32Window? owner, string branch) => throw NotAvailable();

    public bool StartCheckoutRevisionDialog(IWin32Window? owner, string? revision = null) => throw NotAvailable();

    public bool StartCherryPickDialog(IWin32Window? owner = null, GitRevision? revision = null) => throw NotAvailable();

    public bool StartCherryPickDialog(IWin32Window? owner, IEnumerable<GitRevision> revisions) => throw NotAvailable();

    public bool StartCleanupRepositoryDialog(IWin32Window? owner = null, string? path = null) => throw NotAvailable();

    public bool StartCloneDialog(IWin32Window? owner, string url, EventHandler<GitModuleEventArgs> gitModuleChanged)
        => throw NotAvailable();

    public bool StartCloneDialog(IWin32Window? owner, string? url = null, bool openedFromProtocolHandler = false,
        EventHandler<GitModuleEventArgs>? gitModuleChanged = null) => throw NotAvailable();

    public void StartCloneForkFromHoster(IWin32Window? owner, IRepositoryHostPlugin gitHoster,
        EventHandler<GitModuleEventArgs>? gitModuleChanged) => throw NotAvailable();

    public bool StartCompareRevisionsDialog(IWin32Window? owner = null) => throw NotAvailable();

    public bool StartCreateBranchDialog(IWin32Window? owner = null, ObjectId objectId = default,
        string? newBranchNamePrefix = null) => throw NotAvailable();

    public bool StartCreateBranchDialog(IWin32Window? owner, string? branch) => throw NotAvailable();

    public void StartCreatePullRequest(IWin32Window? owner) => throw NotAvailable();

    public void StartCreatePullRequest(IWin32Window? owner, IRepositoryHostPlugin gitHoster,
        string? chooseRemote = null,
        string? chooseBranch = null) => throw NotAvailable();

    public bool StartCreateTagDialog(IWin32Window? owner = null, GitRevision? revision = null) => throw NotAvailable();

    public bool StartDeleteBranchDialog(IWin32Window? owner, IEnumerable<string> branches) => throw NotAvailable();

    public bool StartDeleteBranchDialog(IWin32Window? owner, string branch) => throw NotAvailable();

    public bool StartDeleteRemoteBranchDialog(IWin32Window? owner, string remoteBranch) => throw NotAvailable();

    public bool StartDeleteTagDialog(IWin32Window? owner, string? tag) => throw NotAvailable();

    public bool StartEditGitAttributesDialog(IWin32Window? owner = null) => throw NotAvailable();

    public bool StartEditGitIgnoreDialog(IWin32Window? owner, bool localExcludes) => throw NotAvailable();

    public bool StartFileEditorDialog(string? filename, bool showWarning = false, int? lineNumber = null)
        => throw NotAvailable();

    public void StartFileHistoryDialog(IWin32Window? owner, string fileName, GitRevision? revision = null,
        bool filterByRevision = false, bool showBlame = false) => throw NotAvailable();

    public bool StartFixupCommitDialog(IWin32Window? owner, GitRevision revision) => throw NotAvailable();

    public bool StartFormCommitDiff(ObjectId objectId) => throw NotAvailable();

    public bool StartFormatPatchDialog(IWin32Window? owner = null) => throw NotAvailable();

    public bool StartInitializeDialog(IWin32Window? owner = null, string? dir = null,
        EventHandler<GitModuleEventArgs>? gitModuleChanged = null) => throw NotAvailable();

    public bool StartInteractiveRebase(IWin32Window? owner, string onto) => throw NotAvailable();

    public bool StartMailMapDialog(IWin32Window? owner = null) => throw NotAvailable();

    public bool StartMergeBranchDialog(IWin32Window? owner, string? branch) => throw NotAvailable();

    public bool StartPullDialog(IWin32Window? owner = null, string? remoteBranch = null, string? remote = null,
        GitPullAction pullAction = GitPullAction.None) => throw NotAvailable();

    public bool StartPullDialogAndPullImmediately(IWin32Window? owner = null, string? remoteBranch = null,
        string? remote = null, GitPullAction pullAction = GitPullAction.None) => throw NotAvailable();

    public bool StartPullDialogAndPullImmediately(out bool pullCompleted, IWin32Window? owner = null,
        string? remoteBranch = null, string? remote = null, GitPullAction pullAction = GitPullAction.None)
        => throw NotAvailable();

    public void StartPullRequestsDialog(IWin32Window? owner, IRepositoryHostPlugin gitHoster) => throw NotAvailable();

    public bool StartPushDialog(IWin32Window? owner, bool pushOnShow) => throw NotAvailable();

    public bool StartPushDialog(IWin32Window? owner, bool pushOnShow, bool forceWithLease, out bool pushCompleted,
        string? branchName = null) => throw NotAvailable();

    public bool StartRebase(IWin32Window? owner, string onto) => throw NotAvailable();

    public bool StartRebaseDialog(IWin32Window? owner, string? from, string? to, string? onto, bool interactive = false,
        bool startRebaseImmediately = true) => throw NotAvailable();

    public bool StartRebaseDialog(IWin32Window? owner, string? onto) => throw NotAvailable();

    public bool StartRebaseDialogWithAdvOptions(IWin32Window? owner, string onto, string from = "")
        => throw NotAvailable();

    public bool StartRemotesDialog(IWin32Window? owner, string? preselectRemote = null, string? preselectLocal = null)
        => throw NotAvailable();

    public bool StartRenameDialog(IWin32Window? owner, string branch) => throw NotAvailable();

    public bool StartRepoSettingsDialog(IWin32Window? owner) => StartSettingsDialog(owner);

    public bool StartResetChangesDialog(IWin32Window? owner, IReadOnlyCollection<GitItemStatus> workTreeFiles,
        bool onlyWorkTree) => throw NotAvailable();

    public bool StartResetCurrentBranchDialog(IWin32Window? owner, string branch) => throw NotAvailable();

    public bool StartResolveConflictsDialog(IWin32Window? owner = null, bool offerCommit = true)
        => throw NotAvailable();

    public bool StartRevertCommitDialog(IWin32Window? owner, GitRevision revision) => throw NotAvailable();

    public bool StartSparseWorkingCopyDialog(IWin32Window? owner) => throw NotAvailable();

    public bool StartSquashCommitDialog(IWin32Window? owner, GitRevision revision) => throw NotAvailable();

    public bool StartStashDialog(IWin32Window? owner = null, bool manageStashes = true, string? initialStash = null)
        => throw NotAvailable();

    public bool StartSubmodulesDialog(IWin32Window? owner) => throw NotAvailable();

    public bool StartSyncSubmodulesDialog(IWin32Window? owner) => throw NotAvailable();

    public bool StartTheContinueRebaseDialog(IWin32Window? owner) => throw NotAvailable();

    public bool StartUpdateSubmoduleDialog(IWin32Window? owner, string submoduleLocalPath, string submoduleParentPath)
        => throw NotAvailable();

    public bool StartUpdateSubmodulesDialog(IWin32Window? owner, string submoduleLocalPath = "")
        => throw NotAvailable();

    public bool StartVerifyDatabaseDialog(IWin32Window? owner = null) => throw NotAvailable();

    public bool StartViewPatchDialog(IWin32Window? owner, string? patchFile = null) => throw NotAvailable();

    public bool StartViewPatchDialog(string patchFile) => throw NotAvailable();

    public bool StashApply(IWin32Window? owner, string stashName) => throw NotAvailable();

    public bool StashDrop(IWin32Window? owner, string stashName) => throw NotAvailable();

    public bool StashPop(IWin32Window? owner, string stashName = "") => throw NotAvailable();

    public bool StashSave(IWin32Window? owner, bool includeUntrackedFiles, bool keepIndex = false, string message = "",
        IReadOnlyList<string>? selectedFiles = null) => throw NotAvailable();

    public bool StashStaged(IWin32Window? owner) => throw NotAvailable();

    public void UpdateSubmodules(IWin32Window? owner) => throw NotAvailable();

    public bool WorktreeCreate(IWin32Window? owner, string mainWorktreePath) => throw NotAvailable();

    public bool WorktreeDelete(IWin32Window? owner, string worktreePath) => throw NotAvailable();

    public bool WorktreeSwitch(IWin32Window? owner, string worktreePath) => throw NotAvailable();

    private static GitModule CreateModule(string workingDir)
        => new(new GitExecutorProvider(new GitDirectoryResolver()), workingDir);

    private static NotSupportedException NotAvailable(
        [System.Runtime.CompilerServices.CallerMemberName]
        string member = "")
        => new($"A plugin asked for '{member}', which this version of Git Extensions does not have yet.");

    private static bool TryCall(Action action)
    {
        try
        {
            action();
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"Plugin failed: {ex}");
            return false;
        }
    }

    private void ShowError(string message) => _ = new ErrorWindow(message).ShowDialog(_owner);
}

/// <summary>
///  The upstream plugin events the new shell raises.
/// </summary>
internal enum PluginEvent
{
    Commit,
    CheckoutBranch,
    CheckoutRevision,
    Settings,
    UpdateSubmodules,
    EditGitIgnore,
}

/// <summary>
///  An Avalonia window as the WinForms owner upstream code passes around.
/// </summary>
public sealed class WindowOwner(Window window) : IWin32Window
{
    public Window Window => window;

    public IntPtr Handle => window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
}

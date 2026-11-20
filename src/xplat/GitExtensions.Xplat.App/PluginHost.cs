using System.Reflection;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Plugins;
using GitExtensions.Extensibility.Settings;
using GitExtensions.Xplat.App.RepositoryHosts;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Plugins;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Ui;
using GitExtUtils;
using GitUI;
using GitUIPluginInterfaces;
using Form = System.Windows.Forms.Form;
using Image = System.Drawing.Image;
using IWin32Window = System.Windows.Forms.IWin32Window;

namespace GitExtensions.Xplat.App;

/// <summary>
///  What the plugins see of the app: the new shell's <see cref="IGitUICommands"/> (upstream's <c>GitUICommands</c>) for one
///  window. It keeps the current repository's module, raises upstream's events where the shell has the matching action,
///  and registers the plugins with the repository, as upstream's <c>FormBrowse</c> does. Its <c>Start...Dialog</c> methods run
///  the browse window's flows (<see cref="IPluginFlows"/>) and return their answer, as upstream's; only a WinForms form
///  (<c>ShowModelessForm</c>) cannot show, and throws <see cref="NotSupportedException"/>.
/// </summary>
internal sealed class PluginHost : IGitUICommands
{
    /// <summary>
    ///  The name of the embedded PNG icon of the plugins built for the new shell (src/xplat/plugins).
    /// </summary>
    public const string IconResourceName = "GitExtensions.Xplat.PluginIcon.png";

    private static readonly IGitExecutorProvider
        _executorProvider = PluginServices.Container.GetRequiredService<IGitExecutorProvider>();

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

    private static byte[]? PngOf(Image? image)
    {
        if (image is null)
        {
            return null;
        }

        try
        {
            using MemoryStream stream = new();
            image.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            return stream.ToArray();
        }
        catch (Exception ex) when (ex is PlatformNotSupportedException or TypeInitializationException
                                       or System.Runtime.InteropServices.ExternalException)
        {
            return null;
        }
    }

    private static byte[]? EmbeddedIcon(Assembly assembly)
    {
        using Stream? stream = assembly.GetManifestResourceStream(IconResourceName);
        if (stream is null)
        {
            return null;
        }

        using MemoryStream copy = new();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    /// <summary>
    ///  Upstream's services without WinForms (<see cref="PluginServices"/>): the git executor provider (the Statistics plugin
    ///  opens submodules with it), the file system, the git directory resolver, the repository description provider, the
    ///  app title generator, the link factory, the submodule status provider, the branch name normaliser and the trace
    ///  listener. Upstream's GitUI services are not available.
    /// </summary>
    public object? GetService(Type serviceType) => PluginServices.Container.GetService(serviceType);

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
        => new PluginHost(_owner, _actions, _showSettings, _showCommit, module)
        {
            NewCloneWindow = NewCloneWindow,
            Flows = Flows,
        };

    public IGitUICommands WithWorkingDirectory(string? workingDirectory)
        => WithGitModule(CreateModule(workingDirectory ?? ""));

    // As upstream: the commit window's "Commit templates" menu lists them with their icon. A plugin built for the new shell
    // has no System.Drawing images (they throw off Windows), so its embedded icon stands in: the plugin's own icon, which is
    // what upstream's GitHub plugin passes. NoInlining keeps the plugin as the calling assembly.
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void AddCommitTemplate(string key, Func<string> addingText, Image? icon, bool isRegex = false)
        => CommitTemplates.Register(key, addingText, isRegex, PngOf(icon) ?? EmbeddedIcon(Assembly.GetCallingAssembly()));

    public void RemoveCommitTemplate(string key) => CommitTemplates.Unregister(key);

    // Upstream's RunCommand: args[0] is the program, args[1] the verb, run as from the command line but on this window.
    public bool RunCommand(IReadOnlyList<string> args)
        => CommandLine.Parse([.. args.Skip(1)]) is { } request
           && RunFlow(owner: null, (f, _, _) => f.RunCommandAsync(request));

    // Upstream's AddUpstreamRemote: the plugin adds the fork's parent as "upstream", which is then fetched (upstream opens
    // its pull dialog to fetch it at once).
    public void AddUpstreamRemote(IWin32Window? owner, IRepositoryHostPlugin gitHoster)
        => WrapRepoHostingCall(RepositoryHostTexts.AddUpstreamRemote, gitHoster, host => UiActions.Run(
            async () =>
            {
                string? remoteName = await host.AddUpstreamRemoteAsync();
                if (!string.IsNullOrEmpty(remoteName))
                {
                    await _actions.FetchAsync(Module.WorkingDir, remoteName, prune: false);
                }
            },
            ex => ShowError(ex.Message)));

    public IGitRemoteCommand CreateRemoteCommand()
        => Flows is not null ? new RemoteCommand(this) : throw NotAvailable();

    public void OpenWithDifftool(IWin32Window? owner, IReadOnlyList<GitRevision?> revisions, string fileName,
        string? oldFileName, RevisionDiffKind diffKind, bool isTracked, string? customTool = null)
    {
        // As upstream: the revisions as the diff kind takes them, else the reason in a message.
        if (!RevisionDiffInfoProvider.TryGet(revisions, diffKind, out string? firstRevision, out string? secondRevision,
                out string? error))
        {
            ShowError(error ?? "");
            return;
        }

        Module.OpenWithDifftool(fileName, oldFileName, firstRevision, secondRevision, isTracked: isTracked,
            customTool: customTool);
    }

    // A WinForms form cannot show in the new shell (the shim's Form is no window); the plugins built for it show their
    // windows themselves (PluginDialog).
    public void ShowModelessForm(IWin32Window? owner, bool requiresValidWorkingDir,
        EventHandler<GitUIEventArgs>? preEvent, EventHandler<GitUIPostActionEventArgs>? postEvent,
        Func<Form> provideForm)
        => throw NotAvailable();

    public bool StartAddFilesDialog(IWin32Window? owner, string? addFiles = null)
        => RunFlow(owner, (f, o, p) => f.AddFilesAsync(o, p, addFiles));

    public bool StartAddToGitIgnoreDialog(IWin32Window? owner, bool localExclude, params string[] filePattern)
        => RunFlow(owner, (f, o, p) => f.AddToGitIgnoreAsync(o, p, localExclude, filePattern));

    public bool StartAmendCommitDialog(IWin32Window? owner, GitRevision revision)
        => RunFlow(owner, (f, _, p) => f.SpecialCommitAsync(p, CommitKind.Amend, revision.ObjectId));

    public bool StartApplyPatchDialog(IWin32Window? owner, string? patchFile = null)
        => RunFlow(owner, (f, o, p) => f.ApplyPatchAsync(o, p, patchFile));

    public bool StartArchiveDialog(IWin32Window? owner = null, GitRevision? revision = null,
        GitRevision? revision2 = null, string? path = null)
        => RunFlow(owner, (f, o, p) => f.ArchiveAsync(o, p, revision?.Guid ?? "HEAD", revision2?.Guid, path));

    /// <summary>
    ///  As upstream: writes <paramref name="batchFile"/> to a temporary file, runs it in the process window and deletes it.
    ///  Upstream runs a cmd.exe batch file; there is no cmd.exe off Windows, so there the text is a POSIX shell script, run
    ///  by sh, which the plugins built for the new shell write (e.g. FindLargeFiles). Upstream waits for the dialog; the
    ///  process window runs on.
    /// </summary>
    public void StartBatchFileProcessDialog(string batchFile)
    {
        bool windows = OperatingSystem.IsWindows();
        string tempFile = Path.Join(Path.GetTempPath(),
            $"GitExtensions-{Guid.NewGuid():N}{(windows ? ".cmd" : ".sh")}");
        File.WriteAllText(tempFile, windows ? $"@prompt $G{Environment.NewLine}{batchFile}" : batchFile);
        string program = windows ? "cmd.exe" : "/bin/sh";
        string arguments = windows ? $"/C \"{tempFile}\"" : $"\"{tempFile}\"";
        string workingDir = Module.WorkingDir.Length > 0 ? Module.WorkingDir : Environment.CurrentDirectory;
        UiActions.Run(
            async () =>
            {
                try
                {
                    await _actions.RunProgramAsync($"{program} {arguments}", program, arguments, workingDir);
                }
                finally
                {
                    File.Delete(tempFile);
                }
            },
            ex => ShowError(ex.Message));
    }

    public bool StartBrowseDialog(IWin32Window? owner, BrowseArguments? args = null)
        => RunFlow(owner, (f, _, p) => f.BrowseAsync(p, args?.RevFilter ?? "", args?.PathFilter ?? "",
            args is { SelectedId.IsZero: false } ? args.SelectedId.ToString() : null));

    public bool StartCheckoutBranch(IWin32Window? owner, IReadOnlyList<ObjectId>? containObjectIds)
        => RunFlow(owner, (f, o, p) => f.CheckoutBranchAsync(o, p, "", remote: false, containObjectIds));

    public bool StartCheckoutBranch(IWin32Window? owner, string branch = "", bool remote = false,
        IReadOnlyList<ObjectId>? containObjectIds = null)
        => RunFlow(owner, (f, o, p) => f.CheckoutBranchAsync(o, p, branch, remote, containObjectIds));

    public bool StartCheckoutRemoteBranch(IWin32Window? owner, string branch)
        => RunFlow(owner, (f, _, p) => f.CheckoutRemoteBranchAsync(p, branch));

    public bool StartCheckoutRevisionDialog(IWin32Window? owner, string? revision = null)
        => RunFlow(owner, (f, _, p) => f.CheckoutRevisionAsync(p, revision));

    public bool StartCherryPickDialog(IWin32Window? owner = null, GitRevision? revision = null)
        => RunFlow(owner, (f, o, p) => f.CherryPickAsync(o, p, [revision?.Guid ?? "HEAD"]));

    public bool StartCherryPickDialog(IWin32Window? owner, IEnumerable<GitRevision> revisions)
        => RunFlow(owner, (f, o, p) => f.CherryPickAsync(o, p, [.. revisions.Select(revision => revision.Guid)]));

    public bool StartCleanupRepositoryDialog(IWin32Window? owner = null, string? path = null)
        => RunFlow(owner, (f, o, p) => f.CleanupAsync(o, path ?? p));

    /// <summary>
    ///  The browse window's flows behind the <c>Start...Dialog</c> methods; set by the browse window.
    /// </summary>
    public IPluginFlows? Flows { get; set; }

    /// <summary>
    ///  Makes the browse window's clone window for a URL and who hears of the opened clone; set by the browse window.
    /// </summary>
    public Func<string?, EventHandler<GitModuleEventArgs>?, CloneWindow>? NewCloneWindow { get; set; }

    public bool StartCloneDialog(IWin32Window? owner, string url, EventHandler<GitModuleEventArgs> gitModuleChanged)
        => StartCloneDialog(owner, url, openedFromProtocolHandler: false, gitModuleChanged);

    public bool StartCloneDialog(IWin32Window? owner, string? url = null, bool openedFromProtocolHandler = false,
        EventHandler<GitModuleEventArgs>? gitModuleChanged = null)
    {
        if (NewCloneWindow is not { } newWindow)
        {
            throw NotAvailable();
        }

        _ = newWindow(url, gitModuleChanged).ShowDialog<bool>(OwnerOf(owner));
        return true;
    }

    // Upstream's StartCloneForkFromHoster. The clone opens in the window, as every clone does; gitModuleChanged hears of it
    // too, as upstream calls it.
    public void StartCloneForkFromHoster(IWin32Window? owner, IRepositoryHostPlugin gitHoster,
        EventHandler<GitModuleEventArgs>? gitModuleChanged)
        => WrapRepoHostingCall(RepositoryHostTexts.ForkCloneRepo, gitHoster, host =>
            _ = new ForkAndCloneWindow(host, _actions, DefaultCloneDestination(), gitModuleChanged).ShowDialog(
                OwnerOf(owner)));

    public bool StartCompareRevisionsDialog(IWin32Window? owner = null)
        => RunFlow(owner, (f, o, p) => f.CompareRevisionsAsync(o, p));

    public bool StartCreateBranchDialog(IWin32Window? owner = null, ObjectId objectId = default,
        string? newBranchNamePrefix = null)
        => RunFlow(owner, (f, o, p) =>
            f.CreateBranchAsync(o, p, objectId.IsZero ? null : objectId.ToString(), newBranchNamePrefix));

    public bool StartCreateBranchDialog(IWin32Window? owner, string? branch)
        => RunFlow(owner, (f, o, p) => f.CreateBranchAsync(o, p, branch, namePrefix: null));

    /// <summary>
    ///  True when a repository host plugin knows the open repository (upstream's <c>TryGetGitHosterForModule</c>).
    /// </summary>
    public bool HasRepositoryHost
        => _registered.OfType<IRepositoryHostPlugin>().Any(host => host.GitModuleIsRelevantToMe());

    // Upstream's StartCreatePullRequest: the one host plugin that knows the repository.
    public void StartCreatePullRequest(IWin32Window? owner)
    {
        List<IRepositoryHostPlugin> relevantHosts =
            [.. _registered.OfType<IRepositoryHostPlugin>().Where(host => host.GitModuleIsRelevantToMe())];
        if (relevantHosts.Count == 1)
        {
            StartCreatePullRequest(owner, relevantHosts[0]);
        }
        else
        {
            MessageBoxes.Show(owner, relevantHosts.Count == 0
                    ? "Could not find any repo hosts for current working directory"
                    : "StartCreatePullRequest:Selection not implemented!",
                RepositoryHostTexts.Error, System.Windows.Forms.MessageBoxButtons.OK,
                System.Windows.Forms.MessageBoxIcon.Error);
        }
    }

    // Upstream's StartCreatePullRequest; as upstream's form, the branch to choose is not used.
    public void StartCreatePullRequest(IWin32Window? owner, IRepositoryHostPlugin gitHoster,
        string? chooseRemote = null,
        string? chooseBranch = null)
        => WrapRepoHostingCall(RepositoryHostTexts.CreatePullRequest, gitHoster, host =>
            new CreatePullRequestWindow(host, Module, chooseRemote).Show(OwnerOf(owner)));

    public bool StartCreateTagDialog(IWin32Window? owner = null, GitRevision? revision = null)
        => RunFlow(owner, (f, o, _) => f.CreateTagAsync(o, revision?.Guid ?? "HEAD"));

    public bool StartDeleteBranchDialog(IWin32Window? owner, IEnumerable<string> branches)
        => RunFlow(owner, (f, _, p) => f.DeleteBranchesAsync(p, [.. branches]));

    public bool StartDeleteBranchDialog(IWin32Window? owner, string branch)
        => RunFlow(owner, (f, _, p) => f.DeleteBranchesAsync(p, [branch]));

    public bool StartDeleteRemoteBranchDialog(IWin32Window? owner, string remoteBranch)
        => RunFlow(owner, (f, _, p) => f.DeleteRemoteBranchAsync(p, remoteBranch));

    public bool StartDeleteTagDialog(IWin32Window? owner, string? tag)
        => RunFlow(owner, (f, _, p) => f.DeleteTagAsync(p, tag));

    public bool StartEditGitAttributesDialog(IWin32Window? owner = null)
        => RunFlow(owner, (f, _, p) => f.EditRepositoryFileAsync(p, RepositoryTextFiles.GitAttributes));

    public bool StartEditGitIgnoreDialog(IWin32Window? owner, bool localExcludes)
        => RunFlow(owner, (f, _, _) => f.EditGitIgnoreAsync(localExcludes));

    public bool StartFileEditorDialog(string? filename, bool showWarning = false, int? lineNumber = null)
        => filename is not null && RunFlow(owner: null, (f, o, _) => f.EditFileAsync(o, filename, showWarning));

    public void StartFileHistoryDialog(IWin32Window? owner, string fileName, GitRevision? revision = null,
        bool filterByRevision = false, bool showBlame = false)
        => _ = RunFlow(owner, (f, o, p) => f.ShowFileHistoryAsync(o, p, fileName, revision?.Guid, showBlame));

    public bool StartFixupCommitDialog(IWin32Window? owner, GitRevision revision)
        => RunFlow(owner, (f, _, p) => f.SpecialCommitAsync(p, CommitKind.Fixup, revision.ObjectId));

    public bool StartFormCommitDiff(ObjectId objectId)
        => RunFlow(owner: null, (f, o, p) => f.ShowCommitDiffAsync(o, p, objectId.ToString()));

    public bool StartFormatPatchDialog(IWin32Window? owner = null)
        => RunFlow(owner, (f, o, p) => f.FormatPatchAsync(o, p));

    public bool StartInitializeDialog(IWin32Window? owner = null, string? dir = null,
        EventHandler<GitModuleEventArgs>? gitModuleChanged = null)
        => RunFlow(owner, async (f, o, _) =>
        {
            if (await f.InitAsync(o, dir) is not { } folder)
            {
                return false;
            }

            gitModuleChanged?.Invoke(this, new GitModuleEventArgs(CreateModule(folder)));
            return true;
        });

    public bool StartInteractiveRebase(IWin32Window? owner, string onto)
        => RunFlow(owner, (f, _, p) => f.RebaseAsync(p, "", to: null, onto, interactive: true, immediately: true));

    public bool StartMailMapDialog(IWin32Window? owner = null)
        => RunFlow(owner, (f, _, p) => f.EditRepositoryFileAsync(p, RepositoryTextFiles.MailMap));

    public bool StartMergeBranchDialog(IWin32Window? owner, string? branch)
        => RunFlow(owner, (f, _, p) => f.MergeAsync(p, branch));

    public bool StartPullDialog(IWin32Window? owner = null, string? remoteBranch = null, string? remote = null,
        GitPullAction pullAction = GitPullAction.None)
        => RunFlow(owner, (f, o, _) => f.PullAsync(o, remoteBranch, remote, pullAction, pullAtOnce: false));

    public bool StartPullDialogAndPullImmediately(IWin32Window? owner = null, string? remoteBranch = null,
        string? remote = null, GitPullAction pullAction = GitPullAction.None)
        => RunFlow(owner, (f, o, _) => f.PullAsync(o, remoteBranch, remote, pullAction, pullAtOnce: true));

    public bool StartPullDialogAndPullImmediately(out bool pullCompleted, IWin32Window? owner = null,
        string? remoteBranch = null, string? remote = null, GitPullAction pullAction = GitPullAction.None)
        => pullCompleted = RunFlow(owner, (f, o, _) => f.PullAsync(o, remoteBranch, remote, pullAction, pullAtOnce: true));

    public void StartPullRequestsDialog(IWin32Window? owner, IRepositoryHostPlugin gitHoster)
        => WrapRepoHostingCall(RepositoryHostTexts.ViewPullRequest, gitHoster, host =>
            new ViewPullRequestsWindow(host, Module, _actions).Show(OwnerOf(owner)));

    public bool StartPushDialog(IWin32Window? owner, bool pushOnShow)
        => RunFlow(owner, (f, _, _) => f.PushAsync(pushOnShow, branch: null));

    public bool StartPushDialog(IWin32Window? owner, bool pushOnShow, bool forceWithLease, out bool pushCompleted,
        string? branchName = null)
        => pushCompleted = RunFlow(owner, (f, _, _) => f.PushAsync(pushOnShow, branchName));

    public bool StartRebase(IWin32Window? owner, string onto)
        => RunFlow(owner, (f, _, p) => f.RebaseAsync(p, "", to: null, onto, interactive: false, immediately: true));

    public bool StartRebaseDialog(IWin32Window? owner, string? from, string? to, string? onto, bool interactive = false,
        bool startRebaseImmediately = true)
        => RunFlow(owner, (f, _, p) => f.RebaseAsync(p, from ?? "", to, onto, interactive, startRebaseImmediately));

    public bool StartRebaseDialog(IWin32Window? owner, string? onto)
        => RunFlow(owner, (f, _, p) => f.RebaseAsync(p, "", to: null, onto, interactive: false, immediately: false));

    public bool StartRebaseDialogWithAdvOptions(IWin32Window? owner, string onto, string from = "")
        => RunFlow(owner, (f, _, p) => f.RebaseAsync(p, from, to: null, onto, interactive: false, immediately: false));

    public bool StartRemotesDialog(IWin32Window? owner, string? preselectRemote = null, string? preselectLocal = null)
        => RunFlow(owner, (f, o, _) => f.RemotesAsync(o, preselectRemote, preselectLocal));

    public bool StartRenameDialog(IWin32Window? owner, string branch)
        => RunFlow(owner, (f, _, _) => f.RenameBranchAsync(branch));

    public bool StartRepoSettingsDialog(IWin32Window? owner) => StartSettingsDialog(owner);

    public bool StartResetChangesDialog(IWin32Window? owner, IReadOnlyCollection<GitItemStatus> workTreeFiles,
        bool onlyWorkTree)
        => RunFlow(owner, (f, o, p) => f.ResetChangesAsync(o, p, hasExistingFiles: workTreeFiles.Any(file => !file.IsNew),
            hasNewFiles: workTreeFiles.Any(file => file.IsNew), onlyWorkTree));

    public bool StartResetCurrentBranchDialog(IWin32Window? owner, string branch)
        => RunFlow(owner, (f, _, p) => f.ResetCurrentBranchAsync(p, branch));

    public bool StartResolveConflictsDialog(IWin32Window? owner = null, bool offerCommit = true)
        => RunFlow(owner, (f, o, p) => f.ResolveConflictsAsync(o, p));

    public bool StartRevertCommitDialog(IWin32Window? owner, GitRevision revision)
        => RunFlow(owner, (f, o, p) => f.RevertCommitAsync(o, p, revision.Guid));

    public bool StartSparseWorkingCopyDialog(IWin32Window? owner)
        => RunFlow(owner, (f, _, p) => f.SparseWorkingCopyAsync(p));

    public bool StartSquashCommitDialog(IWin32Window? owner, GitRevision revision)
        => RunFlow(owner, (f, _, p) => f.SpecialCommitAsync(p, CommitKind.Squash, revision.ObjectId));

    public bool StartStashDialog(IWin32Window? owner = null, bool manageStashes = true, string? initialStash = null)
        => RunFlow(owner, (f, o, _) => f.StashWindowAsync(o, manageStashes, initialStash));

    public bool StartSubmodulesDialog(IWin32Window? owner)
        => RunFlow(owner, (f, _, p) => f.ManageSubmodulesAsync(p));

    public bool StartSyncSubmodulesDialog(IWin32Window? owner)
        => RunFlow(owner, (_, _, p) => _actions.SyncSubmodulesAsync(p, path: null));

    public bool StartTheContinueRebaseDialog(IWin32Window? owner)
        => RunFlow(owner, (f, _, p) => f.ContinueRebaseAsync(p));

    public bool StartUpdateSubmoduleDialog(IWin32Window? owner, string submoduleLocalPath, string submoduleParentPath)
        => RunFlow(owner, (f, _, _) => f.UpdateSubmodulesAsync(submoduleParentPath, submoduleLocalPath));

    public bool StartUpdateSubmodulesDialog(IWin32Window? owner, string submoduleLocalPath = "")
        => RunFlow(owner, (f, _, p) => f.UpdateSubmodulesAsync(p, submoduleLocalPath.Length == 0 ? null : submoduleLocalPath));

    public bool StartVerifyDatabaseDialog(IWin32Window? owner = null)
        => RunFlow(owner, (f, _, p) => f.VerifyDatabaseAsync(p));

    public bool StartViewPatchDialog(IWin32Window? owner, string? patchFile = null)
        => RunFlow(owner, (f, o, p) => f.ViewPatchAsync(o, p, patchFile));

    public bool StartViewPatchDialog(string patchFile)
        => RunFlow(owner: null, (f, o, p) => f.ViewPatchAsync(o, p, patchFile));

    public bool StashApply(IWin32Window? owner, string stashName)
        => RunFlow(owner, (_, _, p) => _actions.ApplyStashAsync(p, stashName));

    public bool StashDrop(IWin32Window? owner, string stashName)
        => RunFlow(owner, (_, _, p) => _actions.DropStashAsync(p, stashName));

    public bool StashPop(IWin32Window? owner, string stashName = "")
        => RunFlow(owner, (_, _, p) => _actions.PopStashAsync(p, stashName));

    public bool StashSave(IWin32Window? owner, bool includeUntrackedFiles, bool keepIndex = false, string message = "",
        IReadOnlyList<string>? selectedFiles = null)
        => RunFlow(owner, (_, _, p) => _actions.StashAsync(p, message, includeUntrackedFiles, keepIndex, selectedFiles));

    public bool StashStaged(IWin32Window? owner)
        => RunFlow(owner, (_, _, p) => _actions.StashStagedAsync(p));

    public void UpdateSubmodules(IWin32Window? owner)
        => _ = RunFlow(owner, (f, _, p) => f.UpdateSubmodulesAsync(p, submodulePath: null));

    public bool WorktreeCreate(IWin32Window? owner, string mainWorktreePath)
        => RunFlow(owner, (f, o, p) => f.WorktreeCreateAsync(o, p, mainWorktreePath));

    public bool WorktreeDelete(IWin32Window? owner, string worktreePath)
        => RunFlow(owner, (f, o, p) => f.WorktreeDeleteAsync(o, p, worktreePath));

    public bool WorktreeSwitch(IWin32Window? owner, string worktreePath)
        => RunFlow(owner, (f, o, _) => f.WorktreeSwitchAsync(o, worktreePath));

    // Upstream's WrapRepoHostingCall: a host that is not set up yet is run first (the GitHub plugin then opens its settings),
    // and a failure is shown with upstream's text.
    private void WrapRepoHostingCall(string name, IRepositoryHostPlugin gitHoster, Action<IRepositoryHostPlugin> call)
    {
        if (!gitHoster.ConfigurationOk)
        {
            gitHoster.Execute(new GitUIEventArgs(null, this));
        }

        if (!gitHoster.ConfigurationOk)
        {
            return;
        }

        try
        {
            call(gitHoster);
        }
        catch (Exception ex)
        {
            MessageBoxes.Show(Owner,
                string.Format("ERROR: {0} failed. Message: {1}\r\n\r\n{2}", name, ex.Message, ex.StackTrace),
                "Error! :(", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
        }
    }

    // Upstream starts in the default clone folder, or the folder of the most recent repository (usually the open one).
    private string DefaultCloneDestination()
        => AppServices.Preferences.DefaultCloneDestinationPath is { Length: > 0 } destination ? destination
            : Module.WorkingDir.Length > 0 ? Path.GetDirectoryName(Module.WorkingDir.TrimEnd('/', '\\')) ?? ""
            : "";

    private Window OwnerOf(IWin32Window? owner) => ModalWindow.OwnerWindow(owner) ?? _owner;

    private static GitModule CreateModule(string workingDir) => new(_executorProvider, workingDir);

    // Upstream's Start...Dialog methods show their dialog and return its answer once it has closed: the browse window's
    // flow for it runs on this host's repository in a nested dispatcher frame (ModalWindow.Wait). Without the browse
    // window's flows there is no such dialog.
    private bool RunFlow(IWin32Window? owner, Func<IPluginFlows, Window, string, Task<bool>> flow,
        [CallerMemberName] string member = "")
        => Flows is { } flows
            ? ModalWindow.Wait(() => flow(flows, OwnerOf(owner), Module.WorkingDir))
            : throw NotAvailable(member);

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

    // Upstream's GitRemoteCommand: a git command on a remote, run in the process window after Pageant for the remote's key;
    // Execute returns when it has finished.
    private sealed class RemoteCommand(PluginHost host) : IGitRemoteCommand
    {
        public event EventHandler<GitRemoteCommandCompletedEventArgs>? Completed;

        public object? OwnerForm { get; set; }

        public string? Remote { get; set; }

        public string? Title { get; set; }

        public string? CommandText { get; set; }

        public bool ErrorOccurred { get; private set; }

        public string? CommandOutput { get; private set; }

        public void Execute()
        {
            ErrorOccurred = !host.RunFlow(OwnerForm as IWin32Window,
                (flows, _, path) => flows.RunRemoteGitAsync(path, Remote, CommandText ?? ""));
            CommandOutput = string.Join(Environment.NewLine, host._actions.OutputLines);
            Completed?.Invoke(this, new GitRemoteCommandCompletedEventArgs(this, ErrorOccurred, handled: false));
        }
    }
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

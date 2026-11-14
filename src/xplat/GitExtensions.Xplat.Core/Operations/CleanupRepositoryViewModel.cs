using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Presentation;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  The view model of upstream's <c>FormCleanupRepository</c> (rules in <see cref="CleanupDialog"/>): <c>git clean</c> with
///  upstream's choices, previewed with a dry run, run after upstream's question; git's output goes to the log.
/// </summary>
public sealed partial class CleanupRepositoryViewModel : DialogViewModel
{
    /// <summary>
    ///  The upstream form whose texts the window shows, by their names.
    /// </summary>
    public const string Category = "FormCleanupRepository";

    private readonly IGitModule _module;
    private readonly Action? _cleaned;

    /// <param name="module">The repository to clean.</param>
    /// <param name="cleaned">Called after a cleanup ran, so the browse window reads the work tree again.</param>
    public CleanupRepositoryViewModel(IDialogHost host, IGitModule module, Action? cleaned)
        : base(host)
    {
        _module = module;
        _cleaned = cleaned;
    }

    /// <summary>
    ///  What to remove: upstream's three radio buttons.
    /// </summary>
    [ObservableProperty]
    public partial CleanMode Mode { get; set; } = CleanMode.All;

    [ObservableProperty]
    public partial bool RemoveDirectories { get; set; } = true;

    [ObservableProperty]
    public partial bool CleanSubmodules { get; set; }

    /// <summary>
    ///  Upstream's "Affect the following directory path(s) only".
    /// </summary>
    [ObservableProperty]
    public partial bool IncludePathFilter { get; set; }

    [ObservableProperty]
    public partial string? IncludePaths { get; set; }

    /// <summary>
    ///  Upstream's "Exclude the following file path(s)".
    /// </summary>
    [ObservableProperty]
    public partial bool ExcludePathFilter { get; set; }

    [ObservableProperty]
    public partial string? ExcludePaths { get; set; }

    /// <summary>
    ///  Upstream's log: git's output of the last run.
    /// </summary>
    [ObservableProperty]
    public partial string Output { get; private set; } = "";

    /// <summary>
    ///  The last run of git, for tests.
    /// </summary>
    public Task LastRun { get; private set; } = Task.CompletedTask;

    /// <summary>
    ///  Picks a folder starting in the given one; null when cancelled. The window sets it, as it is the platform's picker.
    /// </summary>
    public Func<string, Task<string?>>? PickFolder { get; set; }

    /// <summary>
    ///  Picks a file starting in the given folder; null when cancelled. The window sets it, as it is the platform's picker.
    /// </summary>
    public Func<string, Task<string?>>? PickFile { get; set; }

    [RelayCommand]
    private Task PreviewAsync() => GuardAsync(() => CleanUpAsync(dryRun: true));

    // Upstream's Cleanup_Click.
    [RelayCommand]
    private Task CleanupAsync() => GuardAsync(async () =>
    {
        if (await Host.ConfirmAsync(Host.Translate(CleanupDialog.ReallyCleanupQuestion),
                Host.Translate(CleanupDialog.ReallyCleanupQuestionCaption)))
        {
            await CleanUpAsync(dryRun: false);
            _cleaned?.Invoke();
        }
    });

    [RelayCommand]
    private void Close() => Host.Close(null);

    // Upstream's AddIncludePath_Click: a folder of the work tree, on a line of its own, with the filter on.
    [RelayCommand]
    private Task AddIncludePathAsync() => GuardAsync(async () =>
    {
        string? folder = PickFolder is null ? null : await PickFolder(_module.WorkingDir);
        if (CleanupDialog.AcceptsIncludePath(_module.WorkingDir, _module.WorkingDirGitDir, folder))
        {
            IncludePathFilter = true;
            IncludePaths = CleanupDialog.AddLine(IncludePaths, folder!);
        }
    });

    // Upstream's AddExcludePath_Click: a file of the work tree, relative to it, on a line of its own, with the filter on.
    [RelayCommand]
    private Task AddExcludePathAsync() => GuardAsync(async () =>
    {
        string? file = PickFile is null ? null : await PickFile(_module.WorkingDir);
        if (CleanupDialog.ExcludePath(_module.WorkingDir, file) is { } path)
        {
            ExcludePathFilter = true;
            ExcludePaths = CleanupDialog.AddLine(ExcludePaths, path);
        }
    });

    // Upstream's CleanUp: git clean, then the submodules', their output in the log.
    private Task CleanUpAsync(bool dryRun)
    {
        IReadOnlyList<ArgumentString> commands = CleanupDialog.Commands(Mode, dryRun, RemoveDirectories, CleanSubmodules,
            CleanupDialog.IncludePaths(IncludePathFilter, IncludePaths),
            CleanupDialog.ExcludePaths(ExcludePathFilter, ExcludePaths));
        return LastRun = RunCommandsAsync(commands);
    }

    private async Task RunCommandsAsync(IReadOnlyList<ArgumentString> commands)
    {
        Output = "";
        foreach (ArgumentString command in commands)
        {
            ExecutionResult result = await Task.Run(() => _module.GitExecutable.Execute(command, throwOnErrorExit: false));
            Output += result.StandardOutput + result.StandardError;
        }
    }
}

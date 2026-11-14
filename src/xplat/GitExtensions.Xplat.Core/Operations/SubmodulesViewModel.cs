using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitCommands;
using GitCommands.Config;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Presentation;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  The view model of upstream's <c>FormSubmodules</c> (Repository > Manage submodules...; rules in
///  <see cref="SubmodulesDialog"/>): the submodules with the selected one's details, adding one, and synchronizing,
///  updating, pulling or removing the selected one, each followed by a new read of the list.
/// </summary>
public sealed partial class SubmodulesViewModel : DialogViewModel
{
    /// <summary>
    ///  The upstream form whose texts the window shows, by their names.
    /// </summary>
    public const string Category = "FormSubmodules";

    private readonly IGitModule _module;
    private readonly Func<string, Task<bool>> _runGitWithOutput;
    private readonly Func<string, Task> _pull;
    private readonly Func<Task> _addSubmodule;

    /// <param name="module">The repository whose submodules are managed.</param>
    /// <param name="runGitWithOutput">Runs a git command in the repository with its output shown (upstream's FormProcess):
    /// synchronizing and updating a submodule.</param>
    /// <param name="pull">Opens the pull dialog on a submodule's work tree (upstream's <c>Pull_Click</c>).</param>
    /// <param name="addSubmodule">Opens upstream's <c>FormAddSubmodule</c>.</param>
    public SubmodulesViewModel(IDialogHost host, IGitModule module, Func<string, Task<bool>> runGitWithOutput,
        Func<string, Task> pull, Func<Task> addSubmodule)
        : base(host)
    {
        _module = module;
        _runGitWithOutput = runGitWithOutput;
        _pull = pull;
        _addSubmodule = addSubmodule;
    }

    [ObservableProperty]
    public partial IReadOnlyList<GitSubmoduleInfo> Submodules { get; private set; } = [];

    /// <summary>
    ///  The selected submodule, whose details show (upstream binds them to the selected row).
    /// </summary>
    [ObservableProperty]
    public partial GitSubmoduleInfo? Selected { get; set; }

    /// <summary>
    ///  True while the submodules are read; upstream shows the wait cursor.
    /// </summary>
    [ObservableProperty]
    public partial bool IsLoading { get; private set; }

    /// <summary>
    ///  The last read of the submodules, for tests.
    /// </summary>
    public Task Loaded { get; private set; } = Task.CompletedTask;

    /// <summary>
    ///  Upstream's <c>Initialize</c>: reads the submodules again, keeping the selected one selected (else the first, as its
    ///  binding source). The window calls it when it opens.
    /// </summary>
    public Task ReloadAsync() => Loaded = GuardAsync(async () =>
    {
        string? selectedName = Selected?.Name;
        IsLoading = true;
        try
        {
            IReadOnlyList<GitSubmoduleInfo> submodules = await Task.Run(() => SubmodulesDialog.Load(_module));
            Submodules = submodules;
            Selected = submodules.FirstOrDefault(info => info.Name == selectedName) ?? submodules.FirstOrDefault();
        }
        finally
        {
            IsLoading = false;
        }
    });

    // Upstream's AddSubmoduleClick: the add window, then the list read again.
    [RelayCommand]
    private Task AddSubmoduleAsync() => GuardAsync(async () =>
    {
        await _addSubmodule();
        _ = ReloadAsync();
    });

    // Upstream's SynchronizeSubmoduleClick: the command on the local path, with its output.
    [RelayCommand]
    private Task SynchronizeAsync() => RunOnSelectedAsync(GitCommands.Git.Commands.SubmoduleSync);

    // Upstream's UpdateSubmoduleClick.
    [RelayCommand]
    private Task UpdateAsync() => RunOnSelectedAsync(localPath => GitCommands.Git.Commands.SubmoduleUpdate(localPath));

    // Upstream's RemoveSubmoduleClick: after its warning question; .gitmodules that cannot be read is reported.
    [RelayCommand]
    private Task RemoveAsync() => GuardAsync(async () =>
    {
        if (Selected is not { } info
            || !await Host.ConfirmAsync(Host.Translate(SubmodulesDialog.RemoveSelectedSubmodule),
                Host.Translate(SubmodulesDialog.RemoveSelectedSubmoduleCaption)))
        {
            return;
        }

        try
        {
            await Task.Run(() => SubmodulesDialog.Remove(_module, info.Name, info.LocalPath));
        }
        catch (GitConfigurationException ex)
        {
            await Host.ShowMessageAsync(
                string.Format(ResourceManager.TranslatedStrings.GeneralGitConfigExceptionMessage, ex.ConfigPath,
                    Environment.NewLine, (ex.InnerException ?? ex).Message),
                ResourceManager.TranslatedStrings.GeneralGitConfigExceptionCaption);
            return;
        }

        _ = ReloadAsync();
    });

    // Upstream's Pull_Click: the pull dialog on the submodule, then the list read again.
    [RelayCommand]
    private Task PullAsync() => GuardAsync(async () =>
    {
        if (Selected is not { } info)
        {
            return;
        }

        await _pull(Path.Combine(_module.WorkingDir, info.LocalPath));
        _ = ReloadAsync();
    });

    private Task RunOnSelectedAsync(Func<string, ArgumentString> command) => GuardAsync(async () =>
    {
        if (Selected is not { } info)
        {
            return;
        }

        await _runGitWithOutput(command(info.LocalPath).ToString());
        _ = ReloadAsync();
    });
}

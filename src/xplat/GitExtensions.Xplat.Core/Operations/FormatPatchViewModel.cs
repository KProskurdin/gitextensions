using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Presentation;
using GitExtensions.Xplat.Core.Settings;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  The view model of upstream's <c>FormFormatPatch</c> (rules in <see cref="FormatPatchDialog"/>): <c>git format-patch</c>
///  of the commits selected in the grid into the output folder, which is kept (upstream's <c>LastFormatPatchDir</c>); git's
///  output is shown, or upstream's error when it made nothing. Closes its window after the patches were made.
/// </summary>
public sealed partial class FormatPatchViewModel : DialogViewModel
{
    /// <summary>
    ///  The upstream form whose texts the window shows, by their names.
    /// </summary>
    public const string Category = "FormFormatPatch";

    private readonly IGitModule _module;
    private readonly IAppPreferences _preferences;
    private readonly TaskCompletionSource _loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <param name="module">The repository.</param>
    /// <param name="preferences">Where upstream's <c>LastFormatPatchDir</c> is kept.</param>
    /// <param name="history">Reads the grid's commits.</param>
    public FormatPatchViewModel(IDialogHost host, IGitModule module, IAppPreferences preferences, ICommitHistory history)
        : base(host)
    {
        _module = module;
        _preferences = preferences;
        Commits = new CommitListViewModel(history);
        OutputPath = preferences.LastFormatPatchDir;
    }

    /// <summary>
    ///  The grid's commits.
    /// </summary>
    public CommitListViewModel Commits { get; }

    [ObservableProperty]
    public partial string? OutputPath { get; set; }

    /// <summary>
    ///  Upstream's <c>SelectedBranch</c> label: the current branch once read.
    /// </summary>
    [ObservableProperty]
    public partial string BranchText { get; private set; } = "Branch";

    /// <summary>
    ///  The commits selected in the grid; the window sets them, as the grid selects several.
    /// </summary>
    public IReadOnlyList<CommitRow> SelectedRows { get; set; } = [];

    /// <summary>
    ///  Picks the output folder; null when cancelled. The window sets it, as it is the platform's picker.
    /// </summary>
    public Func<Task<string?>>? PickFolder { get; set; }

    /// <summary>
    ///  Completes when the commits and the current branch are shown, for tests.
    /// </summary>
    public Task Loaded => _loaded.Task;

    /// <summary>
    ///  Upstream's <c>FormFormatPath_Load</c>: the current branch and the grid. The window calls it when it opens.
    /// </summary>
    public Task LoadAsync(string repositoryPath) => GuardAsync(async () =>
    {
        string branch = await Task.Run(() => _module.GetSelectedBranch());
        BranchText = Host.Translate(FormatPatchDialog.CurrentBranch) + " " + branch;
        await Commits.OpenAsync(repositoryPath);
        _loaded.TrySetResult();
    });

    // Upstream's OutputPath_TextChanged: a folder that exists is kept.
    partial void OnOutputPathChanged(string? value)
    {
        if (Directory.Exists(value))
        {
            _preferences.LastFormatPatchDir = value;
        }
    }

    // Upstream's Browse_Click: OsShellUtil.PickFolder.
    [RelayCommand]
    private Task BrowseAsync() => GuardAsync(async () =>
    {
        if (PickFolder is not null && await PickFolder() is { } folder)
        {
            OutputPath = folder;
        }
    });

    // Upstream's FormatPatch_Click.
    [RelayCommand]
    private Task FormatPatchAsync() => GuardAsync(async () =>
    {
        string output = OutputPath ?? "";
        if (output.Length == 0)
        {
            await ShowAsync(FormatPatchDialog.NoOutputPathEntered, ManageWorktreeDialog.Error);
            return;
        }

        // The selection oldest first, as upstream's SortDirection.Descending orders the grid's rows.
        List<(string Hash, string? FirstParent)> selected =
        [
            .. SelectedRows
                .OrderByDescending(row => Commits.IndexOfHash(row.Hash) ?? 0)
                .Select(row => (row.Hash, row.ParentHashes is [{ } parent, ..] ? parent : null))
        ];
        if (selected.Count == 0)
        {
            await ShowAsync(FormatPatchDialog.RevisionsNeeded, FormatPatchDialog.RevisionsNeededCaption);
            return;
        }

        IReadOnlyList<FormatPatchRange> ranges = FormatPatchDialog.Ranges(selected);
        string result = await Task.Run(() => string.Concat(ranges.Select(range =>
            _module.FormatPatch(range.From, range.To, output, range.Start))));
        if (string.IsNullOrEmpty(result))
        {
            await ShowAsync(FormatPatchDialog.FailCreatePatch, FormatPatchDialog.RevisionsNeededCaption);
            return;
        }

        await Host.ShowMessageAsync(result, Host.Translate(FormatPatchDialog.PatchResultCaption));
        Host.Close(null);
    });

    private Task ShowAsync(string text, string caption) => Host.ShowMessageAsync(Host.Translate(text), Host.Translate(caption));
}

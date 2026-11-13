using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Presentation;
using GitUIPluginInterfaces;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  The view model of upstream's <c>FormArchive</c> (rules in <see cref="ArchiveDialog"/>): archives a revision as zip or
///  tar, all of it, only some paths, or only the files changed since a second revision. Closes its window after starting
///  the archive.
/// </summary>
public sealed partial class ArchiveViewModel : DialogViewModel
{
    /// <summary>
    ///  The upstream form whose texts the window shows, by their names.
    /// </summary>
    public const string Category = "FormArchive";

    // Upstream's text of a second revision not chosen yet.
    private const string NoDiffRevision = "...";

    private readonly IGitModule _module;
    private readonly string? _revision;
    private readonly string? _diffRevision;
    private readonly string? _path;
    private readonly Func<string, Task>? _archive;
    private readonly Func<string?, Task<string?>> _chooseCommit;
    private readonly TaskCompletionSource _loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private GitRevision? _selectedRevision;
    private GitRevision? _diffSelectedRevision;

    /// <param name="revision">The revision to archive.</param>
    /// <param name="diffRevision">The revision to compare with (upstream's second selected revision), or null.</param>
    /// <param name="path">The path to archive alone (upstream's <c>SetPathArgument</c>), or null.</param>
    /// <param name="archive">Runs git with the archive arguments, showing its output (upstream's FormProcess).</param>
    /// <param name="chooseCommit">Upstream's FormChooseCommit on the given commit: the chosen commit's hash, or null.</param>
    /// <param name="pickOutput">Asks for the archive's file (the suggested name, the extension); null when cancelled.</param>
    public ArchiveViewModel(IDialogHost host, IGitModule module, string revision, string? diffRevision, string? path,
        Func<string, Task>? archive, Func<string?, Task<string?>> chooseCommit,
        Func<string, string, Task<string?>> pickOutput)
        : base(host)
    {
        _module = module;
        _revision = revision;
        _diffRevision = diffRevision;
        _path = path;
        _archive = archive;
        _chooseCommit = chooseCommit;
        PickOutput = pickOutput;
        Paths = "";
        IsZip = true;
        ShowDiffRevision();
    }

    /// <summary>
    ///  Asks for the archive's file: the suggested name and the extension; null when cancelled. The window's save picker;
    ///  tests replace it.
    /// </summary>
    public Func<string, string, Task<string?>> PickOutput { get; set; }

    /// <summary>
    ///  Upstream's <c>CommitSummaryUserControl</c> of the revision to archive; null when git does not know it.
    /// </summary>
    [ObservableProperty]
    public partial CommitSummary? Summary { get; private set; }

    /// <summary>
    ///  Upstream's "Archive specific paths only"; it and <see cref="RevisionFilter"/> exclude each other.
    /// </summary>
    [ObservableProperty]
    public partial bool PathFilter { get; set; }

    /// <summary>
    ///  The paths to archive, one per line.
    /// </summary>
    [ObservableProperty]
    public partial string Paths { get; set; }

    /// <summary>
    ///  Upstream's "Take the files that have changed from the revision above to this one and archive only those".
    /// </summary>
    [ObservableProperty]
    public partial bool RevisionFilter { get; set; }

    /// <summary>
    ///  The zip format; tar otherwise.
    /// </summary>
    [ObservableProperty]
    public partial bool IsZip { get; set; }

    /// <summary>
    ///  Upstream's second revision box: its header (the short hash), message, author and date caption.
    /// </summary>
    [ObservableProperty]
    public partial string DiffHeader { get; private set; } = NoDiffRevision;

    [ObservableProperty]
    public partial string DiffMessage { get; private set; } = NoDiffRevision;

    [ObservableProperty]
    public partial string DiffAuthor { get; private set; } = NoDiffRevision;

    [ObservableProperty]
    public partial string DiffDateCaption { get; private set; } = "";

    /// <summary>
    ///  Completes when the revisions are shown, for tests.
    /// </summary>
    public Task Loaded => _loaded.Task;

    /// <summary>
    ///  Upstream's <c>StartArchiveDialog</c>: the revision, the second one with its filter checked, and the path with its
    ///  own. The window calls it when it opens.
    /// </summary>
    public Task LoadAsync() => GuardAsync(async () =>
    {
        try
        {
            await SelectRevisionAsync(_revision);
            if (!string.IsNullOrEmpty(_path))
            {
                PathFilter = true;
                Paths = _path;
            }

            if (_diffRevision is not null)
            {
                RevisionFilter = true;
                _diffSelectedRevision = await ReadRevisionAsync(_diffRevision);
                ShowDiffRevision();
            }
        }
        finally
        {
            _loaded.TrySetResult();
        }
    });

    // Upstream's checkBoxPathFilter_CheckedChanged: the two filters exclude each other.
    partial void OnPathFilterChanged(bool value)
    {
        if (value)
        {
            RevisionFilter = false;
        }
    }

    // Upstream's checkboxRevisionFilter_CheckedChanged.
    partial void OnRevisionFilterChanged(bool value)
    {
        if (value)
        {
            PathFilter = false;
        }
    }

    // Upstream's btnChooseRevision_Click.
    [RelayCommand]
    private Task ChooseRevisionAsync() => GuardAsync(async () =>
    {
        if (await _chooseCommit(_selectedRevision?.Guid) is { } hash)
        {
            await SelectRevisionAsync(hash);
        }
    });

    // Upstream's btnDiffChooseRevision_Click.
    [RelayCommand]
    private Task ChooseDiffRevisionAsync() => GuardAsync(async () =>
    {
        if (await _chooseCommit(_diffSelectedRevision?.Guid) is { } hash)
        {
            _diffSelectedRevision = await ReadRevisionAsync(hash);
            ShowDiffRevision();
        }
    });

    // Upstream's Save_Click.
    [RelayCommand]
    private Task SaveAsync() => GuardAsync(async () =>
    {
        if (RevisionFilter && _diffSelectedRevision is null)
        {
            await Host.ShowMessageAsync(Host.Translate(ArchiveDialog.NoRevisionSelected),
                Host.Translate(ManageWorktreeDialog.Error));
            return;
        }

        string revision = _selectedRevision?.Guid ?? "";
        bool zip = IsZip;
        string suggestion = ArchiveDialog.FileNameSuggestion(_module.WorkingDir, revision, PathFilter, Paths);
        if (await PickOutput(suggestion, zip ? "zip" : "tar") is not { } output)
        {
            return;
        }

        string paths = await PathArgumentAsync();
        if (_archive is not null)
        {
            await _archive(ArchiveDialog.Arguments(zip, revision, output, paths));
        }

        Host.Close(null);
    });

    private async Task SelectRevisionAsync(string? revision)
    {
        _selectedRevision = await ReadRevisionAsync(revision);
        Summary = _selectedRevision is null ? null : CreateBranchDialog.Summarize(_selectedRevision);
    }

    private Task<GitRevision?> ReadRevisionAsync(string? revision)
        => Task.Run(() => revision is null || _module.RevParse(revision) is not { IsZero: false } id
            ? null
            : _module.GetRevision(id, shortFormat: true, loadRefs: true));

    // Upstream's DiffSelectedRevision setter.
    private void ShowDiffRevision()
    {
        string dateCaption = $"{ResourceManager.TranslatedStrings.CommitDate}:";
        GitRevision? revision = _diffSelectedRevision;
        DiffDateCaption = revision is null ? dateCaption : $"{dateCaption} {revision.CommitDate}";
        DiffAuthor = revision?.Author ?? NoDiffRevision;
        DiffHeader = revision?.ObjectId.ToShortString() ?? NoDiffRevision;
        DiffMessage = revision?.Subject ?? NoDiffRevision;
    }

    // Upstream's GetPathArgumentFromGui.
    private Task<string> PathArgumentAsync()
    {
        if (PathFilter)
        {
            return Task.FromResult(ArchiveDialog.PathArgument(Paths));
        }

        if (!RevisionFilter)
        {
            return Task.FromResult("");
        }

        string? first = _diffSelectedRevision?.Guid;
        string? second = _selectedRevision?.Guid;
        return Task.Run(() => ArchiveDialog.ChangedFilesArgument(_module
            .GetDiffFilesWithUntracked(first, second, StagedStatus.None, noCache: false, cancellationToken: default)
            .Where(file => !file.IsDeleted)
            .Select(file => file.Name)));
    }
}

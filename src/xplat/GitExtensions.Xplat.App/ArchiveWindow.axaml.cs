using Avalonia.Controls;
using Avalonia.Platform.Storage;
using GitCommands;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;
using GitExtensions.Xplat.Ui;
using GitUIPluginInterfaces;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormArchive</c> (rules in <see cref="ArchiveDialog"/>): the revision to archive (another one chosen in
///  upstream's FormChooseCommit), the paths or the files changed since a second revision, zip or tar, and Save as, which
///  asks for the file and runs <c>git archive</c> through the caller.
/// </summary>
public partial class ArchiveWindow : Window
{
    private readonly string _repositoryPath;
    private readonly string? _revision;
    private readonly string? _diffRevision;
    private readonly string? _path;
    private readonly IAppPreferences _preferences;
    private readonly Func<string, Task>? _archive;
    private readonly GitModule _module;
    private readonly TaskCompletionSource _loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private GitRevision? _selectedRevision;
    private GitRevision? _diffSelectedRevision;

    public ArchiveWindow()
        : this("", "HEAD", null, null, AppServices.Preferences, null)
    {
    }

    /// <param name="repositoryPath">The repository.</param>
    /// <param name="revision">The revision to archive.</param>
    /// <param name="diffRevision">The revision to compare with (upstream's second selected revision), or null.</param>
    /// <param name="path">The path to archive alone (upstream's <c>SetPathArgument</c>), or null.</param>
    /// <param name="preferences">The commit chooser's grid settings.</param>
    /// <param name="archive">Runs git with the archive arguments, showing its output (upstream's FormProcess).</param>
    public ArchiveWindow(string repositoryPath, string revision, string? diffRevision, string? path,
        IAppPreferences preferences, Func<string, Task>? archive)
    {
        _repositoryPath = repositoryPath;
        _revision = revision;
        _diffRevision = diffRevision;
        _path = path;
        _preferences = preferences;
        _archive = archive;
        _module = GitModules.Open(repositoryPath);
        InitializeComponent();
        UpstreamTranslation.Apply(this, "FormArchive");
        InitialFocus.Attach(this, () => buttonArchiveRevision);
        checkBoxPathFilter.IsCheckedChanged += (_, _) => PathFilterChanged();
        checkboxRevisionFilter.IsCheckedChanged += (_, _) => RevisionFilterChanged();
        PathFilterChanged();
        RevisionFilterChanged();
        ShowDiffRevision();
        btnChooseRevision.Click += (_, _) => Run(ChooseRevisionAsync);
        btnDiffChooseRevision.Click += (_, _) => Run(ChooseDiffRevisionAsync);
        buttonArchiveRevision.Click += (_, _) => Run(SaveAsync);
        Opened += (_, _) => Run(LoadAsync);
    }

    /// <summary>
    ///  Asks for the archive's file: the suggested name and the extension; null when cancelled. The save picker by default;
    ///  tests replace it.
    /// </summary>
    public Func<string, string, Task<string?>>? PickOutput { get; set; }

    /// <summary>
    ///  Completes when the revisions are shown, for tests.
    /// </summary>
    public Task RevisionsLoaded => _loaded.Task;

    private void Run(Func<Task> action)
        => UiActions.Run(action, ex => _ = new ErrorWindow(ex.Message).ShowDialog(this));

    // Upstream's StartArchiveDialog: the revision, the second one with its filter checked, and the path with its own.
    private async Task LoadAsync()
    {
        await SelectRevisionAsync(_revision);
        if (!string.IsNullOrEmpty(_path))
        {
            checkBoxPathFilter.IsChecked = true;
            textBoxPaths.Text = _path;
        }

        if (_diffRevision is not null)
        {
            checkboxRevisionFilter.IsChecked = true;
            _diffSelectedRevision = await ReadRevisionAsync(_diffRevision);
            ShowDiffRevision();
        }

        _loaded.TrySetResult();
    }

    private async Task SelectRevisionAsync(string? revision)
    {
        _selectedRevision = await ReadRevisionAsync(revision);
        commitSummaryUserControl1.Show(_selectedRevision is null
            ? null
            : CreateBranchDialog.Summarize(_selectedRevision));
    }

    private Task<GitRevision?> ReadRevisionAsync(string? revision)
        => Task.Run(() => revision is null || _module.RevParse(revision) is not { IsZero: false } id
            ? null
            : _module.GetRevision(id, shortFormat: true, loadRefs: true));

    // Upstream's DiffSelectedRevision setter.
    private void ShowDiffRevision()
    {
        const string defaultString = "...";
        string dateCaption = $"{ResourceManager.TranslatedStrings.CommitDate}:";
        GitRevision? revision = _diffSelectedRevision;
        labelDateCaption.Text = revision is null ? dateCaption : $"{dateCaption} {revision.CommitDate}";
        labelAuthor.Text = revision?.Author ?? defaultString;
        gbDiffRevision.Header = revision?.ObjectId.ToShortString() ?? defaultString;
        labelMessage.Text = revision?.Subject ?? defaultString;
    }

    // Upstream's checkBoxPathFilter_CheckedChanged: the two filters exclude each other.
    private void PathFilterChanged()
    {
        textBoxPaths.IsEnabled = checkBoxPathFilter.IsChecked == true;
        if (checkBoxPathFilter.IsChecked == true)
        {
            checkboxRevisionFilter.IsChecked = false;
        }
    }

    // Upstream's checkboxRevisionFilter_CheckedChanged.
    private void RevisionFilterChanged()
    {
        bool filter = checkboxRevisionFilter.IsChecked == true;
        btnDiffChooseRevision.IsEnabled = gbDiffRevision.IsEnabled = filter;
        if (filter)
        {
            checkBoxPathFilter.IsChecked = false;
        }
    }

    // Upstream's btnChooseRevision_Click.
    private async Task ChooseRevisionAsync()
    {
        if (await new ChooseCommitWindow(_repositoryPath, _selectedRevision?.Guid, _preferences)
                .ShowDialog<Core.CommitHistory.CommitRow?>(this) is { } row)
        {
            await SelectRevisionAsync(row.Hash);
        }
    }

    // Upstream's btnDiffChooseRevision_Click.
    private async Task ChooseDiffRevisionAsync()
    {
        if (await new ChooseCommitWindow(_repositoryPath, _diffSelectedRevision?.Guid, _preferences)
                .ShowDialog<Core.CommitHistory.CommitRow?>(this) is { } row)
        {
            _diffSelectedRevision = await ReadRevisionAsync(row.Hash);
            ShowDiffRevision();
        }
    }

    // Upstream's Save_Click.
    private async Task SaveAsync()
    {
        if (checkboxRevisionFilter.IsChecked == true && _diffSelectedRevision is null)
        {
            await new MessageBoxWindow(UpstreamTranslation.ByText(ArchiveDialog.NoRevisionSelected),
                UpstreamTranslation.ByText(ManageWorktreeDialog.Error), MessageBoxButtons.OK).ShowDialog(this);
            return;
        }

        string revision = _selectedRevision?.Guid ?? "";
        bool zip = _NO_TRANSLATE_radioButtonFormatZip.IsChecked == true;
        string suggestion = ArchiveDialog.FileNameSuggestion(_module.WorkingDir, revision,
            checkBoxPathFilter.IsChecked == true, textBoxPaths.Text);
        if (await (PickOutput ?? PickWithDialogAsync)(suggestion, zip ? "zip" : "tar") is not { } output)
        {
            return;
        }

        string paths = await PathArgumentAsync();
        if (_archive is not null)
        {
            await _archive(ArchiveDialog.Arguments(zip, revision, output, paths));
        }

        Close();
    }

    // Upstream's GetPathArgumentFromGui.
    private Task<string> PathArgumentAsync()
    {
        if (checkBoxPathFilter.IsChecked == true)
        {
            return Task.FromResult(ArchiveDialog.PathArgument(textBoxPaths.Text));
        }

        if (checkboxRevisionFilter.IsChecked != true)
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

    // Upstream's SaveFileDialog: its title, the format's filter and the suggested name.
    private async Task<string?> PickWithDialogAsync(string suggestion, string extension)
    {
        string caption = UpstreamTranslation.ByText(extension == "zip"
            ? ArchiveDialog.SaveFileDialogFilterZip
            : ArchiveDialog.SaveFileDialogFilterTar);
        IStorageFile? file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = UpstreamTranslation.ByText(ArchiveDialog.SaveFileDialogCaption),
            SuggestedFileName = suggestion,
            DefaultExtension = extension,
            FileTypeChoices = [new FilePickerFileType(caption) { Patterns = [$"*.{extension}"] }],
        });
        return file?.TryGetLocalPath();
    }
}

using Avalonia.Controls;
using Avalonia.Platform.Storage;
using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Ui;
using DialogResult = System.Windows.Forms.DialogResult;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormCleanupRepository</c> (rules in <see cref="CleanupDialog"/>): <c>git clean</c> with upstream's
///  choices, previewed with a dry run, run after upstream's question; git's output goes to the log. Upstream shows each run
///  in its process window too; here the log is the only place.
/// </summary>
public partial class CleanupRepositoryWindow : Window
{
    private readonly GitModule _module;
    private readonly Action? _cleaned;

    public CleanupRepositoryWindow()
        : this("", null)
    {
    }

    /// <param name="repositoryPath">The repository to clean.</param>
    /// <param name="cleaned">Called after a cleanup ran, so the browse window reads the work tree again.</param>
    public CleanupRepositoryWindow(string repositoryPath, Action? cleaned)
    {
        _module = GitModules.Open(repositoryPath);
        _cleaned = cleaned;
        InitializeComponent();
        UpstreamTranslation.Apply(this, "FormCleanupRepository");
        InitialFocus.Attach(this);
        checkBoxIncludePathFilter.IsCheckedChanged += (_, _) => ShowPathFilters();
        checkBoxExcludePathFilter.IsCheckedChanged += (_, _) => ShowPathFilters();
        ShowPathFilters();
        Preview.Click += (_, _) => Run(() => CleanUpAsync(dryRun: true));
        Cleanup.Click += (_, _) => Run(CleanupAfterQuestionAsync);
        _NO_TRANSLATE_Close.Click += (_, _) => Close();
        AddInclusivePath.Click += (_, _) => Run(AddIncludePathAsync);
        AddExclusivePath.Click += (_, _) => Run(AddExcludePathAsync);
    }

    /// <summary>
    ///  The last run of git, for tests.
    /// </summary>
    public Task LastRun { get; private set; } = Task.CompletedTask;

    private void Run(Func<Task> action)
        => UiActions.Run(action, ex => _ = new ErrorWindow(ex.Message).ShowDialog(this));

    // Upstream's checkBoxPathFilter_CheckedChanged and checkBoxExcludePathFilter_CheckedChanged.
    private void ShowPathFilters()
    {
        textBoxIncludePaths.IsEnabled = labelPathHintInclude.IsVisible = checkBoxIncludePathFilter.IsChecked == true;
        textBoxExcludePaths.IsEnabled = labelPathHintExclude.IsVisible = checkBoxExcludePathFilter.IsChecked == true;
    }

    // Upstream's Cleanup_Click.
    private async Task CleanupAfterQuestionAsync()
    {
        MessageBoxWindow question = new(UpstreamTranslation.ByText(CleanupDialog.ReallyCleanupQuestion),
            UpstreamTranslation.ByText(CleanupDialog.ReallyCleanupQuestionCaption), MessageBoxButtons.YesNo);
        await question.ShowDialog(this);
        if (question.Result == DialogResult.Yes)
        {
            await CleanUpAsync(dryRun: false);
            _cleaned?.Invoke();
        }
    }

    // Upstream's CleanUp: git clean, then the submodules', their output in the log.
    private Task CleanUpAsync(bool dryRun)
    {
        CleanMode mode = RemoveIgnored.IsChecked == true ? CleanMode.OnlyIgnored
            : RemoveNonIgnored.IsChecked == true ? CleanMode.OnlyNonIgnored
            : CleanMode.All;
        IReadOnlyList<ArgumentString> commands = CleanupDialog.Commands(mode, dryRun,
            RemoveDirectories.IsChecked == true, CleanSubmodules.IsChecked == true,
            CleanupDialog.IncludePaths(checkBoxIncludePathFilter.IsChecked == true, textBoxIncludePaths.Text),
            CleanupDialog.ExcludePaths(checkBoxExcludePathFilter.IsChecked == true, textBoxExcludePaths.Text));
        return LastRun = RunCommandsAsync(commands);
    }

    private async Task RunCommandsAsync(IReadOnlyList<ArgumentString> commands)
    {
        PreviewOutput.Text = "";
        foreach (ArgumentString command in commands)
        {
            ExecutionResult result = await Task.Run(() => _module.GitExecutable.Execute(command, throwOnErrorExit: false));
            PreviewOutput.Text += result.StandardOutput + result.StandardError;
        }
    }

    // Upstream's AddIncludePath_Click: a folder of the work tree, on a line of its own, with the filter on.
    private async Task AddIncludePathAsync()
    {
        IReadOnlyList<IStorageFolder> folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(_module.WorkingDir),
        });
        string? folder = folders.FirstOrDefault()?.TryGetLocalPath();
        if (CleanupDialog.AcceptsIncludePath(_module.WorkingDir, _module.WorkingDirGitDir, folder))
        {
            checkBoxIncludePathFilter.IsChecked = true;
            textBoxIncludePaths.Text = CleanupDialog.AddLine(textBoxIncludePaths.Text, folder!);
        }
    }

    // Upstream's AddExcludePath_Click: a file of the work tree, relative to it, on a line of its own, with the filter on.
    private async Task AddExcludePathAsync()
    {
        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(_module.WorkingDir),
        });
        if (CleanupDialog.ExcludePath(_module.WorkingDir, files.FirstOrDefault()?.TryGetLocalPath()) is { } path)
        {
            checkBoxExcludePathFilter.IsChecked = true;
            textBoxExcludePaths.Text = CleanupDialog.AddLine(textBoxExcludePaths.Text, path);
        }
    }
}

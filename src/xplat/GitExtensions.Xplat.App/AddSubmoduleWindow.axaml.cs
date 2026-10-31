using Avalonia.Controls;
using Avalonia.Platform.Storage;
using GitCommands;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Ui;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormAddSubmodule</c> (rules in <see cref="SubmodulesDialog"/>): <c>git submodule add</c> of a repository
///  at a local path, on a branch, forced or not.
/// </summary>
public partial class AddSubmoduleWindow : Window
{
    private readonly GitModule _module;
    private readonly Func<string, Task<bool>> _runGitWithOutput;

    public AddSubmoduleWindow()
        : this("", _ => Task.FromResult(false))
    {
    }

    /// <param name="repositoryPath">The repository the submodule is added to.</param>
    /// <param name="runGitWithOutput">Runs the add command with its output shown (upstream's FormProcess).</param>
    public AddSubmoduleWindow(string repositoryPath, Func<string, Task<bool>> runGitWithOutput)
    {
        _module = GitModules.Open(repositoryPath);
        _runGitWithOutput = runGitWithOutput;
        InitializeComponent();
        UpstreamTranslation.Apply(this, "FormAddSubmodule");
        InitialFocus.Attach(this, () => Directory);
        Directory.PropertyChanged += (_, e) =>
        {
            // Upstream's DirectoryTextUpdate and DirectorySelectedIndexChanged.
            if (e.Property == ComboBox.TextProperty && SubmodulesDialog.ProposedLocalPath(Directory.Text) is { } path)
            {
                LocalPath.Text = path;
            }
        };
        Branch.DropDownOpened += (_, _) => Run(() => BranchesLoaded = LoadBranchesAsync());
        Browse.Click += (_, _) => Run(BrowseAsync);
        Add.Click += (_, _) => Run(AddAsync);
        Opened += (_, _) => Run(LoadHistoryAsync);
    }

    /// <summary>
    ///  The last read of the remote's branches, for tests.
    /// </summary>
    public Task BranchesLoaded { get; private set; } = Task.CompletedTask;

    // Upstream's constructor: the remote URL history in the list, the boxes empty.
    private async Task LoadHistoryAsync()
    {
        IList<GitCommands.UserRepositoryHistory.Repository> remotes = await AppServices.RemoteUrls.LoadAsync();
        Directory.ItemsSource = remotes.Select(remote => remote.Path).ToList();
    }

    // Upstream's BranchDropDown: the remote repository's branches, read each time the list opens.
    private async Task LoadBranchesAsync()
    {
        string? url = Directory.Text;
        string text = Branch.Text ?? "";
        Branch.ItemsSource = await Task.Run(() => SubmodulesDialog.LoadRemoteBranches(_module.GitExecutable, url));
        Branch.Text = text;
    }

    // Upstream's BrowseClick: a folder as the repository to add.
    private async Task BrowseAsync()
    {
        IStorageFolder? start = string.IsNullOrWhiteSpace(Directory.Text) || !System.IO.Directory.Exists(Directory.Text)
            ? null
            : await StorageProvider.TryGetFolderFromPathAsync(Directory.Text);
        IReadOnlyList<IStorageFolder> folders = await StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions { AllowMultiple = false, SuggestedStartLocation = start });
        if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } path)
        {
            Directory.Text = path;
        }
    }

    // Upstream's AddClick: both paths are needed; the command runs with its output and the window closes.
    private async Task AddAsync()
    {
        if (!SubmodulesDialog.CanAdd(Directory.Text, LocalPath.Text))
        {
            await new MessageBoxWindow(UpstreamTranslation.ByText(SubmodulesDialog.RemoteAndLocalPathRequired), Title ?? "",
                MessageBoxButtons.OK).ShowDialog(this);
            return;
        }

        await _runGitWithOutput(SubmodulesDialog.AddArguments(Directory.Text!, LocalPath.Text!, Branch.Text,
            chkForce.IsChecked == true).ToString());
        Close();
    }

    private void Run(Func<Task> action)
        => UiActions.Run(action, ex => _ = new ErrorWindow(ex.Message).ShowDialog(this));
}

using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using GitCommands;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormSubmodules</c> (Repository > Manage submodules...), bound to <see cref="SubmodulesViewModel"/> (rules
///  in <see cref="SubmodulesDialog"/>): the submodules with their details, adding one (<see cref="AddSubmoduleWindow"/>),
///  and synchronizing, updating, pulling or removing the selected one.
/// </summary>
public partial class SubmodulesWindow : Window
{
    public SubmodulesWindow()
        : this("", _ => Task.FromResult(false), (_, _) => Task.CompletedTask)
    {
    }

    /// <param name="repositoryPath">The repository whose submodules are managed.</param>
    /// <param name="runGitWithOutput">
    ///  Runs a git command in the repository with its output shown (upstream's FormProcess): adding, synchronizing and
    ///  updating a submodule.
    /// </param>
    /// <param name="pull">Opens the pull dialog on a submodule's work tree (upstream's <c>Pull_Click</c>).</param>
    public SubmodulesWindow(string repositoryPath, Func<string, Task<bool>> runGitWithOutput,
        Func<Window, string, Task> pull)
    {
        GitModule module = GitModules.Open(repositoryPath);
        ViewModel = new SubmodulesViewModel(new WindowDialogHost(this), module, runGitWithOutput,
            path => pull(this, path),
            () => new AddSubmoduleWindow(module.WorkingDir, runGitWithOutput).ShowDialog(this));
        DataContext = ViewModel;
        InitializeComponent();
        UpstreamTranslation.Apply(this, SubmodulesViewModel.Category);
        InitialFocus.Attach(this, () => Submodules);
        new SplitterTracker(this, AppServices.Splitters, SubmodulesViewModel.Category)
            .Add("splitContainer1", splitContainer1, rows: false, Core.Settings.SplitterFixedPanel.Panel1);

        // View concern: upstream's wait cursor while the list is read.
        ViewModel.PropertyChanged += OnViewModelChanged;
        Opened += (_, _) => _ = ViewModel.ReloadAsync();
    }

    public SubmodulesViewModel ViewModel { get; }

    /// <summary>
    ///  The last read of the submodules, for tests.
    /// </summary>
    public Task SubmodulesLoaded => ViewModel.Loaded;

    /// <summary>
    ///  The submodules as listed.
    /// </summary>
    public IReadOnlyList<GitSubmoduleInfo> Items => ViewModel.Submodules;

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SubmodulesViewModel.IsLoading))
        {
            Cursor = ViewModel.IsLoading ? new Cursor(StandardCursorType.Wait) : Cursor.Default;
        }
    }
}

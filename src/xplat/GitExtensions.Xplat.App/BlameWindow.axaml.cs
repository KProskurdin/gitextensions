using Avalonia.Controls;
using GitExtensions.Extensibility.Plugins;
using GitExtensions.Xplat.Core.CommitHistory;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Shows, for each line of a file, the commit and author that last changed it, as of a commit (upstream's <c>FormBlame</c>
///  around the shared <see cref="BlameView"/>); bound to <see cref="BlameWindowViewModel"/>.
/// </summary>
public partial class BlameWindow : Window
{
    /// <summary>
    ///  The design-time constructor (<see cref="DesignTime"/>): the window's XAML only.
    /// </summary>
    [Obsolete(DesignTime.ConstructorOnly)]
    public BlameWindow() => InitializeComponent();

    /// <param name="repositoryHost">The repository host plugin of the repository, whose items the context menu shows, as
    ///  upstream's blame menu shows the GitHub plugin's "View in GitHub".</param>
    /// <param name="initialLine">Upstream's <c>initialLine</c>: the line to select once the file is blamed.</param>
    public BlameWindow(string repositoryPath, string hash, string filePath,
        Func<IRepositoryHostPlugin?>? repositoryHost = null, int? initialLine = null)
    {
        InitializeComponent();
        WindowPlacementTracker.Attach(this, "Xplat.BlameWindow");
        BlameWindowViewModel viewModel = new(BlameView.NewViewModel(), repositoryPath, hash, filePath);
        DataContext = viewModel;
        Blame.RepositoryHost = repositoryHost;
        viewModel.Blame.GoToLineWhenBlamed(initialLine);

        // A line leads to that commit's change to the file.
        viewModel.Blame.CommitRequested += (_, request) =>
            new DiffWindow(repositoryPath, request.Hash, request.FileName, staged: false).Show(this);
        Opened += (_, _) => _ = viewModel.LoadAsync();
    }
}

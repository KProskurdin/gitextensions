using Avalonia.Controls;
using GitExtensions.Extensibility.Plugins;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Shows, for each line of a file, the commit and author that last changed it, as of a commit (upstream's <c>FormBlame</c>
///  around the shared <see cref="BlameView"/>).
/// </summary>
public partial class BlameWindow : Window
{
    private readonly string _repositoryPath;
    private readonly string _hash;
    private readonly string _filePath;

    /// <param name="repositoryHost">The repository host plugin of the repository, whose items the context menu shows, as
    ///  upstream's blame menu shows the GitHub plugin's "View in GitHub".</param>
    public BlameWindow(string repositoryPath, string hash, string filePath,
        Func<IRepositoryHostPlugin?>? repositoryHost = null)
    {
        _repositoryPath = repositoryPath;
        _hash = hash;
        _filePath = filePath;
        InitializeComponent();
        WindowPlacementTracker.Attach(this, "Xplat.BlameWindow");
        Title = $"Blame: {filePath}";
        Blame.RepositoryHost = repositoryHost;
        Blame.ErrorOccurred += (_, message) => ErrorText.Text = message;

        // A line leads to that commit's change to the file.
        Blame.CommitRequested += (_, hash) => new DiffWindow(_repositoryPath, hash, _filePath, staged: false).Show(this);
        Opened += (_, _) => UiActions.Run(() => Blame.LoadAsync(_repositoryPath, _hash, _filePath),
            ex => ErrorText.Text = ex.Message);
    }
}

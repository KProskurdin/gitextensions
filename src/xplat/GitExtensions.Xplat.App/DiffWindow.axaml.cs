using Avalonia.Controls;
using GitExtensions.Xplat.Core.Diff;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Shows the diff of one file, either in a commit or in the working tree (staged or unstaged), in its own window; bound to
///  <see cref="DiffWindowViewModel"/>.
/// </summary>
public partial class DiffWindow : Window
{
    /// <summary>
    ///  The design-time constructor (<see cref="DesignTime"/>): the window's XAML only.
    /// </summary>
    [Obsolete(DesignTime.ConstructorOnly)]
    public DiffWindow() => InitializeComponent();

    /// <summary>
    ///  Shows a diff that <paramref name="show"/> loads into the window's view (a file between two revisions).
    /// </summary>
    public DiffWindow(string title, Func<DiffView, Task> show)
    {
        InitializeComponent();

        // The caller's load takes the view, whose view model is the window's.
        Open(new DiffWindowViewModel(new WindowDialogHost(this), DiffView.NewViewModel(), title, _ => show(Diff)));
    }

    public DiffWindow(string repositoryPath, string? commitHash, string? filePath, bool staged)
    {
        InitializeComponent();
        Open(new DiffWindowViewModel(new WindowDialogHost(this), DiffView.NewViewModel(), repositoryPath, commitHash,
            filePath, staged));
    }

    public DiffWindowViewModel? ViewModel { get; private set; }

    private void Open(DiffWindowViewModel viewModel)
    {
        WindowPlacementTracker.Attach(this, "Xplat.DiffWindow");
        ViewModel = viewModel;
        DataContext = viewModel;
        Opened += (_, _) => _ = viewModel.LoadAsync();
    }
}

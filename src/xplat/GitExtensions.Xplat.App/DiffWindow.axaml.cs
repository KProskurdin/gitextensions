using Avalonia.Controls;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Shows the diff of one file, either in a commit or in the working tree (staged or unstaged), in its own window.
/// </summary>
public partial class DiffWindow : Window
{
    /// <summary>
    ///  Shows a diff that <paramref name="show"/> loads into the window's view (a file between two revisions).
    /// </summary>
    public DiffWindow(string title, Func<DiffView, Task> show)
    {
        InitializeComponent();
        WindowPlacementTracker.Attach(this, "Xplat.DiffWindow");
        Title = title;
        Opened += (_, _) => UiActions.Run(() => show(Diff), ex => _ = new ErrorWindow(ex.Message).ShowDialog(this));
    }

    public DiffWindow(string repositoryPath, string? commitHash, string? filePath, bool staged)
    {
        InitializeComponent();
        WindowPlacementTracker.Attach(this, "Xplat.DiffWindow");
        string? name = filePath is null ? null : $": {filePath}";
        Title = commitHash is null
            ? $"{(staged ? "Staged" : "Unstaged")}{name}"
            : $"{commitHash[..Math.Min(8, commitHash.Length)]}{name}";
        Opened += (_, _) => UiActions.Run(
            () => Diff.ShowAsync(repositoryPath, commitHash, filePath, staged, filePath ?? Title),
            ex => _ = new ErrorWindow(ex.Message).ShowDialog(this));
    }
}

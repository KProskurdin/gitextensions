using Avalonia.Controls;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormAddToGitIgnore</c>, bound to <see cref="AddToGitIgnoreViewModel"/>: patterns (one per line) appended
///  to the repository's .gitignore or its local excludes, with a preview of the files they match that follows the typing.
///  <see cref="Added"/> tells whether the file was written.
/// </summary>
public partial class AddToGitIgnoreWindow : Window
{
    public AddToGitIgnoreWindow()
        : this("", localExclude: false, [])
    {
    }

    /// <param name="repositoryPath">The repository's working directory.</param>
    /// <param name="localExclude">True for .git/info/exclude, false for .gitignore.</param>
    /// <param name="filePatterns">The patterns in the box at first.</param>
    public AddToGitIgnoreWindow(string repositoryPath, bool localExclude, IReadOnlyList<string> filePatterns)
    {
        ViewModel = new AddToGitIgnoreViewModel(new WindowDialogHost(this), repositoryPath, localExclude, filePatterns);
        InitializeComponent();
        UpstreamTranslation.Apply(this, AddToGitIgnoreViewModel.Category);

        // After the translation pass, which would give the local excludes the .gitignore title: the view model translates it.
        DataContext = ViewModel;
        InitialFocus.Attach(this, () => FilePattern);
        Opened += (_, _) => ViewModel.UpdatePreview();
    }

    public AddToGitIgnoreViewModel ViewModel { get; }

    /// <summary>
    ///  True when the patterns were appended to the file.
    /// </summary>
    public bool Added => ViewModel.Added;

    /// <summary>
    ///  The files the preview shows for the current patterns; empty while it is updating.
    /// </summary>
    public IReadOnlyList<string> PreviewFiles => ViewModel.PreviewFiles;

    /// <summary>
    ///  Upstream's <c>StartAddToGitIgnoreDialog</c>: the window shown over <paramref name="owner"/>; true when the patterns
    ///  were added.
    /// </summary>
    public static async Task<bool> ShowAsync(Window owner, string repositoryPath, bool localExclude,
        IReadOnlyList<string> filePatterns)
    {
        AddToGitIgnoreWindow window = new(repositoryPath, localExclude, filePatterns);
        await window.ShowDialog(owner);
        return window.Added;
    }
}

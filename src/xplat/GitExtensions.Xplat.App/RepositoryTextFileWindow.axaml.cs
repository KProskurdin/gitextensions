using Avalonia.Controls;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormGitAttributes</c> and <c>FormMailMap</c>, bound to <see cref="RepositoryTextFileViewModel"/>: the
///  file's text to edit beside upstream's help. Save writes and closes; closing with changes asks upstream's question.
///  Closes with true when the file was saved.
/// </summary>
public partial class RepositoryTextFileWindow : Window
{
    public RepositoryTextFileWindow()
        : this("", RepositoryTextFiles.GitAttributes)
    {
    }

    public RepositoryTextFileWindow(string repositoryPath, RepositoryTextFile file)
    {
        ViewModel = new RepositoryTextFileViewModel(new WindowDialogHost(this), repositoryPath, file);
        DataContext = ViewModel;
        InitializeComponent();
        UpstreamTranslation.Apply(this, ViewModel.Category);
        InitialFocus.Attach(this, () => FileText);

        // A close with changes waits for upstream's question, which closes the window again once it is answered.
        Closing += (sender, e) =>
        {
            if (!ViewModel.MayClose)
            {
                e.Cancel = true;
                _ = ViewModel.AskToCloseAsync();
            }
        };
        Opened += (_, _) => _ = ViewModel.LoadAsync();
    }

    public RepositoryTextFileViewModel ViewModel { get; }

    /// <summary>
    ///  Completes when the file's text is shown, for tests.
    /// </summary>
    public Task FileLoaded => ViewModel.Loaded;
}

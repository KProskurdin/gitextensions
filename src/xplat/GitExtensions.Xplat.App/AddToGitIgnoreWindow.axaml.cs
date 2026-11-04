using Avalonia.Controls;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Ui;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormAddToGitIgnore</c>: patterns (one per line) appended to the repository's .gitignore or its local
///  excludes, with a preview of the files they match that follows the typing. <see cref="Added"/> tells whether the file
///  was written.
/// </summary>
public partial class AddToGitIgnoreWindow : Window
{
    private const string FormName = "FormAddToGitIgnore";
    private const string ErrorCaption = "Error";

    private readonly string _repositoryPath;
    private readonly string _excludeFile;
    private CancellationTokenSource? _preview;

    public AddToGitIgnoreWindow()
        : this("", localExclude: false, [])
    {
    }

    /// <param name="repositoryPath">The repository's working directory.</param>
    /// <param name="localExclude">True for .git/info/exclude, false for .gitignore.</param>
    /// <param name="filePatterns">The patterns in the box at first.</param>
    public AddToGitIgnoreWindow(string repositoryPath, bool localExclude, IReadOnlyList<string> filePatterns)
    {
        _repositoryPath = repositoryPath;
        _excludeFile = repositoryPath.Length > 0 ? GitIgnoreFile.PathIn(repositoryPath, localExclude) : "";
        InitializeComponent();
        UpstreamTranslation.Apply(this, FormName);
        InitialFocus.Attach(this, () => FilePattern);
        if (localExclude)
        {
            Title = UpstreamTranslation.ByText(GitIgnoreDialog.AddToLocalExcludeTitle);
        }

        FilePattern.Text = string.Join(Environment.NewLine, filePatterns);
        FilePattern.TextChanged += (_, _) => UpdatePreview();
        AddToIgnore.Click += (_, _) => UiActions.Run(AddToIgnoreAsync, ShowError);
        btnCancel.Click += (_, _) => Close();
        Opened += (_, _) => UpdatePreview();
    }

    /// <summary>
    ///  True when the patterns were appended to the file.
    /// </summary>
    public bool Added { get; private set; }

    /// <summary>
    ///  The files the preview shows for the current patterns; empty while it is updating.
    /// </summary>
    public IReadOnlyList<string> PreviewFiles { get; private set; } = [];

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

    // Upstream's FilePattern_TextChanged: the running preview cancelled, "Updating ..." while it waits, and git asked once
    // the typing has paused.
    private void UpdatePreview()
    {
        _preview?.Cancel();
        CancellationTokenSource preview = new();
        _preview = preview;
        if (_NO_TRANSLATE_Preview.IsEnabled)
        {
            string updating = UpstreamTranslation.ByText(GitIgnoreDialog.Updating);
            _NO_TRANSLATE_filesWillBeIgnored.Text = updating;
            _NO_TRANSLATE_Preview.ItemsSource = new[] { updating };
            _NO_TRANSLATE_Preview.IsEnabled = false;
            PreviewFiles = [];
        }

        IReadOnlyList<string> patterns = GitIgnoreDialog.PatternsOf(FilePattern.Text ?? "");
        UiActions.Run(() => LoadPreviewAsync(patterns, preview.Token), ShowError);
    }

    private async Task LoadPreviewAsync(IReadOnlyList<string> patterns, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> files;
        try
        {
            await Task.Delay(GitIgnoreDialog.PreviewDelay, cancellationToken);
            files = await GitIgnoreDialog.IgnoredFilesAsync(_repositoryPath, patterns, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        // Upstream's UpdatePreviewPanel.
        PreviewFiles = files;
        _NO_TRANSLATE_Preview.ItemsSource = files;
        _NO_TRANSLATE_filesWillBeIgnored.Text =
            string.Format(UpstreamTranslation.ByText(GitIgnoreDialog.MatchingFiles), files.Count);
        _NO_TRANSLATE_Preview.IsEnabled = true;
        noMatchPanel.IsVisible = files.Count == 0;
    }

    // Upstream's AddToIgnoreClick: closes in any case, after an error too.
    private async Task AddToIgnoreAsync()
    {
        IReadOnlyList<string> patterns = GitIgnoreDialog.PatternsOf(FilePattern.Text ?? "");
        if (patterns.Count > 0)
        {
            try
            {
                await Task.Run(() => GitIgnoreDialog.Append(_excludeFile, patterns));
                Added = true;
            }
            catch (Exception ex)
            {
                await new MessageBoxWindow(ex.ToString(), UpstreamTranslation.ByText(ErrorCaption),
                        MessageBoxButtons.OK)
                    .ShowDialog(this);
            }
        }

        Close();
    }

    private void ShowError(Exception ex) => _ = new ErrorWindow(ex.Message).ShowDialog(this);
}

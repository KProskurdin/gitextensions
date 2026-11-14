using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitExtensions.Xplat.Core.Presentation;

namespace GitExtensions.Xplat.Core.Repository;

/// <summary>
///  The view model of upstream's <c>FormAddToGitIgnore</c> (rules in <see cref="GitIgnoreDialog"/>): patterns (one per
///  line) appended to the repository's .gitignore or its local excludes, with a preview of the files they match that
///  follows the typing. <see cref="Added"/> tells whether the file was written; the window closes after Ignore in any case.
/// </summary>
public sealed partial class AddToGitIgnoreViewModel : DialogViewModel
{
    /// <summary>
    ///  The upstream form whose texts the window shows, by their names.
    /// </summary>
    public const string Category = "FormAddToGitIgnore";

    private const string ErrorCaption = "Error";
    private const string TitleName = "$this";
    private const string GitIgnoreTitle = "Add file(s) to .gitignore";

    private readonly string _repositoryPath;
    private readonly string _excludeFile;
    private CancellationTokenSource? _preview;
    private string _patternText;

    /// <param name="repositoryPath">The repository's working directory.</param>
    /// <param name="localExclude">True for .git/info/exclude, false for .gitignore.</param>
    /// <param name="filePatterns">The patterns in the box at first.</param>
    public AddToGitIgnoreViewModel(IDialogHost host, string repositoryPath, bool localExclude,
        IReadOnlyList<string> filePatterns)
        : base(host)
    {
        _repositoryPath = repositoryPath;
        _excludeFile = repositoryPath.Length > 0 ? GitIgnoreFile.PathIn(repositoryPath, localExclude) : "";
        Title = localExclude
            ? Host.Translate(GitIgnoreDialog.AddToLocalExcludeTitle)
            : Host.Translate(Category, TitleName, GitIgnoreTitle);
        _patternText = string.Join(Environment.NewLine, filePatterns);
    }

    public string Title { get; }

    /// <summary>
    ///  The patterns, one per line; the preview follows them.
    /// </summary>
    public string PatternText
    {
        get => _patternText;
        set
        {
            if (SetProperty(ref _patternText, value))
            {
                UpdatePreview();
            }
        }
    }

    /// <summary>
    ///  The preview list: the matched files, or upstream's "Updating ..." while it waits.
    /// </summary>
    [ObservableProperty]
    public partial IReadOnlyList<string> PreviewItems { get; private set; } = [];

    /// <summary>
    ///  Upstream's count of matched files under the preview.
    /// </summary>
    [ObservableProperty]
    public partial string PreviewText { get; private set; } = "(matched files count)";

    /// <summary>
    ///  True when the preview shows files; upstream disables the list while it updates.
    /// </summary>
    [ObservableProperty]
    public partial bool IsPreviewReady { get; private set; } = true;

    /// <summary>
    ///  Upstream's "No existing files match that pattern." over the empty list.
    /// </summary>
    [ObservableProperty]
    public partial bool NoMatch { get; private set; }

    /// <summary>
    ///  The files the preview shows for the current patterns; empty while it is updating.
    /// </summary>
    public IReadOnlyList<string> PreviewFiles { get; private set; } = [];

    /// <summary>
    ///  True when the patterns were appended to the file.
    /// </summary>
    public bool Added { get; private set; }

    /// <summary>
    ///  Upstream's <c>FilePattern_TextChanged</c>: the running preview cancelled, "Updating ..." while it waits, and git
    ///  asked once the typing has paused. The window calls it when it opens; a change of the patterns calls it too.
    /// </summary>
    public void UpdatePreview()
    {
        _preview?.Cancel();
        CancellationTokenSource preview = new();
        _preview = preview;
        if (IsPreviewReady)
        {
            string updating = Host.Translate(GitIgnoreDialog.Updating);
            PreviewText = updating;
            PreviewItems = [updating];
            IsPreviewReady = false;
            PreviewFiles = [];
        }

        IReadOnlyList<string> patterns = GitIgnoreDialog.PatternsOf(PatternText);
        Run(() => LoadPreviewAsync(patterns, preview.Token));
    }

    // Upstream's AddToIgnoreClick: closes in any case, after an error too.
    [RelayCommand]
    private Task AddToIgnoreAsync() => GuardAsync(async () =>
    {
        IReadOnlyList<string> patterns = GitIgnoreDialog.PatternsOf(PatternText);
        if (patterns.Count > 0)
        {
            try
            {
                await Task.Run(() => GitIgnoreDialog.Append(_excludeFile, patterns));
                Added = true;
            }
            catch (Exception ex)
            {
                await Host.ShowMessageAsync(ex.ToString(), Host.Translate(ErrorCaption));
            }
        }

        Host.Close(null);
    });

    [RelayCommand]
    private void Cancel() => Host.Close(null);

    private async Task LoadPreviewAsync(IReadOnlyList<string> patterns, CancellationToken cancellationToken)
    {
        await Task.Delay(GitIgnoreDialog.PreviewDelay, cancellationToken);
        IReadOnlyList<string> files =
            await GitIgnoreDialog.IgnoredFilesAsync(_repositoryPath, patterns, cancellationToken);
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        // Upstream's UpdatePreviewPanel.
        PreviewFiles = files;
        PreviewItems = files;
        PreviewText = string.Format(Host.Translate(GitIgnoreDialog.MatchingFiles), files.Count);
        IsPreviewReady = true;
        NoMatch = files.Count == 0;
    }
}

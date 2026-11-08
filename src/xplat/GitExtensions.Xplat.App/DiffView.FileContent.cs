using Avalonia.Controls;
using Avalonia.Media.Imaging;
using GitCommands;
using GitExtensions.Xplat.Core.Diff;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream FileViewer's whole files in the viewer: a picture instead of a diff when the file is an image, a hex dump of a
///  binary file, and the click it asks for before showing a large one.
/// </summary>
public partial class DiffView
{
    // Git's line for a new binary file; upstream shows a new file itself, not a diff.
    private const string NewBinaryFilePrefix = "Binary files /dev/null and ";
    private const double BytesPerMb = 1024d * 1024;

    // What is on show; a read that finishes after something else was asked for shows nothing.
    private int _contentVersion;
    private Func<Task>? _showLargeFile;

    /// <summary>
    ///  Decodes an image file (upstream's <c>CreateImage</c>); a failure shows upstream's "Cannot view image". Tests replace
    ///  it, as the headless platform's bitmaps decode nothing.
    /// </summary>
    public Func<byte[], Bitmap> DecodeImage { get; set; } = bytes =>
    {
        using MemoryStream stream = new(bytes);
        return new Bitmap(stream);
    };

    /// <summary>
    ///  True while the viewer shows a picture.
    /// </summary>
    public bool IsShowingImage => PicturePanel.IsVisible;

    /// <summary>
    ///  Upstream's <c>ViewGitItemAsync</c>: the file of a commit, the index or the working directory, as a picture, a text
    ///  or a hex dump.
    /// </summary>
    public Task ShowFileAsync(string repositoryPath, string? commitHash, bool staged, string filePath,
        string? title = null)
        => LoadFileAsync(ResetContent(), repositoryPath, FileRevision.Of(commitHash, staged), filePath,
            title ?? filePath,
            showLarge: false);

    private void WireFileContent()
    {
        ShowLargeFileLink.Click += (_, _) =>
        {
            if (_showLargeFile is { } show)
            {
                UiActions.Run(show, ex => ShowError(ex.Message));
            }
        };

        // Upstream's PictureBox_MouseWheel: the wheel goes to the next or previous file, as past a diff's end.
        PicturePanel.PointerWheelChanged += (_, e) =>
            RaiseScrollReached(e.Delta.Y > 0 ? TopScrollReached : BottomScrollReached, e.KeyModifiers);
    }

    // Upstream's ResetView: whatever was shown goes, and reads still running no longer count.
    private int ResetContent()
    {
        _showLargeFile = null;
        ShowLargeFileLink.IsVisible = false;
        PicturePanel.IsVisible = false;
        DiffList.IsVisible = true;
        Bitmap? shown = PictureBox.Source as Bitmap;
        PictureBox.Source = null;
        shown?.Dispose();
        return ++_contentVersion;
    }

    private async Task LoadFileAsync(int version, string repositoryPath, FileRevision revision, string filePath,
        string title,
        bool showLarge)
    {
        ViewedFile file = await FileContent.ReadAsync(repositoryPath, revision, filePath, _options, showLarge);
        if (version == _contentVersion)
        {
            await ShowViewedFileAsync(version, file, repositoryPath, revision, filePath, title);
        }
    }

    // Upstream's ViewChangesAsync: an image that the revision has shows as a picture, not as a diff. False when it has none,
    // as for a deleted image, which keeps its diff.
    private async Task<bool> ShowImageInsteadOfDiffAsync(int version, string repositoryPath, FileRevision revision,
        string filePath, string title)
    {
        ViewedFile file = await FileContent.ReadAsync(repositoryPath, revision, filePath, _options);
        if (version != _contentVersion)
        {
            return true;
        }

        if (file.Error is not null)
        {
            return false;
        }

        await ShowViewedFileAsync(version, file, repositoryPath, revision, filePath, title);
        return true;
    }

    // Upstream's ViewChangesAsync for a new file, here only a binary one: its hex dump, not git's one line about it.
    private async Task ShowNewBinaryFileAsync(int version, DiffRequest request, FileRevision revision, string title)
    {
        if (version == _contentVersion && _request == request && request.FilePath is { } filePath
            && _viewModel.Lines.Any(line => line.Text.StartsWith(NewBinaryFilePrefix, StringComparison.Ordinal)))
        {
            await LoadFileAsync(ResetContent(), request.RepositoryPath, revision, filePath, title, showLarge: false);
        }
    }

    private async Task ShowViewedFileAsync(int version, ViewedFile file, string repositoryPath, FileRevision revision,
        string filePath, string title)
    {
        if (file.TooLarge is { } length)
        {
            ShowContentText(title, "", syntaxPath: null);
            ShowLargeFileLink.Content = string.Format(UpstreamTranslation.ByText(FileContent.LargeFileSizeWarning),
                length / BytesPerMb);
            ShowLargeFileLink.IsVisible = true;
            _showLargeFile = () =>
                LoadFileAsync(ResetContent(), repositoryPath, revision, filePath, title, showLarge: true);
        }
        else if (file.Image is { } image)
        {
            await ShowImageAsync(version, image, title, filePath);
        }
        else if (file.BinaryData is { } data)
        {
            ShowContentText(title,
                FileContent.HexDump(FileContent.BinaryFile, filePath, data, UpstreamTranslation.ByText),
                syntaxPath: null);
        }
        else if (file.Text is { } text)
        {
            ShowContentText(title, text, filePath);
        }
        else
        {
            ShowContentText(title, "", syntaxPath: null);
            ShowError(file.Error);
        }
    }

    // Upstream's ViewItemAsync: larger than the viewer the picture is zoomed to fit, else centered at its size (the Image's
    // Stretch and StretchDirection), and its first frame only, which is what a Bitmap decodes.
    private async Task ShowImageAsync(int version, byte[] image, string title, string filePath)
    {
        Bitmap bitmap;
        try
        {
            bitmap = await Task.Run(() => DecodeImage(image));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (version == _contentVersion)
            {
                ShowContentText(title, FileContent.HexDump(FileContent.CannotViewImage, filePath,
                    FileContent.BinaryData(image), UpstreamTranslation.ByText), syntaxPath: null);
            }

            return;
        }

        if (version != _contentVersion)
        {
            bitmap.Dispose();
            return;
        }

        ShowContentText(title, "", syntaxPath: null);
        VerticalRuler.IsVisible = false;
        DiffList.IsVisible = false;
        PictureBox.Source = bitmap;
        PicturePanel.IsVisible = true;
    }

    private void ShowContentText(string title, string text, string? syntaxPath)
    {
        _request = null;
        _grep = null;
        _grepRead = null;
        TitleText.Text = title;
        _hasOptions = false;
        ShowToolbar();
        ShowError(null);
        _syntaxPath = syntaxPath;
        _textIsDiff = false;
        _text = DiffParser.ParseText(text);
        ShowLines();
    }
}

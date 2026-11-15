using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream FileViewer's whole files in the viewer: which file shows as a picture, a hex dump or a link to a large file is
///  the view model's; decoding the picture and showing it is the view's.
/// </summary>
public partial class DiffView
{
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
    public bool IsShowingImage => ViewModel.IsShowingImage;

    /// <summary>
    ///  Upstream's <c>ViewGitItemAsync</c>: the file of a commit, the index or the working directory, as a picture, a text
    ///  or a hex dump.
    /// </summary>
    public Task ShowFileAsync(string repositoryPath, string? commitHash, bool staged, string filePath,
        string? title = null)
        => ViewModel.ShowFileAsync(repositoryPath, commitHash, staged, filePath, title);

    private void WireFileContent()
    {
        // Upstream's PictureBox_MouseWheel: the wheel goes to the next or previous file, as past a diff's end.
        PicturePanel.PointerWheelChanged += (_, e) =>
            RaiseScrollReached(e.Delta.Y > 0 ? TopScrollReached : BottomScrollReached, e.KeyModifiers);
    }

    // Upstream's ViewItemAsync: larger than the viewer the picture is zoomed to fit, else centered at its size (the Image's
    // Stretch and StretchDirection), and its first frame only, which is what a Bitmap decodes. The view model disposes the
    // picture it replaces.
    private void ShowPicture()
    {
        PictureBox.Source = ViewModel.Picture as IImage;
        if (ViewModel.IsShowingImage)
        {
            VerticalRuler.IsVisible = false;
        }
    }
}

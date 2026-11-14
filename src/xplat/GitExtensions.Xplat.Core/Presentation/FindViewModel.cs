using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace GitExtensions.Xplat.Core.Presentation;

/// <summary>
///  The view model of upstream's <c>FindAndReplaceForm</c>: the text to find (and to replace with, in replace mode), match
///  case and whole word, and the searches it asks its viewer for through its events. Its title names the viewer's file
///  and whether the search keeps to the selection.
/// </summary>
public sealed partial class FindViewModel : DialogViewModel
{
    /// <summary>
    ///  The upstream form whose texts the window shows, by their names.
    /// </summary>
    public const string Category = "FindAndReplaceForm";

    // Upstream's texts.
    public const string FindText = "Find";
    public const string FindAndReplaceText = "Find & replace";
    public const string SelectionOnlyText = "selection only";

    public FindViewModel(IDialogHost host)
        : base(host)
    {
    }

    /// <summary>
    ///  Raised for Find next (false) and Find previous (true).
    /// </summary>
    public event EventHandler<bool>? FindNextRequested;

    /// <summary>
    ///  Raised for upstream's "Find &amp; highlight all".
    /// </summary>
    public event EventHandler? HighlightAllRequested;

    /// <summary>
    ///  Raised for upstream's "Replace".
    /// </summary>
    public event EventHandler? ReplaceRequested;

    /// <summary>
    ///  Raised for upstream's "Replace All".
    /// </summary>
    public event EventHandler? ReplaceAllRequested;

    /// <summary>
    ///  Upstream's <c>LookFor</c>: the text to find.
    /// </summary>
    [ObservableProperty]
    public partial string? LookFor { get; set; }

    /// <summary>
    ///  Upstream's <c>txtReplaceWith</c>.
    /// </summary>
    [ObservableProperty]
    public partial string? ReplaceWith { get; set; }

    [ObservableProperty]
    public partial bool MatchCase { get; set; }

    [ObservableProperty]
    public partial bool MatchWholeWord { get; set; }

    /// <summary>
    ///  Upstream's <c>ReplaceMode</c>: the replace controls show, and Replace takes the place of highlighting.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsHighlightAll), nameof(Title))]
    public partial bool ReplaceMode { get; set; }

    /// <summary>
    ///  Whether the viewer can mark every occurrence; a plain text box cannot.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsHighlightAll))]
    public partial bool CanHighlightAll { get; set; } = true;

    /// <summary>
    ///  The viewer's file, named in the title; null for none.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    public partial string? FileName { get; set; }

    /// <summary>
    ///  Upstream's <c>ScanRegionChanged</c>: the search keeps to the selection.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    public partial bool SelectionOnly { get; set; }

    public bool ShowsHighlightAll => CanHighlightAll && !ReplaceMode;

    /// <summary>
    ///  Upstream's <c>UpdateTitleBar</c>.
    /// </summary>
    public string Title
    {
        get
        {
            string text = Host.Translate(ReplaceMode ? FindAndReplaceText : FindText);
            if (FileName is not null)
            {
                text += " - " + Path.GetFileName(FileName);
            }

            if (SelectionOnly)
            {
                text += " (" + Host.Translate(SelectionOnlyText) + ")";
            }

            return text;
        }
    }

    [RelayCommand]
    private void FindNext() => FindNextRequested?.Invoke(this, false);

    [RelayCommand]
    private void FindPrevious() => FindNextRequested?.Invoke(this, true);

    [RelayCommand]
    private void HighlightAll() => HighlightAllRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void Replace() => ReplaceRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void ReplaceAll() => ReplaceAllRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void Cancel() => Host.Close(null);
}

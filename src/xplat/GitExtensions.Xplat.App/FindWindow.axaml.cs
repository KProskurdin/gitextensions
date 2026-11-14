using Avalonia;
using Avalonia.Controls;
using GitExtensions.Xplat.Core.Presentation;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FindAndReplaceForm</c>, bound to <see cref="FindViewModel"/>, one per viewer and kept while it lives:
///  closing hides it, as upstream's does, so the next Find shows it again with the last search. The viewer runs the
///  searches it asks for.
/// </summary>
public partial class FindWindow : Window
{
    // Upstream's texts.
    public const string FindText = FindViewModel.FindText;
    public const string FindAndReplaceText = FindViewModel.FindAndReplaceText;
    public const string SelectionOnlyText = FindViewModel.SelectionOnlyText;

    // Upstream shows the form this far right of and below its owner's corner.
    private const int OwnerOffset = 100;

    public FindWindow()
    {
        ViewModel = new FindViewModel(new WindowDialogHost(this));
        DataContext = ViewModel;
        InitializeComponent();
        UpstreamTranslation.Apply(this, FindViewModel.Category);

        // Upstream's FindAndReplaceForm_FormClosing: a user's close hides the form, which is used again, and drops the
        // selection the search kept to; the owner's close ends it.
        Closing += (_, e) =>
        {
            if (e.CloseReason is WindowCloseReason.OwnerWindowClosing or WindowCloseReason.ApplicationShutdown
                or WindowCloseReason.OSShutdown)
            {
                return;
            }

            e.Cancel = true;
            Hide();
            Hidden?.Invoke(this, EventArgs.Empty);
        };
    }

    /// <summary>
    ///  Raised for Find next (false) and Find previous (true).
    /// </summary>
    public event EventHandler<bool>? FindNextRequested
    {
        add => ViewModel.FindNextRequested += value;
        remove => ViewModel.FindNextRequested -= value;
    }

    /// <summary>
    ///  Raised for upstream's "Find &amp; highlight all".
    /// </summary>
    public event EventHandler? HighlightAllRequested
    {
        add => ViewModel.HighlightAllRequested += value;
        remove => ViewModel.HighlightAllRequested -= value;
    }

    /// <summary>
    ///  Raised for upstream's "Replace".
    /// </summary>
    public event EventHandler? ReplaceRequested
    {
        add => ViewModel.ReplaceRequested += value;
        remove => ViewModel.ReplaceRequested -= value;
    }

    /// <summary>
    ///  Raised for upstream's "Replace All".
    /// </summary>
    public event EventHandler? ReplaceAllRequested
    {
        add => ViewModel.ReplaceAllRequested += value;
        remove => ViewModel.ReplaceAllRequested -= value;
    }

    /// <summary>
    ///  Raised when the user closed the window, which hides it.
    /// </summary>
    public event EventHandler? Hidden;

    public FindViewModel ViewModel { get; }

    /// <summary>
    ///  Upstream's <c>LookFor</c>: the text to find.
    /// </summary>
    public string LookFor
    {
        get => ViewModel.LookFor ?? "";
        set => ViewModel.LookFor = value;
    }

    /// <summary>
    ///  Upstream's <c>ReplaceMode</c>: the replace controls show, and Replace takes the place of highlighting.
    /// </summary>
    public bool ReplaceMode
    {
        get => ViewModel.ReplaceMode;
        set => ViewModel.ReplaceMode = value;
    }

    /// <summary>
    ///  Whether the window's owner can mark every occurrence; a plain text box cannot.
    /// </summary>
    public bool CanHighlightAll
    {
        get => ViewModel.CanHighlightAll;
        set => ViewModel.CanHighlightAll = value;
    }

    /// <summary>
    ///  Upstream's <c>txtReplaceWith</c>.
    /// </summary>
    public string ReplaceWith
    {
        get => ViewModel.ReplaceWith ?? "";
        set => ViewModel.ReplaceWith = value;
    }

    public bool MatchCase => ViewModel.MatchCase;

    public bool MatchWholeWord => ViewModel.MatchWholeWord;

    /// <summary>
    ///  Upstream's <c>ShowFor</c>: shown over <paramref name="owner"/>, titled with the viewer's file and whether the search
    ///  keeps to the selection, the text to find selected.
    /// </summary>
    public void ShowFor(Window owner, string? fileName, bool selectionOnly)
    {
        ViewModel.FileName = fileName;
        ViewModel.SelectionOnly = selectionOnly;
        if (!IsVisible)
        {
            Position = new PixelPoint(owner.Position.X + OwnerOffset, owner.Position.Y + OwnerOffset);
            Show(owner);
        }

        Activate();
        txtLookFor.SelectAll();
        txtLookFor.Focus();
    }

    /// <summary>
    ///  Upstream's <c>ScanRegionChanged</c>: the title says whether the search keeps to the selection.
    /// </summary>
    public void SetSelectionOnly(bool selectionOnly) => ViewModel.SelectionOnly = selectionOnly;
}

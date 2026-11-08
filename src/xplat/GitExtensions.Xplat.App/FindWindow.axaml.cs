using Avalonia;
using Avalonia.Controls;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FindAndReplaceForm</c>, one per viewer and kept while it lives: closing hides it, as
///  upstream's does, so the next Find shows it again with the last search. The viewer runs the searches it asks for.
/// </summary>
public partial class FindWindow : Window
{
    // Upstream's texts.
    public const string FindText = "Find";
    public const string FindAndReplaceText = "Find & replace";
    public const string SelectionOnlyText = "selection only";

    // Upstream shows the form this far right of and below its owner's corner.
    private const int OwnerOffset = 100;

    private string? _fileName;
    private bool _selectionOnly;
    private bool _canHighlightAll = true;

    public FindWindow()
    {
        InitializeComponent();
        UpstreamTranslation.Apply(this, "FindAndReplaceForm");
        btnFindNext.Click += (_, _) => FindNextRequested?.Invoke(this, false);
        btnFindPrevious.Click += (_, _) => FindNextRequested?.Invoke(this, true);
        btnHighlightAll.Click += (_, _) => HighlightAllRequested?.Invoke(this, EventArgs.Empty);
        btnReplace.Click += (_, _) => ReplaceRequested?.Invoke(this, EventArgs.Empty);
        btnReplaceAll.Click += (_, _) => ReplaceAllRequested?.Invoke(this, EventArgs.Empty);
        btnCancel.Click += (_, _) => Close();

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
    ///  Raised when the user closed the window, which hides it.
    /// </summary>
    public event EventHandler? Hidden;

    /// <summary>
    ///  Upstream's <c>LookFor</c>: the text to find.
    /// </summary>
    public string LookFor
    {
        get => txtLookFor.Text ?? "";
        set => txtLookFor.Text = value;
    }

    /// <summary>
    ///  Upstream's <c>ReplaceMode</c>: the replace controls show, and Replace takes the place of highlighting.
    /// </summary>
    public bool ReplaceMode
    {
        get => txtReplaceWith.IsVisible;
        set
        {
            txtReplaceWith.IsVisible = lblReplaceWith.IsVisible = btnReplace.IsVisible = btnReplaceAll.IsVisible = value;
            btnReplace.IsDefault = value;
            btnFindNext.IsDefault = !value;
            btnHighlightAll.IsVisible = !value && CanHighlightAll;
            UpdateTitleBar();
        }
    }

    /// <summary>
    ///  Whether the window's owner can mark every occurrence; a plain text box cannot.
    /// </summary>
    public bool CanHighlightAll
    {
        get => _canHighlightAll;
        set
        {
            _canHighlightAll = value;
            btnHighlightAll.IsVisible = value && !ReplaceMode;
        }
    }

    /// <summary>
    ///  Upstream's <c>txtReplaceWith</c>.
    /// </summary>
    public string ReplaceWith
    {
        get => txtReplaceWith.Text ?? "";
        set => txtReplaceWith.Text = value;
    }

    public bool MatchCase => chkMatchCase.IsChecked == true;

    public bool MatchWholeWord => chkMatchWholeWord.IsChecked == true;

    /// <summary>
    ///  Upstream's <c>ShowFor</c>: shown over <paramref name="owner"/>, titled with the viewer's file and whether the search
    ///  keeps to the selection, the text to find selected.
    /// </summary>
    public void ShowFor(Window owner, string? fileName, bool selectionOnly)
    {
        _fileName = fileName;
        SetSelectionOnly(selectionOnly);
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
    public void SetSelectionOnly(bool selectionOnly)
    {
        _selectionOnly = selectionOnly;
        UpdateTitleBar();
    }

    // Upstream's UpdateTitleBar.
    private void UpdateTitleBar()
    {
        string text = UpstreamTranslation.ByText(ReplaceMode ? FindAndReplaceText : FindText);
        if (_fileName is not null)
        {
            text += " - " + Path.GetFileName(_fileName);
        }

        if (_selectionOnly)
        {
            text += " (" + UpstreamTranslation.ByText(SelectionOnlyText) + ")";
        }

        Title = text;
    }
}

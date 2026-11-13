using Avalonia.Controls;
using GitExtensions.Xplat.Core.Presentation;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormGoToLine</c>, bound to <see cref="GoToLineViewModel"/>: the line number to go to, from 1 to the
///  viewer's last line number.
/// </summary>
public partial class GoToLineWindow : Window
{
    public GoToLineWindow()
    {
        ViewModel = new GoToLineViewModel(new WindowDialogHost(this));
        DataContext = ViewModel;
        InitializeComponent();
        UpstreamTranslation.Apply(this, GoToLineViewModel.Category);
        InitialFocus.Attach(this, () => _NO_TRANSLATE_LineNumberUpDown);
    }

    public GoToLineViewModel ViewModel { get; }

    /// <summary>
    ///  The line number chosen.
    /// </summary>
    public int LineNumber => ViewModel.LineNumber;

    /// <summary>
    ///  Upstream's <c>SetMaxLineNumber</c>: the last line and the label that names the range.
    /// </summary>
    public void SetMaxLineNumber(int maxLineNumber) => ViewModel.SetMaxLineNumber(maxLineNumber);

    /// <summary>
    ///  Shows the window over <paramref name="owner"/>; the line number for OK, null for Cancel.
    /// </summary>
    public Task<int?> ShowAsync(Window owner) => ShowDialog<int?>(owner);
}

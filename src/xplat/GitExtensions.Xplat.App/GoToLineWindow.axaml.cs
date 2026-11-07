using Avalonia.Controls;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormGoToLine</c>: the line number to go to, from 1 to the viewer's last line number.
/// </summary>
public partial class GoToLineWindow : Window
{
    private string _label = "";

    public GoToLineWindow()
    {
        InitializeComponent();
        UpstreamTranslation.Apply(this, "FormGoToLine");
        _label = lineLabel.Content as string ?? "";
        InitialFocus.Attach(this, () => _NO_TRANSLATE_LineNumberUpDown);
        okBtn.Click += (_, _) => Close(LineNumber);
        cancelBtn.Click += (_, _) => Close(null);
    }

    /// <summary>
    ///  The line number chosen.
    /// </summary>
    public int LineNumber => (int)(_NO_TRANSLATE_LineNumberUpDown.Value ?? 1);

    /// <summary>
    ///  Upstream's <c>SetMaxLineNumber</c>: the last line and the label that names the range.
    /// </summary>
    public void SetMaxLineNumber(int maxLineNumber)
    {
        _NO_TRANSLATE_LineNumberUpDown.Maximum = Math.Max(1, maxLineNumber);
        lineLabel.Content = _label + " (1 - " + maxLineNumber + "):";
    }

    /// <summary>
    ///  Shows the window over <paramref name="owner"/>; the line number for OK, null for Cancel.
    /// </summary>
    public Task<int?> ShowAsync(Window owner) => ShowDialog<int?>(owner);
}

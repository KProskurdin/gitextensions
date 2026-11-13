using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace GitExtensions.Xplat.Core.Presentation;

/// <summary>
///  The view model of upstream's <c>FormGoToLine</c>: the line number, 1 to the viewer's last. Closes its window with the
///  number for OK, null for Cancel.
/// </summary>
public sealed partial class GoToLineViewModel : DialogViewModel
{
    /// <summary>
    ///  The upstream form whose texts the window shows, by their names.
    /// </summary>
    public const string Category = "FormGoToLine";

    private const string LabelText = "Line number";

    private readonly string _label;

    public GoToLineViewModel(IDialogHost host)
        : base(host)
    {
        _label = host.Translate(Category, "lineLabel", LabelText);
        Label = _label;
        Line = 1;
        Maximum = 1;
    }

    /// <summary>
    ///  The label, with the range once it is known.
    /// </summary>
    [ObservableProperty]
    public partial string Label { get; private set; }

    /// <summary>
    ///  The number box's value (a decimal, as the box keeps it).
    /// </summary>
    [ObservableProperty]
    public partial decimal? Line { get; set; }

    [ObservableProperty]
    public partial decimal Maximum { get; private set; }

    /// <summary>
    ///  The line number chosen.
    /// </summary>
    public int LineNumber => (int)(Line ?? 1);

    /// <summary>
    ///  Upstream's <c>SetMaxLineNumber</c>: the last line and the label that names the range.
    /// </summary>
    public void SetMaxLineNumber(int maxLineNumber)
    {
        Maximum = Math.Max(1, maxLineNumber);
        Label = _label + " (1 - " + maxLineNumber + "):";
    }

    [RelayCommand]
    private void Ok() => Host.Close(LineNumber);

    [RelayCommand]
    private void Cancel() => Host.Close(null);
}

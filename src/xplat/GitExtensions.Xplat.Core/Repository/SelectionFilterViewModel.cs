using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GitExtensions.Xplat.Core.Repository;

/// <summary>
///  Upstream FormCommit's selection filter (<c>toolbarSelectionFilter</c>): a regular expression that selects the unstaged
///  files whose path it matches, ignoring case, once typing pauses; an empty one selects them all. An expression that
///  selected files is kept in the box's list, the latest first, up to upstream's ten. Ctrl+F shows and hides it; it shows
///  from the start when upstream's <c>commitdialogselectionfilter</c> is on.
/// </summary>
public sealed partial class SelectionFilterViewModel : ObservableObject
{
    public const string LabelText = "Selection Filter";
    public const string ToolTipText = "Enter a regular expression to select unstaged files.";
    public const string ErrorToolTipText = "Error {0}";

    // Upstream's SelectionFilterMaxLength.
    private const int MaxHistory = 10;

    private readonly Func<IReadOnlyList<FileChange>> _unstaged;
    private readonly Func<string, string> _translate;
    private int _typed;

    /// <param name="visible">Upstream's <c>CommitDialogSelectionFilter</c>.</param>
    /// <param name="unstaged">The files the unstaged list shows.</param>
    /// <param name="translate">Upstream's translation of a text.</param>
    public SelectionFilterViewModel(bool visible, Func<IReadOnlyList<FileChange>> unstaged, Func<string, string> translate)
    {
        _unstaged = unstaged;
        _translate = translate;
        IsVisible = visible;
        ToolTip = translate(ToolTipText);
    }

    /// <summary>
    ///  Raised with the files to select in the unstaged list.
    /// </summary>
    public event EventHandler<IReadOnlyList<FileChange>>? SelectRequested;

    /// <summary>
    ///  Upstream's throttle on the typed text.
    /// </summary>
    public static TimeSpan TypingPause { get; } = TimeSpan.FromMilliseconds(250);

    [ObservableProperty]
    public partial bool IsVisible { get; set; }

    [ObservableProperty]
    public partial string Text { get; set; } = "";

    /// <summary>
    ///  Upstream's tooltip of the box: the hint, or the expression's error.
    /// </summary>
    [ObservableProperty]
    public partial string ToolTip { get; private set; }

    /// <summary>
    ///  The expressions that selected files, the latest first.
    /// </summary>
    public ObservableCollection<string> History { get; } = [];

    /// <summary>
    ///  Upstream's <c>ToggleSelectionFilter</c>; true when the filter shows now.
    /// </summary>
    public bool Toggle() => IsVisible = !IsVisible;

    /// <summary>
    ///  Upstream's <c>Update</c>: selects the files <paramref name="expression"/> matches; the number selected.
    /// </summary>
    public int Apply(string expression)
    {
        IReadOnlyList<FileChange> matches;
        try
        {
            Regex? regex = string.IsNullOrEmpty(expression) ? null : new Regex(expression, RegexOptions.IgnoreCase);
            matches = [.. _unstaged().Where(change => regex?.IsMatch(change.Path) != false)];
            ToolTip = _translate(ToolTipText);
        }
        catch (ArgumentException ex)
        {
            ToolTip = string.Format(_translate(ErrorToolTipText), ex.Message);
            return 0;
        }

        SelectRequested?.Invoke(this, matches);
        if (matches.Count > 0)
        {
            Remember(expression);
        }

        return matches.Count;
    }

    // Upstream's OnSelectionFilterTextChanged: the selection follows once typing pauses. Choosing an expression of the
    // list (upstream's OnSelectionFilterIndexChanged) sets the text too, and applies the same way.
    partial void OnTextChanged(string value)
    {
        int typed = ++_typed;
        _ = ApplyAfterPauseAsync();
        return;

        async Task ApplyAfterPauseAsync()
        {
            await Task.Delay(TypingPause);
            if (typed == _typed)
            {
                Apply(value);
            }
        }
    }

    // Upstream's AddToSelectionFilter.
    private void Remember(string expression)
    {
        if (History.Contains(expression))
        {
            return;
        }

        while (History.Count >= MaxHistory)
        {
            History.RemoveAt(MaxHistory - 1);
        }

        History.Insert(0, expression);
    }
}

using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using GitCommands.Settings;
using GitExtensions.Xplat.Core.Diff;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Which selected-line action the window around a diff offers in its menu: upstream FileViewer shows "Stage selected
///  line(s)" for the working directory's diff and "Unstage selected line(s)" for the index's.
/// </summary>
public enum DiffLineAction
{
    None,
    Stage,
    Unstage,
}

// Upstream FileViewer's toolbar (fileviewerToolbar) and menu (contextMenu).
public partial class DiffView
{
    private bool _hasOptions = true;
    private bool _pointerOver;

    /// <summary>
    ///  The line action the menu offers; it raises <see cref="LinesHotkey"/> as the hotkey does.
    /// </summary>
    public DiffLineAction LineAction { get; set; }

    private void WireOptions()
    {
        ShowOptions();
        NextChangeButton.Click += (_, _) => GoToChange(next: true);
        PreviousChangeButton.Click += (_, _) => GoToChange(next: false);
        IncreaseContextButton.Click += (_, _) => ChangeOptions(_options.WithMoreContext());
        DecreaseContextButton.Click += (_, _) => ChangeOptions(_options.WithLessContext());
        IncreaseContextMenuItem.Click += (_, _) => ChangeOptions(_options.WithMoreContext());
        DecreaseContextMenuItem.Click += (_, _) => ChangeOptions(_options.WithLessContext());
        Toggle(EntireFileToggle, EntireFileMenuItem, () => _options.ToggleEntireFile());
        Toggle(NonPrintingToggle, NonPrintingMenuItem,
            () => _options with { ShowNonPrintingChars = !_options.ShowNonPrintingChars });
        Toggle(SyntaxHighlightingToggle, SyntaxHighlightingMenuItem,
            () => _options with { ShowSyntaxHighlighting = !_options.ShowSyntaxHighlighting });
        Toggle(IgnoreWhitespaceEolToggle, IgnoreWhitespaceEolMenuItem,
            () => _options.ToggleIgnoreWhitespace(IgnoreWhitespaceKind.Eol));
        Toggle(IgnoreWhitespaceChangeToggle, IgnoreWhitespaceChangeMenuItem,
            () => _options.ToggleIgnoreWhitespace(IgnoreWhitespaceKind.Change));
        Toggle(IgnoreAllWhitespaceToggle, IgnoreAllWhitespaceMenuItem,
            () => _options.ToggleIgnoreWhitespace(IgnoreWhitespaceKind.AllSpace));

        DiffContextMenu.Opening += (_, _) => ShowMenu();
        StageLinesMenuItem.Click += (_, _) => LinesHotkey?.Invoke(this, DiffCommand.StageLines);
        UnstageLinesMenuItem.Click += (_, _) => LinesHotkey?.Invoke(this, DiffCommand.UnstageLines);
        CopyMenuItem.Click += (_, _) => Copy(DiffCopy.Text(SelectedOrAllLines(), _textIsDiff));
        CopyPatchMenuItem.Click += (_, _) => Copy(DiffCopy.Patch(SelectedOrAllLines()));
        CopyNewVersionMenuItem.Click += (_, _) => Copy(DiffCopy.Version(SelectedOrAllLines(), _textIsDiff, newVersion: true));
        CopyOldVersionMenuItem.Click += (_, _) => Copy(DiffCopy.Version(SelectedOrAllLines(), _textIsDiff, newVersion: false));

        // As upstream, the toolbar shows while the pointer is on the viewer.
        PointerEntered += (_, _) =>
        {
            _pointerOver = true;
            ShowToolbar();
        };
        PointerExited += (_, _) =>
        {
            _pointerOver = false;
            ShowToolbar();
        };
        return;

        void Toggle(ToolToggleButton button, MenuItem item, Func<DiffOptions> change)
        {
            button.IsCheckedChanged += (_, _) =>
            {
                if (!_showingOptions)
                {
                    ChangeOptions(change());
                }
            };
            item.Click += (_, _) => ChangeOptions(change());
        }
    }

    private void ShowToolbar() => DiffToolbar.IsVisible = _hasOptions && _pointerOver;

    // The toggles and menu checks follow the options; as upstream, the context buttons are off while the entire file shows.
    private void ShowOptions()
    {
        _showingOptions = true;
        EntireFileToggle.IsChecked = EntireFileMenuItem.IsChecked = _options.ShowEntireFile;
        NonPrintingToggle.IsChecked = NonPrintingMenuItem.IsChecked = _options.ShowNonPrintingChars;
        SyntaxHighlightingToggle.IsChecked = SyntaxHighlightingMenuItem.IsChecked = _options.ShowSyntaxHighlighting;
        IgnoreWhitespaceEolToggle.IsChecked = IgnoreWhitespaceEolMenuItem.IsChecked =
            _options.IgnoreWhitespace == IgnoreWhitespaceKind.Eol;
        IgnoreWhitespaceChangeToggle.IsChecked = IgnoreWhitespaceChangeMenuItem.IsChecked =
            _options.IgnoreWhitespace == IgnoreWhitespaceKind.Change;
        IgnoreAllWhitespaceToggle.IsChecked = IgnoreAllWhitespaceMenuItem.IsChecked =
            _options.IgnoreWhitespace == IgnoreWhitespaceKind.AllSpace;
        bool canDecrease = !_options.ShowEntireFile && _options.ContextLines > 0;
        DecreaseContextButton.IsEnabled = DecreaseContextMenuItem.IsEnabled = canDecrease;
        IncreaseContextButton.IsEnabled = IncreaseContextMenuItem.IsEnabled = !_options.ShowEntireFile;
        ToolTip.SetTip(IncreaseContextButton,
            $"Increase the number of lines of context ({_options.ContextLines.ToString(CultureInfo.CurrentCulture)})");
        _showingOptions = false;
    }

    // Upstream's contextMenu_Opening: the line items only where the window can stage, the options only for a diff.
    private void ShowMenu()
    {
        bool selected = DiffList.Selection.SelectedIndexes.Count > 0 && _request is not null && _text is null;
        StageLinesMenuItem.IsVisible = LineAction == DiffLineAction.Stage;
        StageLinesMenuItem.IsEnabled = selected;
        UnstageLinesMenuItem.IsVisible = LineAction == DiffLineAction.Unstage;
        UnstageLinesMenuItem.IsEnabled = selected;
        CopyNewVersionMenuItem.IsVisible = CopyOldVersionMenuItem.IsVisible = _textIsDiff;
        foreach (Control item in new Control[]
                 {
                     OptionsSeparator, IncreaseContextMenuItem, DecreaseContextMenuItem, EntireFileMenuItem,
                     NonPrintingMenuItem, SyntaxHighlightingMenuItem, IgnoreWhitespaceEolMenuItem,
                     IgnoreWhitespaceChangeMenuItem, IgnoreAllWhitespaceMenuItem
                 })
        {
            item.IsVisible = _hasOptions;
        }
    }

    // Upstream's next and previous change buttons and hotkeys.
    private void GoToChange(bool next)
    {
        IReadOnlyList<DiffLine> lines = _text ?? _viewModel.Lines;
        if (DiffNavigation.FindChange(lines, DiffList.SelectedIndex, next) is { } index)
        {
            DiffList.SelectedIndex = index;
            DiffList.ScrollIntoView(index);
        }
    }

    // The selected lines in order, or all lines when none is selected, as upstream copies.
    private IReadOnlyList<DiffLine> SelectedOrAllLines()
    {
        IReadOnlyList<DiffLine> lines = _text ?? _viewModel.Lines;
        IReadOnlyList<int> selected = DiffList.Selection.SelectedIndexes;
        return selected.Count == 0
            ? lines
            : [.. selected.Where(index => index < lines.Count).Order().Select(index => lines[index])];
    }

    private void Copy(string text)
    {
        if (text.Length > 0 && TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
        {
            UiActions.Run(() => clipboard.SetTextAsync(text), _ => { });
        }
    }
}

using System.Globalization;
using System.Text;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using GitCommands;
using GitCommands.Settings;
using GitExtensions.Xplat.Core.Diff;
using GitExtensions.Xplat.Core.Repository;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Which selected-line actions the window around a diff offers in its menu (upstream FileViewer's
///  <c>SupportLinePatching</c>): "Stage selected line(s)" for the working directory's diff, "Unstage selected line(s)"
///  for the index's, "Stage selected line(s)" for a commit's diff too (it applies the lines to the working directory and
///  the index), and "Reset selected line(s)" for all three.
/// </summary>
public enum DiffLineAction
{
    None,
    Stage,
    Unstage,

    /// <summary>
    ///  A commit's diff: staging applies the lines (upstream's cherry pick of lines), resetting undoes them.
    /// </summary>
    Apply,
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
        EncodingBox.ItemsSource = AppSettings.AvailableEncodings.Values.Select(encoding => encoding.EncodingName).ToList();
        EncodingBox.SelectionChanged += (_, _) => ChooseEncoding();

        // Upstream's settingsButton_Click: the Diff viewer page of the settings.
        SettingsButton.Click += (_, _) =>
        {
            if (ShowSettings is { } showSettings && TopLevel.GetTopLevel(this) is Window owner)
            {
                UiActions.Run(() => showSettings(owner, DiffViewerSettingsPage), ex => ShowError(ex.Message));
            }
        };

        // Upstream's TreatAllFilesAsTextToolStripMenuItemClick: this viewer's choice only, never saved.
        TreatAllFilesAsTextMenuItem.Click += (_, _) =>
            ChangeOptions(_options with { TreatAllFilesAsText = !_options.TreatAllFilesAsText });
        StageLinesMenuItem.Click += (_, _) => LinesHotkey?.Invoke(this, DiffCommand.StageLines);
        UnstageLinesMenuItem.Click += (_, _) => LinesHotkey?.Invoke(this, DiffCommand.UnstageLines);
        ResetLinesMenuItem.Click += (_, _) => LinesHotkey?.Invoke(this, DiffCommand.ResetLines);
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

    private void ShowToolbar()
    {
        bool wasVisible = DiffToolbar.IsVisible;
        DiffToolbar.IsVisible = _hasOptions && _pointerOver;
        SettingsButton.IsVisible = ShowSettings is not null;

        // Upstream's fileviewerToolbar_VisibleChanged: the encoding box shows the viewer's encoding.
        if (DiffToolbar.IsVisible && !wasVisible)
        {
            ShowEncoding();
        }
    }

    // Upstream's DiffViewerSettingsPage.GetPageReference(), as the settings window names its pages.
    private const string DiffViewerSettingsPage = "DiffViewerSettingsPage";

    /// <summary>
    ///  Opens the settings window on a page over a window (the browse window's); null hides the toolbar's settings button.
    /// </summary>
    public static Func<Window, string, Task>? ShowSettings { get; set; }

    // Upstream's encodingToolStripComboBox: the viewer's encoding among upstream's available encodings (blank when it is
    // none of them, or when the viewer shows no repository's file).
    private void ShowEncoding()
    {
        _showingOptions = true;
        Encoding? encoding = _options.Encoding
                             ?? (_request?.RepositoryPath is { } path ? GitModules.Open(path).FilesEncoding : null);
        EncodingBox.SelectedItem = encoding?.EncodingName is { } name && EncodingBox.Items.Contains(name) ? name : null;
        _showingOptions = false;
    }

    // Upstream's encodingToolStripComboBox_SelectedIndexChanged: a chosen encoding reads the view again in it.
    private void ChooseEncoding()
    {
        if (_showingOptions || EncodingBox.SelectedItem is not string name
                            || AppSettings.AvailableEncodings.Values.FirstOrDefault(encoding => encoding.EncodingName == name)
                                is not { } chosen)
        {
            return;
        }

        ChangeOptions(_options with { Encoding = chosen });
    }

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
        TreatAllFilesAsTextMenuItem.IsChecked = _options.TreatAllFilesAsText;
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
        StageLinesMenuItem.IsVisible = LineAction is DiffLineAction.Stage or DiffLineAction.Apply;
        StageLinesMenuItem.IsEnabled = selected;
        UnstageLinesMenuItem.IsVisible = LineAction == DiffLineAction.Unstage;
        UnstageLinesMenuItem.IsEnabled = selected;
        ResetLinesMenuItem.IsVisible = LineAction != DiffLineAction.None;
        ResetLinesMenuItem.IsEnabled = selected;
        CopyNewVersionMenuItem.IsVisible = CopyOldVersionMenuItem.IsVisible = _textIsDiff;

        // Upstream's isPartialTextView: a diff or git-grep's matches, not a file's text.
        TreatAllFilesAsTextSeparator.IsVisible = TreatAllFilesAsTextMenuItem.IsVisible = IsPartialTextView;
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

    // Upstream's isPartialTextView: the view shows a diff or git-grep's matches.
    private bool IsPartialTextView => (_request is not null && _text is null) || _grepRead is not null;

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

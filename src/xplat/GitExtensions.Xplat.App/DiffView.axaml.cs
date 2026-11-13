using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using GitCommands.Settings;
using GitExtensions.Xplat.Core.Diff;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Shows the diff of one file, or of a whole commit, inside another window: the main window's Diff tab, the commit window
///  and the diff window all use it.
/// </summary>
public partial class DiffView : UserControl
{
    private readonly DiffViewModel _viewModel = new(new GitDiffService());
    private DiffRequest? _request;

    // The file whose syntax colors the lines (upstream's FileViewer picks the highlighting by the file name), and whether the
    // lines are a diff (a first column of +, - or space) or a file's text.
    private string? _syntaxPath;
    private bool _textIsDiff = true;
    private IReadOnlyList<DiffLine>? _text;

    // A file's git-grep matches, shown instead of a diff or a text (upstream's ViewMode.Grep), and how they are read for
    // the view's options.
    private IReadOnlyList<GrepLine>? _grep;
    private Func<DiffOptions, Task<(IReadOnlyList<GrepLine> Lines, string? Error)>>? _grepRead;
    private DiffOptions _options;

    // Upstream's line argument of ViewChangesAsync: the line to go to when the next lines are shown.
    private int? _lineToGoTo;

    // The item template's padding and line number columns, before a line's text.
    private const double LinePadding = 4;
    private const double LineNumbersWidth = 88;
    private bool _showingOptions;

    public DiffView()
    {
        InitializeComponent();
        _viewModel.PropertyChanged += (_, e) => OnDiffChanged(e.PropertyName);

        // As upstream's FileViewer, each view starts from the options of this run or the saved defaults; "Treat all files as
        // text" starts off in each.
        _options = AppServices.Preferences.InitialDiffOptions() with { TreatAllFilesAsText = false, Encoding = null };
        AppServices.Preferences.ReloadDiffDisplayAppearance();
        WireOptions();
        WireContinuousScroll();
        WireFind();
        WireFileContent();
        DiffList.SizeChanged += (_, _) => ShowVerticalRuler();
        DiffList.AddHandler(ScrollViewer.ScrollChangedEvent, (_, _) => ShowVerticalRuler());
        DiffList.PropertyChanged += (_, e) =>
        {
            if (e.Property == FontSizeProperty || e.Property == FontFamilyProperty)
            {
                ShowVerticalRuler();
            }
        };
        ActualThemeVariantChanged += (_, _) => ShowLines();

        // A switch between two themes of the same variant (e.g. dark and dark+) changes only the colors.
        AttachedToVisualTree += (_, _) => ThemeApplier.Changed += OnThemeChanged;
        DetachedFromVisualTree += (_, _) => ThemeApplier.Changed -= OnThemeChanged;
        DiffList.SelectionChanged += (_, _) => LineSelectionChanged?.Invoke(this, EventArgs.Empty);
        DiffList.AddHandler(KeyDownEvent, OnDiffKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }

    /// <summary>
    ///  Raised when the selected lines change, e.g. so a window can enable staging of the selection.
    /// </summary>
    public event EventHandler? LineSelectionChanged;

    /// <summary>
    ///  Raised for upstream FileViewer's stage and unstage lines hotkeys; the window that can stage handles them.
    /// </summary>
    public event EventHandler<DiffCommand>? LinesHotkey;

    /// <summary>
    ///  The lines on show, as the list displays them.
    /// </summary>
    public IReadOnlyList<DiffLineItem> Lines { get; private set; } = [];

    /// <summary>
    ///  True while a diff is being read.
    /// </summary>
    public bool IsLoading => _viewModel.IsLoading;

    /// <summary>
    ///  Shows the diff of <paramref name="filePath"/> in <paramref name="commitHash"/>, or of the working tree (staged or
    ///  unstaged) when no commit is given. A null file shows the whole commit.
    /// </summary>
    public async Task ShowAsync(string repositoryPath, string? commitHash, string? filePath, bool staged, string title)
    {
        int version = ResetContent();
        FileRevision revision = FileRevision.Of(commitHash, staged);
        if (filePath is not null && GitCommands.FileHelper.IsImage(filePath)
            && await ShowImageInsteadOfDiffAsync(version, repositoryPath, revision, filePath, title))
        {
            return;
        }

        DiffRequest request = new(repositoryPath, commitHash, filePath, staged);
        _request = request;
        _text = null;
        _grep = null;
        _grepRead = null;
        _syntaxPath = filePath;
        _textIsDiff = true;
        _hasOptions = true;
        ShowToolbar();
        TitleText.Text = title;
        await LoadAsync(request);
        await ShowNewBinaryFileAsync(version, request, revision, title);
    }

    /// <summary>
    ///  Shows the diff <paramref name="read"/> returns for the view's options (upstream's <c>FormDiff</c>, a file between
    ///  two revisions); it is read again when the options change.
    /// </summary>
    /// <param name="revision">The revision the diff goes to, whose image or new binary file is shown instead, as upstream's
    ///  <c>ViewChangesAsync</c>.</param>
    public async Task ShowDiffAsync(string repositoryPath, string? filePath, string title,
        Func<DiffOptions, Task<IReadOnlyList<DiffLine>>> read, FileRevision? revision = null)
    {
        int version = ResetContent();
        if (revision is not null && filePath is not null && GitCommands.FileHelper.IsImage(filePath)
            && await ShowImageInsteadOfDiffAsync(version, repositoryPath, revision, filePath, title))
        {
            return;
        }

        DiffRequest request = new(repositoryPath, CommitHash: null, filePath, Staged: false, read);
        _request = request;
        _text = null;
        _grep = null;
        _grepRead = null;
        _syntaxPath = filePath;
        _textIsDiff = true;
        _hasOptions = true;
        ShowToolbar();
        TitleText.Text = title;
        await LoadAsync(request);
        if (revision is not null)
        {
            await ShowNewBinaryFileAsync(version, request, revision, title);
        }
    }

    /// <summary>
    ///  Shows a file's content with line numbers instead of a diff, as upstream's file viewer does for the File tree tab. A
    ///  null <paramref name="content"/> is a binary file, which is not shown.
    /// </summary>
    public void ShowText(string title, string? content)
    {
        ResetContent();
        _request = null;
        _grep = null;
        _grepRead = null;
        TitleText.Text = title;
        _hasOptions = false;
        ShowToolbar();
        ShowError(content is null ? "Binary file, not shown." : null);
        _syntaxPath = title;
        _textIsDiff = false;
        _text = content is null ? [] : DiffParser.ParseText(content);
        ShowLines();
    }

    /// <summary>
    ///  Shows a patch that is not in the repository (a pull request's file), as upstream's <c>ViewFixedPatch</c>.
    /// </summary>
    /// <summary>
    ///  Shows lines read elsewhere, in their colors, such as a range diff's.
    /// </summary>
    public void ShowDiffLines(string title, IReadOnlyList<DiffLine> lines)
    {
        ResetContent();
        _request = null;
        _grep = null;
        _grepRead = null;
        TitleText.Text = title;
        _hasOptions = false;
        ShowToolbar();
        ShowError(null);
        _syntaxPath = null;
        _textIsDiff = false;
        _text = lines;
        ShowLines();
    }

    public void ShowPatch(string title, string patch)
    {
        ResetContent();
        _request = null;
        _grep = null;
        _grepRead = null;
        TitleText.Text = title;
        _hasOptions = false;
        ShowToolbar();
        _syntaxPath = title;
        _textIsDiff = true;
        ShowError(null);
        _text = DiffParser.Parse(patch);
        ShowLines();
    }

    /// <summary>
    ///  The diff as git printed it and the character range of the selected lines in it, as upstream's <c>PatchManager</c>
    ///  takes them; null when no line is selected or a file's content is shown instead of a diff.
    /// </summary>
    /// <summary>
    ///  The file's line number at the selected line (upstream FileViewer's <c>CurrentFileLine</c>): the new number, or the old
    ///  one for a removed line; null when no numbered line is selected.
    /// </summary>
    public int? CurrentFileLine
        => DiffList.SelectedItem is DiffLineItem line
           && int.TryParse(line.NewNumber.Length > 0 ? line.NewNumber : line.OldNumber, out int number)
            ? number
            : null;

    /// <summary>
    ///  The whole diff as git printed it, as upstream's <c>CherryPickAllChanges</c> applies it; null while no diff is shown.
    /// </summary>
    public (string Text, int Start, int Length)? WholeRange()
    {
        if (_request is null || _text is not null || _viewModel.Lines.Count == 0 || !CanPatchLines)
        {
            return null;
        }

        string text = string.Join('\n', _viewModel.Lines.Select(line => line.Text)) + "\n";
        return (text, 0, text.Length);
    }

    public (string Text, int Start, int Length)? SelectedRange()
    {
        IReadOnlyList<int> selected = DiffList.Selection.SelectedIndexes;
        IReadOnlyList<DiffLine> lines = _viewModel.Lines;
        if (_request is null || _text is not null || selected.Count == 0 || lines.Count == 0 || !CanPatchLines)
        {
            return null;
        }

        int first = selected.Min();
        int last = Math.Min(selected.Max(), lines.Count - 1);
        StringBuilder text = new();
        int start = 0;
        int end = 0;
        for (int i = 0; i < lines.Count; i++)
        {
            if (i == first)
            {
                start = text.Length;
            }

            text.Append(lines[i].Text);
            if (i == last)
            {
                end = text.Length;
            }

            text.Append('\n');
        }

        return (text.ToString(), start, end - start);
    }

    /// <summary>
    ///  Shows a file's git-grep matches as upstream's viewer does (<c>ViewGrepAsync</c>, <c>GrepHighlightService</c>): each
    ///  line with its number in the file, the matches in git's match color, the function lines and the separators as
    ///  headers; git's error instead when it failed. <paramref name="read"/> reads them again when the context changes.
    /// </summary>
    public Task ShowGrepAsync(string title,
        Func<DiffOptions, Task<(IReadOnlyList<GrepLine> Lines, string? Error)>> read)
    {
        ResetContent();
        _request = null;
        _text = null;
        _grepRead = read;
        TitleText.Text = title;
        _hasOptions = true;
        ShowToolbar();
        _syntaxPath = title;
        _textIsDiff = false;
        return LoadGrepAsync(read);
    }

    private async Task LoadGrepAsync(Func<DiffOptions, Task<(IReadOnlyList<GrepLine> Lines, string? Error)>> read)
    {
        (IReadOnlyList<GrepLine> lines, string? error) = await read(_options);
        if (_grepRead != read)
        {
            return;
        }

        ShowError(error);
        _grep = lines;
        ShowLines();
    }

    public void Clear()
    {
        ResetContent();
        _request = null;
        _text = null;
        _grep = null;
        _grepRead = null;
        TitleText.Text = "";
        ShowError(null);
        Lines = [];
        ResetFind();
        DiffList.ItemsSource = Lines;
    }

    private void Reload()
    {
        if (_grepRead is { } grepRead)
        {
            UiActions.Run(() => LoadGrepAsync(grepRead), ex => ShowError(ex.Message));
            return;
        }

        if (_request is { } request)
        {
            UiActions.Run(() => LoadAsync(request), ex => ShowError(ex.Message));
        }
    }

    private Task LoadAsync(DiffRequest request)
    {
        ShowError(null);
        DiffOptions options = _options with
        {
            Histogram = AppServices.Preferences.UseHistogramDiffAlgorithm,
            UseGitColoring = AppServices.Preferences.UseGitColoring,
            Appearance = AppServices.Preferences.DiffDisplayAppearance,
            DifftasticWidth = Difftastic.WidthFor(DiffList.Bounds.Width),
        };
        ShowDifftasticEnabled(request.RepositoryPath);
        return request.Read is { } read
            ? _viewModel.LoadAsync(() => read(options))
            : _viewModel.LoadAsync(request.RepositoryPath, request.CommitHash, request.FilePath, request.Staged, options);
    }

    private void OnDiffChanged(string? propertyName)
    {
        switch (propertyName)
        {
            case nameof(DiffViewModel.Lines):
                ShowLines();
                break;
            case nameof(DiffViewModel.ErrorMessage):
                if (_viewModel.ErrorMessage is { } message)
                {
                    _viewModel.ClearError();
                    ShowError(message);
                }

                break;
        }
    }

    // Brushes follow the theme, so a theme switch rebuilds the items. As upstream's FileViewer, a diff is highlighted only
    // while "Show syntax highlighting" is on, a file's text always.
    private void ShowLines()
    {
        if (_grep is { } grep)
        {
            Lines = [.. grep.Select(GrepItem)];
            ResetFind();
            DiffList.ItemsSource = Lines;
            ShowVerticalRuler();
            return;
        }

        SyntaxHighlighter? highlighter = (_options.ShowSyntaxHighlighting || !_textIsDiff) && _syntaxPath is { } path
            && SyntaxDefinitions.ForFile(path) is { } syntax
                ? new SyntaxHighlighter(syntax)
                : null;
        Lines =
        [
            .. (_text ?? _viewModel.Lines).Select(line => WithDimmedRuns(line, line.ColorRuns is null
                ? new DiffLineItem(DisplayText(line),
                    ThemeBrushes.ForegroundFor(line.Kind), ThemeBrushes.BackgroundFor(line.Kind),
                    line.OldNumber?.ToString(CultureInfo.InvariantCulture) ?? "",
                    line.NewNumber?.ToString(CultureInfo.InvariantCulture) ?? "") { Runs = Highlight(highlighter, line), }
                : ColoredItem(highlighter, line)))
        ];
        ResetFind();
        DiffList.ItemsSource = Lines;
        ShowVerticalRuler();
        ApplyPendingScroll();
        if (_lineToGoTo is { } lineToGoTo && Lines.Count > 0)
        {
            _lineToGoTo = null;
            GoToLine(lineToGoTo);
        }
    }

    // Upstream's AddInlineDifferenceMarkers: what the line shares with the one it replaces goes dimmed over its other colors.
    private DiffLineItem WithDimmedRuns(DiffLine line, DiffLineItem item)
    {
        if (line.DimRuns is not { Count: > 0 } dims)
        {
            return item;
        }

        DiffColorRun?[] dimAt = new DiffColorRun?[item.Text.Length];
        foreach (DiffColorRun dim in dims)
        {
            int start = DisplayOffset(line, dim.Start);
            int end = Math.Min(DisplayOffset(line, dim.Start + dim.Length), dimAt.Length);
            for (int i = start; i < end; i++)
            {
                dimAt[i] = dim;
            }
        }

        IReadOnlyList<DiffTextRun> runs = item.Runs is { } own && string.Concat(own.Select(run => run.Text)) == item.Text
            ? own
            : [new DiffTextRun(item.Text, null, false, false)];
        List<DiffTextRun> result = [];
        int position = 0;
        foreach (DiffTextRun run in runs)
        {
            int partStart = 0;
            for (int i = 1; i <= run.Text.Length; i++)
            {
                if (i == run.Text.Length || dimAt[position + i] != dimAt[position + partStart])
                {
                    DiffColorRun? dim = dimAt[position + partStart];
                    result.Add(dim is null
                        ? run with { Text = run.Text[partStart..i] }
                        : run with
                        {
                            Text = run.Text[partStart..i],
                            Foreground = dim.Foreground is { } fore ? ThemeBrushes.OfColor(fore) : run.Foreground,
                            Background = dim.Background is { } back ? ThemeBrushes.OfColor(back) : run.Background,
                        });
                    partStart = i;
                }
            }

            position += run.Text.Length;
        }

        return item with { Runs = result };
    }

    // Where a position of a line's text is on show: the non-printing marks put a mark before each tab.
    private int DisplayOffset(DiffLine line, int offset)
    {
        if (!_options.ShowNonPrintingChars)
        {
            return offset;
        }

        int prefix = _textIsDiff && line.Text.Length > 0 ? 1 : 0;
        int tabs = 0;
        for (int i = prefix; i < offset && i < line.Text.Length; i++)
        {
            if (line.Text[i] == '\t')
            {
                tabs++;
            }
        }

        return offset + tabs;
    }

    // Upstream's git coloring: the colors are git's, not the line's kind's. The file's syntax colors show where git colored
    // nothing, such as on a context line, which is where upstream's show through too: a change's foreground overrides them.
    private DiffLineItem ColoredItem(SyntaxHighlighter? highlighter, DiffLine line)
    {
        IReadOnlyList<DiffTextRun>? syntax = Highlight(highlighter, line);
        return new DiffLineItem(DisplayText(line), ThemeBrushes.ForegroundFor(DiffLineKind.Context), Background: null,
            line.OldNumber?.ToString(CultureInfo.InvariantCulture) ?? "",
            line.NewNumber?.ToString(CultureInfo.InvariantCulture) ?? "")
        {
            Runs = line.ColorRuns is { Count: > 0 } colors ? GitColorRuns(line, colors) : syntax,
        };
    }

    private List<DiffTextRun> GitColorRuns(DiffLine line, IReadOnlyList<DiffColorRun> colors)
    {
        bool marks = _options.ShowNonPrintingChars;
        List<DiffTextRun> runs = [];
        int position = 0;
        foreach (DiffColorRun color in colors)
        {
            if (color.Start > position)
            {
                runs.Add(Run(line.Text[position..color.Start], null, null));
            }

            runs.Add(Run(line.Text.Substring(color.Start, color.Length), color.Foreground, color.Background));
            position = color.Start + color.Length;
        }

        if (position < line.Text.Length)
        {
            runs.Add(Run(line.Text[position..], null, null));
        }

        if (marks)
        {
            runs.Add(new DiffTextRun(EndOfLineMark(line), null, false, false));
        }

        return runs;

        DiffTextRun Run(string text, System.Drawing.Color? foreground, System.Drawing.Color? background)
            => new(marks ? NonPrintingCharacters.Mark(text) : text,
                foreground is { } fore ? ThemeBrushes.OfColor(fore) : null, false, false)
            {
                Background = background is { } back ? ThemeBrushes.OfColor(back) : null,
            };
    }

    // Upstream's GetDiffLineInfo for grep: a function line and a separator as headers, the matches marked.
    private static DiffLineItem GrepItem(GrepLine line)
    {
        string number = line.Number?.ToString(CultureInfo.InvariantCulture) ?? "";
        return line.Kind switch
        {
            GrepLineKind.Function => new DiffLineItem(line.Text, ThemeBrushes.ForegroundFor(DiffLineKind.Header),
                ThemeBrushes.BackgroundFor(DiffLineKind.Hunk), NewNumber: number),
            GrepLineKind.Separator => new DiffLineItem(line.Text, ThemeBrushes.ForegroundFor(DiffLineKind.Header)),
            _ => new DiffLineItem(line.Text, ThemeBrushes.ForegroundFor(DiffLineKind.Context), NewNumber: number)
            {
                Runs = MatchRuns(line)
            },
        };
    }

    // The line in parts, git's matches bold in the theme's match color (upstream's red of git's "color.grep.match").
    private static IReadOnlyList<DiffTextRun>? MatchRuns(GrepLine line)
    {
        if (line.Matches.Count == 0)
        {
            return null;
        }

        List<DiffTextRun> runs = [];
        int position = 0;
        foreach ((int start, int length) in line.Matches)
        {
            if (start > position)
            {
                runs.Add(new DiffTextRun(line.Text[position..start], null, false, false));
            }

            runs.Add(new DiffTextRun(line.Text.Substring(start, length), ThemeBrushes.Current.Removed, true, false));
            position = start + length;
        }

        if (position < line.Text.Length)
        {
            runs.Add(new DiffTextRun(line.Text[position..], null, false, false));
        }

        return runs;
    }

    // Upstream's nonprinting characters mark a line's spaces, tabs and end, not a diff line's +, - or space.
    private string DisplayText(DiffLine line)
    {
        if (!_options.ShowNonPrintingChars || line.Kind is DiffLineKind.Header or DiffLineKind.Hunk)
        {
            return line.Text;
        }

        int prefix = _textIsDiff && line.Text.Length > 0 ? 1 : 0;
        return line.Text[..prefix] + NonPrintingCharacters.Mark(line.Text[prefix..]) + EndOfLineMark(line);
    }

    private static string EndOfLineMark(DiffLine line)
        => NonPrintingCharacters.EndOfLineMark(line.Ending, AppServices.Preferences.ShowEolMarkerAsGlyph);

    // Upstream's ruler at the column the setting gives, counted from the line's start as upstream's editor counts it.
    private void ShowVerticalRuler()
    {
        int column = AppServices.Preferences.DiffVerticalRulerPosition;
        VerticalRuler.IsVisible = column > 0 && Lines.Count > 0;
        if (!VerticalRuler.IsVisible)
        {
            return;
        }

        FormattedText digit = new("0", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(DiffList.FontFamily, DiffList.FontStyle, DiffList.FontWeight), DiffList.FontSize, null);
        double x = LinePadding + LineNumbersWidth + (column * digit.WidthIncludingTrailingWhitespace)
                   - (DiffList.Scroll?.Offset.X ?? 0);
        VerticalRuler.StartPoint = new Point(x, 0);
        VerticalRuler.EndPoint = new Point(x, DiffList.Bounds.Height);
    }

    private void OnThemeChanged(object? sender, EventArgs e) => ShowLines();

    // The syntax colors of a line's code (after the +, - or space of a diff line); a hunk starts the highlighting again, as
    // its first line may be in the middle of a comment.
    private IReadOnlyList<DiffTextRun>? Highlight(SyntaxHighlighter? highlighter, DiffLine line)
    {
        if (highlighter is null || line.Kind == DiffLineKind.Header)
        {
            return null;
        }

        if (line.Kind == DiffLineKind.Hunk)
        {
            highlighter.Reset();
            return null;
        }

        int prefix = _textIsDiff && line.Text.Length > 0 ? 1 : 0;
        List<DiffTextRun> runs = prefix > 0 ? [new DiffTextRun(line.Text[..prefix], null, false, false)] : [];
        string code = line.Text[prefix..];
        bool marks = _options.ShowNonPrintingChars;
        foreach (SyntaxRun run in highlighter.HighlightLine(code))
        {
            string text = code.Substring(run.Start, run.Length);
            runs.Add(new DiffTextRun(marks ? NonPrintingCharacters.Mark(text) : text,
                run.Style?.Color is { } color ? ThemeBrushes.Syntax(color) : null,
                run.Style?.Bold == true, run.Style?.Italic == true));
        }

        if (marks)
        {
            runs.Add(new DiffTextRun(EndOfLineMark(line), null, false, false));
        }

        return runs;
    }

    // Upstream FileViewer's hotkeys, while the diff has the focus.
    private void OnDiffKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (RunHotkey(e))
        {
            e.Handled = true;
        }
    }

    /// <summary>
    ///  Upstream FileViewer's <c>ExecuteCommand</c> for a key: true when the viewer took it. A window that reads its own
    ///  hotkeys before its controls asks the viewer first while it has the focus, as upstream's inner controls come first.
    /// </summary>
    public bool RunHotkey(Avalonia.Input.KeyEventArgs e)
    {
        if (Hotkeys.Diff.Match(e) is not { } command)
        {
            return false;
        }

        switch (command)
        {
            case DiffCommand.NextChange:
            case DiffCommand.PreviousChange:
                GoToChange(next: command == DiffCommand.NextChange);
                return true;
            case DiffCommand.IgnoreAllWhitespace:
                ChangeOptions(_options.ToggleIgnoreWhitespace(IgnoreWhitespaceKind.AllSpace));
                return true;
            case DiffCommand.IncreaseContext:
                ChangeOptions(_options.WithMoreContext());
                return true;
            case DiffCommand.DecreaseContext:
                ChangeOptions(_options.WithLessContext());
                return true;
            case DiffCommand.ShowEntireFile:
                ChangeOptions(_options.ToggleEntireFile());
                return true;
            case DiffCommand.StageLines:
            case DiffCommand.UnstageLines:
            case DiffCommand.ResetLines:
                if (LinesHotkey is { } handler)
                {
                    handler(this, command);
                    return true;
                }

                return false;

            // Upstream's replace item finds in a viewer that cannot be edited, and every viewer here is such a one.
            case DiffCommand.Find:
            case DiffCommand.Replace:
                Find();
                return true;

            // Upstream's FindNextAsync(searchForwardOrOpenWithDifftool): with nothing to look for, F3 is the window's
            // "open with difftool".
            case DiffCommand.FindNextOrOpenWithDifftool:
                if (_findWindow is not { LookFor.Length: > 0 })
                {
                    return false;
                }

                UiActions.Run(() => FindNextAsync(viaF3: true, searchBackward: false, TextNotFoundText),
                    ex => ShowError(ex.Message));
                return true;
            case DiffCommand.FindPrevious:
                UiActions.Run(() => FindNextAsync(viaF3: true, searchBackward: true, TextNotFoundText),
                    ex => ShowError(ex.Message));
                return true;
            case DiffCommand.GoToLine:
                UiActions.Run(GoToLineAsync, ex => ShowError(ex.Message));
                return true;

            case DiffCommand.TreatFileAsText:
                if (!IsPartialTextView)
                {
                    return false;
                }

                ChangeOptions(_options with { TreatAllFilesAsText = !_options.TreatAllFilesAsText });
                return true;

            // Upstream's PerformClickIfAvailable: only while the menu offers the item.
            case DiffCommand.ShowGitWordColoring:
                ShowMenu();
                if (!DiffAppearanceMenuItem.IsVisible)
                {
                    return false;
                }

                ToggleAppearance(DiffDisplayAppearance.GitWordDiff);
                return true;
            case DiffCommand.ShowDifftastic:
                ShowMenu();
                if (!DiffAppearanceMenuItem.IsVisible || !ShowDifftasticMenuItem.IsEnabled)
                {
                    return false;
                }

                ToggleAppearance(DiffDisplayAppearance.Difftastic);
                return true;

            // As above.
            case DiffCommand.ShowSyntaxHighlighting:
                ShowMenu();
                if (!SyntaxHighlightingMenuItem.IsVisible || !SyntaxHighlightingMenuItem.IsEnabled)
                {
                    return false;
                }

                ChangeOptions(_options with { ShowSyntaxHighlighting = !_options.ShowSyntaxHighlighting });
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    ///  The whitespace, context and entire file choices in effect.
    /// </summary>
    public DiffOptions Options => _options;

    // As upstream's FileViewer: the change is kept for the next view in this run and the diff is read again.
    private void ChangeOptions(DiffOptions options)
    {
        if (options == _options)
        {
            return;
        }

        // Nonprinting characters and syntax highlighting change only the display; the diff is not read again for them.
        bool sameDiff = options.Arguments.SequenceEqual(_options.Arguments) && Equals(options.Encoding, _options.Encoding);
        _options = options;
        AppServices.Preferences.SetDiffOptions(options);
        ShowOptions();
        if (sameDiff)
        {
            ShowLines();
        }
        else
        {
            Reload();
        }
    }

    private void ShowError(string? message)
    {
        ErrorText.Text = message ?? "";
        ErrorText.IsVisible = message is not null;

        // A search waiting for the next file's lines goes on after its error too.
        if (message is not null)
        {
            _linesShown?.TrySetResult();
        }
    }

    private sealed record DiffRequest(string RepositoryPath, string? CommitHash, string? FilePath, bool Staged,
        Func<DiffOptions, Task<IReadOnlyList<DiffLine>>>? Read = null);
}

/// <summary>
///  A diff line as the list shows it, with its old and new line numbers (empty where the line has none). As upstream's
///  diff viewer, added, removed and hunk lines are marked by their background.
/// </summary>
public sealed record DiffLineItem(
    string Text,
    IBrush Foreground,
    IBrush? Background = null,
    string OldNumber = "",
    string NewNumber = "")
{
    /// <summary>
    ///  The line in parts with their syntax colors, or null when the file has no syntax definition.
    /// </summary>
    public IReadOnlyList<DiffTextRun>? Runs { get; init; }
}

/// <summary>
///  A part of a line with its syntax color (null for the line's color), bold and italic.
/// </summary>
public sealed record DiffTextRun(string Text, IBrush? Foreground, bool Bold, bool Italic)
{
    /// <summary>
    ///  The part's background, e.g. a found occurrence's mark; null for the line's.
    /// </summary>
    public IBrush? Background { get; init; }
}

/// <summary>
///  A text block that shows <see cref="Runs"/> in their colors when there are any, otherwise its text.
/// </summary>
public sealed class SyntaxTextBlock : TextBlock
{
    public static readonly StyledProperty<IReadOnlyList<DiffTextRun>?> RunsProperty =
        AvaloniaProperty.Register<SyntaxTextBlock, IReadOnlyList<DiffTextRun>?>(nameof(Runs));

    public IReadOnlyList<DiffTextRun>? Runs
    {
        get => GetValue(RunsProperty);
        set => SetValue(RunsProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(TextBlock);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != RunsProperty)
        {
            return;
        }

        if (Runs is not { } runs)
        {
            Inlines = null;
            return;
        }

        InlineCollection inlines = [];
        foreach (DiffTextRun run in runs)
        {
            Run inline = new(run.Text);
            if (run.Foreground is not null)
            {
                inline.Foreground = run.Foreground;
            }

            if (run.Bold)
            {
                inline.FontWeight = FontWeight.Bold;
            }

            if (run.Italic)
            {
                inline.FontStyle = FontStyle.Italic;
            }

            if (run.Background is not null)
            {
                inline.Background = run.Background;
            }

            inlines.Add(inline);
        }

        Inlines = inlines;
    }
}

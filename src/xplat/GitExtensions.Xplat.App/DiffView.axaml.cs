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
    private DiffOptions _options;
    private bool _showingOptions;

    public DiffView()
    {
        InitializeComponent();
        _viewModel.PropertyChanged += (_, e) => OnDiffChanged(e.PropertyName);

        // As upstream's FileViewer, each view starts from the options of this run or the saved defaults.
        _options = AppServices.Preferences.InitialDiffOptions();
        WhitespaceBox.ItemsSource = WhitespaceOption.All;
        ShowOptions();
        WhitespaceBox.SelectionChanged += (_, _) =>
        {
            if (!_showingOptions && WhitespaceBox.SelectedItem is WhitespaceOption option)
            {
                ChangeOptions(_options with { IgnoreWhitespace = option.Kind });
            }
        };
        DecreaseContextButton.Click += (_, _) => ChangeOptions(_options.WithLessContext());
        IncreaseContextButton.Click += (_, _) => ChangeOptions(_options.WithMoreContext());
        EntireFileToggle.IsCheckedChanged += (_, _) =>
        {
            if (!_showingOptions)
            {
                ChangeOptions(_options.ToggleEntireFile());
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
    public Task ShowAsync(string repositoryPath, string? commitHash, string? filePath, bool staged, string title)
    {
        _request = new DiffRequest(repositoryPath, commitHash, filePath, staged);
        _text = null;
        _syntaxPath = filePath;
        _textIsDiff = true;
        OptionsPanel.IsVisible = true;
        TitleText.Text = title;
        return LoadAsync(_request);
    }

    /// <summary>
    ///  Shows a file's content with line numbers instead of a diff, as upstream's file viewer does for the File tree tab. A
    ///  null <paramref name="content"/> is a binary file, which is not shown.
    /// </summary>
    public void ShowText(string title, string? content)
    {
        _request = null;
        TitleText.Text = title;
        OptionsPanel.IsVisible = false;
        ShowError(content is null ? "Binary file, not shown." : null);
        _syntaxPath = title;
        _textIsDiff = false;
        _text = content is null ? [] : DiffParser.ParseText(content);
        ShowLines();
    }

    /// <summary>
    ///  Shows a patch that is not in the repository (a pull request's file), as upstream's <c>ViewFixedPatch</c>.
    /// </summary>
    public void ShowPatch(string title, string patch)
    {
        _request = null;
        TitleText.Text = title;
        OptionsPanel.IsVisible = false;
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

    public (string Text, int Start, int Length)? SelectedRange()
    {
        IReadOnlyList<int> selected = DiffList.Selection.SelectedIndexes;
        IReadOnlyList<DiffLine> lines = _viewModel.Lines;
        if (_request is null || _text is not null || selected.Count == 0 || lines.Count == 0)
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

    public void Clear()
    {
        _request = null;
        _text = null;
        TitleText.Text = "";
        ShowError(null);
        Lines = [];
        DiffList.ItemsSource = Lines;
    }

    private void Reload()
    {
        if (_request is { } request)
        {
            UiActions.Run(() => LoadAsync(request), ex => ShowError(ex.Message));
        }
    }

    private Task LoadAsync(DiffRequest request)
    {
        ShowError(null);
        return _viewModel.LoadAsync(request.RepositoryPath, request.CommitHash, request.FilePath, request.Staged,
            _options with { Histogram = AppServices.Preferences.UseHistogramDiffAlgorithm });
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

    // Brushes follow the theme, so a theme switch rebuilds the items.
    private void ShowLines()
    {
        SyntaxHighlighter? highlighter = _syntaxPath is { } path && SyntaxDefinitions.ForFile(path) is { } syntax
            ? new SyntaxHighlighter(syntax)
            : null;
        Lines =
        [
            .. (_text ?? _viewModel.Lines).Select(line => new DiffLineItem(line.Text,
                ThemeBrushes.ForegroundFor(line.Kind), ThemeBrushes.BackgroundFor(line.Kind),
                line.OldNumber?.ToString(CultureInfo.InvariantCulture) ?? "",
                line.NewNumber?.ToString(CultureInfo.InvariantCulture) ?? "") { Runs = Highlight(highlighter, line), })
        ];
        DiffList.ItemsSource = Lines;
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
        foreach (SyntaxRun run in highlighter.HighlightLine(code))
        {
            runs.Add(new DiffTextRun(code.Substring(run.Start, run.Length),
                run.Style?.Color is { } color ? ThemeBrushes.Syntax(color) : null,
                run.Style?.Bold == true, run.Style?.Italic == true));
        }

        return runs;
    }

    // Upstream FileViewer's hotkeys, while the diff has the focus.
    private void OnDiffKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (Hotkeys.Diff.Match(e) is not { } command)
        {
            return;
        }

        switch (command)
        {
            case DiffCommand.NextChange:
            case DiffCommand.PreviousChange:
                IReadOnlyList<DiffLine> lines = _text ?? _viewModel.Lines;
                if (DiffNavigation.FindChange(lines, DiffList.SelectedIndex, command == DiffCommand.NextChange) is
                    { } index)
                {
                    DiffList.SelectedIndex = index;
                    DiffList.ScrollIntoView(index);
                }

                e.Handled = true;
                break;
            case DiffCommand.IgnoreAllWhitespace:
                ChangeOptions(_options.ToggleIgnoreWhitespace(IgnoreWhitespaceKind.AllSpace));
                e.Handled = true;
                break;
            case DiffCommand.IncreaseContext:
                ChangeOptions(_options.WithMoreContext());
                e.Handled = true;
                break;
            case DiffCommand.DecreaseContext:
                ChangeOptions(_options.WithLessContext());
                e.Handled = true;
                break;
            case DiffCommand.ShowEntireFile:
                ChangeOptions(_options.ToggleEntireFile());
                e.Handled = true;
                break;
            case DiffCommand.StageLines:
            case DiffCommand.UnstageLines:
                if (LinesHotkey is { } handler)
                {
                    handler(this, command);
                    e.Handled = true;
                }

                break;
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

        _options = options;
        AppServices.Preferences.SetDiffOptions(options);
        ShowOptions();
        Reload();
    }

    private void ShowOptions()
    {
        _showingOptions = true;
        WhitespaceBox.SelectedItem = WhitespaceOption.All.First(option => option.Kind == _options.IgnoreWhitespace);
        ContextText.Text = _options.ContextLines.ToString(CultureInfo.InvariantCulture);
        EntireFileToggle.IsChecked = _options.ShowEntireFile;

        // As upstream, the context buttons are off while the entire file is shown.
        DecreaseContextButton.IsEnabled = !_options.ShowEntireFile && _options.ContextLines > 0;
        IncreaseContextButton.IsEnabled = !_options.ShowEntireFile;
        ContextText.Opacity = _options.ShowEntireFile ? 0.4 : 1;
        _showingOptions = false;
    }

    private void ShowError(string? message)
    {
        ErrorText.Text = message ?? "";
        ErrorText.IsVisible = message is not null;
    }

    private sealed record DiffRequest(string RepositoryPath, string? CommitHash, string? FilePath, bool Staged);
}

/// <summary>
///  A choice of the whitespace box, named as upstream's FileViewer menu names it.
/// </summary>
public sealed record WhitespaceOption(IgnoreWhitespaceKind Kind, string Label)
{
    public static IReadOnlyList<WhitespaceOption> All { get; } =
    [
        new(IgnoreWhitespaceKind.None, "Show all whitespace changes"),
        new(IgnoreWhitespaceKind.Eol, "Ignore whitespace changes at end of line"),
        new(IgnoreWhitespaceKind.Change, "Ignore changes in amount of whitespace"),
        new(IgnoreWhitespaceKind.AllSpace, "Ignore all whitespace changes"),
    ];

    public override string ToString() => Label;
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
public sealed record DiffTextRun(string Text, IBrush? Foreground, bool Bold, bool Italic);

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

            inlines.Add(inline);
        }

        Inlines = inlines;
    }
}

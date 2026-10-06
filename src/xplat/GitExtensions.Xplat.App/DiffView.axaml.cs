using System.Globalization;
using System.Text;
using Avalonia.Controls;
using Avalonia.Media;
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
    private IReadOnlyList<DiffLine>? _text;

    public DiffView()
    {
        InitializeComponent();
        _viewModel.PropertyChanged += (_, e) => OnDiffChanged(e.PropertyName);
        IgnoreWhitespaceCheck.IsCheckedChanged += (_, _) => Reload();
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
        IgnoreWhitespaceCheck.IsVisible = true;
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
        IgnoreWhitespaceCheck.IsVisible = false;
        ShowError(content is null ? "Binary file, not shown." : null);
        _text = content is null ? [] : DiffParser.ParseText(content);
        ShowLines();
    }

    /// <summary>
    ///  The diff as git printed it and the character range of the selected lines in it, as upstream's <c>PatchManager</c>
    ///  takes them; null when no line is selected or a file's content is shown instead of a diff.
    /// </summary>
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
            IgnoreWhitespaceCheck.IsChecked == true);
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
        Lines =
        [
            .. (_text ?? _viewModel.Lines).Select(line => new DiffLineItem(line.Text,
                ThemeBrushes.ForegroundFor(line.Kind), ThemeBrushes.BackgroundFor(line.Kind),
                line.OldNumber?.ToString(CultureInfo.InvariantCulture) ?? "",
                line.NewNumber?.ToString(CultureInfo.InvariantCulture) ?? ""))
        ];
        DiffList.ItemsSource = Lines;
    }

    private void OnThemeChanged(object? sender, EventArgs e) => ShowLines();

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
                IgnoreWhitespaceCheck.IsChecked = IgnoreWhitespaceCheck.IsChecked != true;
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

    private void ShowError(string? message)
    {
        ErrorText.Text = message ?? "";
        ErrorText.IsVisible = message is not null;
    }

    private sealed record DiffRequest(string RepositoryPath, string? CommitHash, string? FilePath, bool Staged);
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
    string NewNumber = "");

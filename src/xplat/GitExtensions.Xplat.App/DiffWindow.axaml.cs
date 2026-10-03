using Avalonia.Controls;
using Avalonia.Media;
using GitExtensions.Xplat.Core.Diff;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Shows the diff of one file, either in a commit or in the working tree (staged or unstaged).
/// </summary>
public partial class DiffWindow : Window
{
    private static readonly IBrush _added = new SolidColorBrush(Color.Parse("#1A7F37"));
    private static readonly IBrush _removed = new SolidColorBrush(Color.Parse("#CF222E"));
    private static readonly IBrush _hunk = new SolidColorBrush(Color.Parse("#0969DA"));
    private static readonly IBrush _header = new SolidColorBrush(Color.Parse("#57606A"));
    private static readonly IBrush _context = Brushes.Black;

    private readonly DiffViewModel _viewModel;
    private readonly string _repositoryPath;
    private readonly string? _commitHash;
    private readonly string _filePath;
    private readonly bool _staged;

    public DiffWindow(string repositoryPath, string? commitHash, string filePath, bool staged)
    {
        _repositoryPath = repositoryPath;
        _commitHash = commitHash;
        _filePath = filePath;
        _staged = staged;
        _viewModel = new DiffViewModel(new GitDiffService());
        InitializeComponent();
        Title = commitHash is null
            ? $"{(staged ? "Staged" : "Unstaged")}: {filePath}"
            : $"{commitHash[..Math.Min(8, commitHash.Length)]}: {filePath}";
        _viewModel.PropertyChanged += (_, e) => OnDiffChanged(e.PropertyName);
        Opened += OnOpened;
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        await _viewModel.LoadAsync(_repositoryPath, _commitHash, _filePath, _staged);
    }

    private void OnDiffChanged(string? propertyName)
    {
        switch (propertyName)
        {
            case nameof(DiffViewModel.Lines):
                DiffList.ItemsSource = _viewModel.Lines.Select(line => new DiffLineItem(line.Text, BrushFor(line.Kind))).ToList();
                break;
            case nameof(DiffViewModel.ErrorMessage):
                ErrorText.Text = _viewModel.ErrorMessage ?? "";
                break;
        }
    }

    private static IBrush BrushFor(DiffLineKind kind) => kind switch
    {
        DiffLineKind.Added => _added,
        DiffLineKind.Removed => _removed,
        DiffLineKind.Hunk => _hunk,
        DiffLineKind.Header => _header,
        _ => _context,
    };
}

public sealed record DiffLineItem(string Text, IBrush Foreground);

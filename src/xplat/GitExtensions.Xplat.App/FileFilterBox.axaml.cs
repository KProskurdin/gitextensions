using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Media;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Ui;
using GitExtUtils.GitUI.Theming;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream FileStatusList's file filter (<c>cboFilterComboBox</c>, <c>DeleteFilterButton</c>), bound to
///  <see cref="FileFilterViewModel"/>. The box is green while it filters and red with the error as its tooltip when the
///  expression is invalid; the colors are the view's.
/// </summary>
public partial class FileFilterBox : UserControl
{
    public FileFilterBox()
    {
        InitializeComponent();
        ViewModel = new FileFilterViewModel(AppServices.Preferences, UpstreamTranslation.ByText);
        LayoutRoot.DataContext = ViewModel;
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(FileFilterViewModel.State))
            {
                ShowState();
            }
        };

        // Upstream: a filter picked from the drop-down applies at once.
        cboFilterComboBox.SelectionChanged += (_, _) =>
        {
            if (!ViewModel.IsSettingText && cboFilterComboBox.SelectedItem is string)
            {
                ViewModel.ApplyNow();
            }
        };
    }

    public FileFilterViewModel ViewModel { get; }

    /// <inheritdoc cref="FileFilterViewModel.FilterChanged"/>
    public event EventHandler? FilterChanged
    {
        add => ViewModel.FilterChanged += value;
        remove => ViewModel.FilterChanged -= value;
    }

    /// <summary>
    ///  The filter's text; set, it applies at once (upstream's <c>SetFilter</c>).
    /// </summary>
    public string Text
    {
        get => ViewModel.Text;
        set => ViewModel.SetFilter(value);
    }

    /// <inheritdoc cref="FileFilterViewModel.WorkingDirectory"/>
    public Func<string?> WorkingDirectory
    {
        get => ViewModel.WorkingDirectory;
        set => ViewModel.WorkingDirectory = value;
    }

    /// <inheritdoc cref="FileFilterViewModel.Filter"/>
    public Regex? Filter => ViewModel.Filter;

    /// <inheritdoc cref="FileFilterViewModel.Error"/>
    public string? Error => ViewModel.Error;

    /// <inheritdoc cref="FileFilterViewModel.HasText"/>
    public bool IsActive => ViewModel.HasText;

    /// <inheritdoc cref="FileFilterViewModel.IsMatch"/>
    public bool IsMatch(string path, string? oldPath = null) => ViewModel.IsMatch(path, oldPath);

    /// <inheritdoc cref="FileFilterViewModel.Shown"/>
    public void Shown(int fileCount) => ViewModel.Shown(fileCount);

    // Upstream's colors of the box, adapted to the theme.
    private void ShowState()
    {
        if (ViewModel.State == FileFilterState.None)
        {
            cboFilterComboBox.ClearValue(BackgroundProperty);
            return;
        }

        System.Drawing.Color color = (ViewModel.State == FileFilterState.Valid
            ? System.Drawing.Color.FromArgb(0xC8, 0xFF, 0xC8)
            : System.Drawing.Color.FromArgb(0xFF, 0xC8, 0xC8)).AdaptBackColor();
        cboFilterComboBox.Background = new SolidColorBrush(Color.FromRgb(color.R, color.G, color.B));
    }
}

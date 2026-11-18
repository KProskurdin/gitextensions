using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Ui;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>CommitPickerSmallControl</c>: the chosen commit's short hash, which can be typed (a hash or ref, resolved
///  when the box is left and refused with upstream's message when git does not know it) or picked with the button in the
///  choose commit window (upstream's <c>buttonPickCommit_Click</c>), and <c>GetCommitCountString</c> from HEAD to it, bound
///  to <see cref="CommitPickerViewModel"/>. A window's view model binds <see cref="Selected"/> (two-way) and
///  <see cref="Module"/>; code-behind windows set them and call <see cref="SetCommitAsync"/>.
/// </summary>
public partial class CommitPickerView : UserControl
{
    // Upstream's text.
    public const string InvalidCommitHash = CommitPickerViewModel.InvalidCommitHash;
    public const string ErrorCaption = CommitPickerViewModel.ErrorCaption;

    /// <summary>
    ///  The chosen commit; null when none is.
    /// </summary>
    public static readonly StyledProperty<ObjectId?> SelectedProperty =
        AvaloniaProperty.Register<CommitPickerView, ObjectId?>(nameof(Selected), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    ///  The repository the commits are resolved in; set before the first <see cref="SetCommitAsync"/>.
    /// </summary>
    public static readonly StyledProperty<IGitModule?> ModuleProperty =
        AvaloniaProperty.Register<CommitPickerView, IGitModule?>(nameof(Module));

    public CommitPickerView()
    {
        InitializeComponent();

        // The view model is the inner panel's data context, so the window's own reaches the control (MVVM.md, Controls).
        LayoutRoot.DataContext = ViewModel;
        ViewModel.PickCommit = PickCommitAsync;
        ViewModel.ShowError = ShowErrorAsync;
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CommitPickerViewModel.Selected))
            {
                SetCurrentValue(SelectedProperty, ViewModel.Selected);
            }
        };

        // View concern: a typed revision is resolved when the box is left.
        CommitHashBox.LostFocus += (_, _) => _ = ViewModel.SetCommitAsync(ViewModel.HashText.Trim());
    }

    /// <summary>
    ///  Raised when another commit is chosen.
    /// </summary>
    public event EventHandler? SelectedChanged;

    public CommitPickerViewModel ViewModel { get; } = new(UpstreamTranslation.ByText);

    public ObjectId? Selected
    {
        get => GetValue(SelectedProperty);
        set => SetValue(SelectedProperty, value);
    }

    public IGitModule? Module
    {
        get => GetValue(ModuleProperty);
        set => SetValue(ModuleProperty, value);
    }

    /// <summary>
    ///  Upstream's <c>SetSelectedCommitHash</c> (<see cref="CommitPickerViewModel.SetCommitAsync"/>).
    /// </summary>
    public Task SetCommitAsync(string? revision) => ViewModel.SetCommitAsync(revision);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectedProperty)
        {
            ViewModel.Selected = Selected;
            SelectedChanged?.Invoke(this, EventArgs.Empty);
        }
        else if (change.Property == ModuleProperty)
        {
            ViewModel.Module = Module;
        }
    }

    // Upstream's buttonPickCommit_Click: the choose commit window over this control's window.
    private async Task<string?> PickCommitAsync(string? selected)
        => Module is { } module && TopLevel.GetTopLevel(this) is Window owner
            ? (await new ChooseCommitWindow(module.WorkingDir, selected, AppServices.Preferences)
                .ShowDialog<Core.CommitHistory.CommitRow?>(owner))?.Hash
            : null;

    private Task ShowErrorAsync(string message, string caption)
        => TopLevel.GetTopLevel(this) is Window owner
            ? new MessageBoxWindow(message, caption, MessageBoxButtons.OK).ShowDialog(owner)
            : Task.CompletedTask;
}

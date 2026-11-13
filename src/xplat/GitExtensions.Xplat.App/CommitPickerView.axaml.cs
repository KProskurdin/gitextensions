using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Ui;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>CommitPickerSmallControl</c>: the chosen commit's short hash, which can be typed (a hash or ref, resolved
///  when the box is left and refused with upstream's message when git does not know it) or picked with the button in the
///  choose commit window (upstream's <c>buttonPickCommit_Click</c>), and <c>GetCommitCountString</c> from HEAD to it. A
///  view model binds <see cref="Selected"/> (two-way) and <see cref="Module"/>; code-behind windows set them and call
///  <see cref="SetCommitAsync"/>.
/// </summary>
public partial class CommitPickerView : UserControl
{
    // Upstream's text.
    public const string InvalidCommitHash = "The given commit hash is not valid for this repository and was therefore discarded.";
    public const string ErrorCaption = "Error";

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

    private int _version;

    public CommitPickerView()
    {
        InitializeComponent();
        CommitHashBox.LostFocus += (_, _) => UiActions.Run(() => SetCommitAsync(CommitHashBox.Text?.Trim() ?? ""), _ => { });
        buttonPickCommit.Click += (_, _) => UiActions.Run(PickCommitAsync, _ => { });
    }

    /// <summary>
    ///  Raised when another commit is chosen.
    /// </summary>
    public event EventHandler? SelectedChanged;

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
    ///  Upstream's <c>SetSelectedCommitHash</c>: resolves <paramref name="revision"/>; an unknown one is refused (with a
    ///  message when something was typed) and the previous commit kept.
    /// </summary>
    public async Task SetCommitAsync(string? revision)
    {
        if (Module is not { } module || (revision ?? "") == (Selected?.ToShortString() ?? ""))
        {
            return;
        }

        ObjectId id = string.IsNullOrWhiteSpace(revision) ? default : await Task.Run(() => module.RevParse(revision));
        if (id.IsZero)
        {
            if (!string.IsNullOrWhiteSpace(revision) && TopLevel.GetTopLevel(this) is Window owner)
            {
                await new MessageBoxWindow(UpstreamTranslation.ByText(InvalidCommitHash), UpstreamTranslation.ByText(ErrorCaption),
                    MessageBoxButtons.OK).ShowDialog(owner);
            }

            CommitHashBox.Text = Selected?.ToShortString() ?? "";
            return;
        }

        if (id == Selected)
        {
            // The same commit by another name: shown by its short hash again.
            CommitHashBox.Text = id.ToShortString();
            return;
        }

        Selected = id;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectedProperty)
        {
            ShowSelected();
            SelectedChanged?.Invoke(this, EventArgs.Empty);
        }
        else if (change.Property == ModuleProperty)
        {
            ShowSelected();
        }
    }

    // Upstream's buttonPickCommit_Click: the choose commit window on the chosen commit.
    private async Task PickCommitAsync()
    {
        if (Module is not { } module || TopLevel.GetTopLevel(this) is not Window owner)
        {
            return;
        }

        if (await new ChooseCommitWindow(module.WorkingDir, Selected?.ToString(), AppServices.Preferences)
                .ShowDialog<Core.CommitHistory.CommitRow?>(owner) is { } row)
        {
            await SetCommitAsync(row.Hash);
        }
    }

    // The chosen commit's short hash, and upstream's GetCommitCountString from HEAD to it, read off the UI thread; a later
    // choice wins.
    private void ShowSelected()
    {
        int version = ++_version;
        CommitHashBox.Text = Selected?.ToShortString() ?? "";
        CommitCountText.Text = "";
        if (Selected is not { } id || Module is not { } module)
        {
            return;
        }

        UiActions.Run(
            async () =>
            {
                string count = await Task.Run(() =>
                {
                    ObjectId current = module.GetCurrentCheckout();
                    return current.IsZero ? "" : module.GetCommitCountString(current, id.ToString());
                });
                if (version == _version)
                {
                    CommitCountText.Text = count;
                }
            },
            _ => { });
    }
}

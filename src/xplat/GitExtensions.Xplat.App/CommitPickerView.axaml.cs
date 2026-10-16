using Avalonia.Controls;
using GitCommands;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Ui;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>CommitPickerSmallControl</c> without its button (upstream's FormChooseCommit is not in the new shell): the
///  chosen commit's short hash, which can be typed (a hash or ref, resolved when the box is left and refused with upstream's
///  message when git does not know it), and <c>GetCommitCountString</c> from HEAD to it.
/// </summary>
public partial class CommitPickerView : UserControl
{
    // Upstream's text.
    public const string InvalidCommitHash = "The given commit hash is not valid for this repository and was therefore discarded.";
    public const string ErrorCaption = "Error";

    private int _version;

    public CommitPickerView()
    {
        InitializeComponent();
        CommitHashBox.LostFocus += (_, _) => UiActions.Run(() => SetCommitAsync(CommitHashBox.Text?.Trim() ?? ""), _ => { });
    }

    /// <summary>
    ///  Raised when another commit is chosen.
    /// </summary>
    public event EventHandler? SelectedChanged;

    /// <summary>
    ///  The chosen commit; null when none is.
    /// </summary>
    public ObjectId? Selected { get; private set; }

    /// <summary>
    ///  The repository the commits are resolved in; set before the first <see cref="SetCommitAsync"/>.
    /// </summary>
    public GitModule? Module { get; set; }

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

        int version = ++_version;
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

        Selected = id;
        CommitHashBox.Text = id.ToShortString();
        CommitCountText.Text = "";
        SelectedChanged?.Invoke(this, EventArgs.Empty);
        string count = await Task.Run(() =>
        {
            ObjectId current = module.GetCurrentCheckout();
            return current.IsZero ? "" : module.GetCommitCountString(current, id.ToString());
        });
        if (version == _version)
        {
            CommitCountText.Text = count;
        }
    }
}

using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  Upstream's <c>FormCheckoutRevision</c> without its window: the commit to check out (detaching HEAD), whether local
///  changes are reset, the check before checking out and the checkout command.
/// </summary>
public sealed class CheckoutRevisionDialog
{
    // Upstream's texts.
    public const string NoRevisionSelected = "Select 1 revision to checkout.";
    public const string NoRevisionSelectedCaption = "Checkout";

    public ObjectId? Revision { get; set; }

    /// <summary>
    ///  Upstream's "Force (reset local changes)".
    /// </summary>
    public bool Force { get; set; }

    /// <summary>
    ///  Upstream's <c>OkClick</c> check: a commit is chosen. Returns the error to show, or null.
    /// </summary>
    public string? Validate() => Revision is { IsZero: false } ? null : NoRevisionSelected;

    /// <summary>
    ///  Upstream's <c>Commands.Checkout</c> of the commit, with <c>--force</c> when forced.
    /// </summary>
    public ArgumentString Arguments()
        => Commands.Checkout((Revision ?? default).ToString(),
            Force ? LocalChangesAction.Reset : LocalChangesAction.DontChange);
}

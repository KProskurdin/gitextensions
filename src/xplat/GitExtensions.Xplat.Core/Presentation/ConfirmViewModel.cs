using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace GitExtensions.Xplat.Core.Presentation;

/// <summary>
///  The view model of a question with a confirm and a cancel button and, as upstream's <c>ConfirmSuppressible</c>, an
///  optional "Don't show me this message again". Closes its window with true when confirmed.
/// </summary>
public sealed partial class ConfirmViewModel : DialogViewModel
{
    // The window's title when the caller gives none.
    private const string DefaultCaption = "Git Extensions";

    /// <param name="offerDontShowAgain">Shows the check box that turns the question off.</param>
    /// <param name="dontShowAgainText">The check box's text; upstream's "Don't show me this message again" when null.</param>
    public ConfirmViewModel(IDialogHost host, string message, string confirmText, string? caption = null,
        bool offerDontShowAgain = false, string? cancelText = null, string? dontShowAgainText = null)
        : base(host)
    {
        Message = message;
        ConfirmText = confirmText;
        Caption = caption ?? DefaultCaption;
        CancelText = cancelText ?? host.Translate("Cancel");
        OfferDontShowAgain = offerDontShowAgain;
        DontShowAgainText = dontShowAgainText ?? Settings.Confirmations.DontShowAgain;
    }

    public string Message { get; }

    public string ConfirmText { get; }

    public string Caption { get; }

    public string CancelText { get; }

    public bool OfferDontShowAgain { get; }

    public string DontShowAgainText { get; }

    /// <summary>
    ///  True when the user ticked "Don't show me this message again".
    /// </summary>
    [ObservableProperty]
    public partial bool DontShowAgain { get; set; }

    [RelayCommand]
    private void Confirm() => Host.Close(true);

    [RelayCommand]
    private void Cancel() => Host.Close(false);
}

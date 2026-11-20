using Avalonia.Controls;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Platform;
using GitExtensions.Xplat.Ui;
using DialogResult = System.Windows.Forms.DialogResult;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream FormRemoteProcess's and FormClone's windows after a plink failure (<see cref="IPuttyPrompts"/>):
///  <see cref="PuttyErrorWindow"/>, the host key and fingerprint questions with upstream's texts, and plink in a console
///  window. The windows open over <paramref name="owner"/>'s window at the time.
/// </summary>
public sealed class PuttyPrompts(Func<Window> owner) : IPuttyPrompts
{
    // The upstream classes whose texts the questions are.
    private const string MessageBoxesCategory = "MessageBoxes";
    private const string RemoteProcessCategory = "FormRemoteProcess";

    public async Task<PuttyKeyAnswer?> AskForKeyAsync()
    {
        PuttyErrorWindow window = new();
        await window.ShowDialog(owner());
        return window.Answer;
    }

    public async Task<bool> ConfirmAsync(PuttyQuestion question)
    {
        (string text, string caption) = question == PuttyQuestion.CacheHostKey
            ? (UpstreamTranslation.Text(MessageBoxesCategory, "_serverHostkeyNotCachedText", PuttyFailure.HostKeyNotCachedText),
                PuttyFailure.HostKeyNotCachedCaption)
            : (UpstreamTranslation.Text(RemoteProcessCategory, "_fingerprintNotRegistredText",
                    PuttyFailure.FingerprintNotRegisteredText),
                UpstreamTranslation.Text(RemoteProcessCategory, "_fingerprintNotRegistredTextCaption",
                    PuttyFailure.FingerprintNotRegisteredCaption));
        MessageBoxWindow box = new(text, caption, MessageBoxButtons.YesNo);
        await box.ShowDialog(owner());
        return box.Result == DialogResult.Yes;
    }

    public Task<bool> ConnectAsync(string url) => Putty.ConnectAsync(url);
}

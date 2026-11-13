using Avalonia.Controls;
using GitExtensions.Xplat.Core.Presentation;
using GitExtensions.Xplat.Ui;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;

namespace GitExtensions.Xplat.App;

/// <summary>
///  A window as its view model's <see cref="IDialogHost"/>: upstream's translations by English text, the shell's message
///  box and error window, and closing.
/// </summary>
internal sealed class WindowDialogHost(Window window) : IDialogHost
{
    public string Translate(string english) => UpstreamTranslation.ByText(english);

    public string Translate(string category, string name, string english) => UpstreamTranslation.Text(category, name, english);

    public Task ShowMessageAsync(string text, string? caption = null)
        => new MessageBoxWindow(text, caption ?? window.Title ?? "", MessageBoxButtons.OK).ShowDialog(window);

    public async Task<bool> ConfirmAsync(string text, string? caption = null)
    {
        MessageBoxWindow question = new(text, caption ?? window.Title ?? "", MessageBoxButtons.YesNo);
        await question.ShowDialog(window);
        return question.Result == System.Windows.Forms.DialogResult.Yes;
    }

    public void ShowError(Exception exception) => _ = new ErrorWindow(exception.Message).ShowDialog(window);

    public void Close(object? result) => window.Close(result);
}

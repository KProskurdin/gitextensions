using GitExtensions.Xplat.Core.Presentation;

namespace GitExtensions.Xplat.Core.Tests;

/// <summary>
///  Records what a view model asks of its window. Translations are marked, so a test sees which texts went through
///  <see cref="IDialogHost.Translate(string)"/>.
/// </summary>
internal sealed class FakeDialogHost : IDialogHost
{
    public const string TranslatedMark = "[t]";

    public List<string> Messages { get; } = [];

    public List<Exception> Errors { get; } = [];

    public List<string> Questions { get; } = [];

    /// <summary>
    ///  The answer to every question: Yes unless a test says No.
    /// </summary>
    public bool Answer { get; set; } = true;

    public bool IsClosed { get; private set; }

    public object? Result { get; private set; }

    public string Translate(string english) => TranslatedMark + english;

    public string Translate(string category, string name, string english) => TranslatedMark + english;

    public Task ShowMessageAsync(string text, string? caption = null)
    {
        Messages.Add(text);
        return Task.CompletedTask;
    }

    public Task<bool> ConfirmAsync(string text, string? caption = null)
    {
        Questions.Add(text);
        return Task.FromResult(Answer);
    }

    public void ShowError(Exception exception) => Errors.Add(exception);

    public void Close(object? result)
    {
        IsClosed = true;
        Result = result;
    }
}

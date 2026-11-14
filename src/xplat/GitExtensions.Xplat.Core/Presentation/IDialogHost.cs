namespace GitExtensions.Xplat.Core.Presentation;

/// <summary>
///  What a window's view model asks of its window (PLAN.md principle 6): upstream's translations, messages, errors and
///  closing. The app implements it once for every window; tests implement it to record what the view model asked.
/// </summary>
public interface IDialogHost
{
    /// <summary>
    ///  Upstream's translation of the English text <paramref name="english"/>, or the text itself. Texts that a view model
    ///  exposes for binding go through this: the window's translation pass, which replaces upstream's English texts when the
    ///  window is loaded, then finds nothing to override them with.
    /// </summary>
    string Translate(string english);

    /// <summary>
    ///  Upstream's translation of its text <paramref name="name"/> (a <c>TranslationString</c> field or a control) in
    ///  <paramref name="category"/> (the upstream form), or <paramref name="english"/>: for a window that reimplements an
    ///  upstream form and translates by its fields' names.
    /// </summary>
    string Translate(string category, string name, string english);

    /// <summary>
    ///  Shows <paramref name="text"/> with an OK button, with the window's title as the caption unless
    ///  <paramref name="caption"/> is given, and completes when it is closed.
    /// </summary>
    Task ShowMessageAsync(string text, string? caption = null);

    /// <summary>
    ///  Asks <paramref name="text"/> with Yes and No buttons (caption as for <see cref="ShowMessageAsync"/>), No the
    ///  default button when <paramref name="defaultIsNo"/>; true for Yes.
    /// </summary>
    Task<bool> ConfirmAsync(string text, string? caption = null, bool defaultIsNo = false);

    /// <summary>
    ///  Asks <paramref name="text"/> with Yes, No and Cancel buttons (caption as for <see cref="ShowMessageAsync"/>): true
    ///  for Yes, false for No, null for Cancel or a closed question. No is the default button when
    ///  <paramref name="defaultIsNo"/>.
    /// </summary>
    Task<bool?> AskAsync(string text, string? caption = null, bool defaultIsNo = false);

    /// <summary>
    ///  Shows an error that stopped a command, as the windows show such errors.
    /// </summary>
    void ShowError(Exception exception);

    /// <summary>
    ///  Closes the window with <paramref name="result"/>.
    /// </summary>
    void Close(object? result);
}

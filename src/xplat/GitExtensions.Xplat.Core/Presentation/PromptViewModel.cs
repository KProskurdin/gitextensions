using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace GitExtensions.Xplat.Core.Presentation;

/// <summary>
///  What a prompt window answered: its value, and the second value when it asked for one.
/// </summary>
public sealed record PromptResult(string Value, string SecondValue);

/// <summary>
///  The view model of the shell's prompt (upstream's simple prompts and its script prompt): a value, optionally a second
///  one, and OK or Cancel. Closes its window with a <see cref="PromptResult"/>, or null when cancelled or empty.
/// </summary>
public sealed partial class PromptViewModel : DialogViewModel
{
    private readonly bool _allowEmpty;

    /// <param name="secondPlaceholder">The second value's placeholder; no second value when empty.</param>
    /// <param name="allowEmpty">Accept an empty value (a script's user input may be empty, as upstream's prompt allows).</param>
    public PromptViewModel(IDialogHost host, string title, string label, string acceptText,
        string secondPlaceholder = "",
        string initialValue = "", bool allowEmpty = false)
        : base(host)
    {
        _allowEmpty = allowEmpty;
        Title = title;
        Label = label;
        AcceptText = acceptText;
        SecondPlaceholder = secondPlaceholder;
        Value = initialValue;
        SecondValue = "";
    }

    public string Title { get; }

    public string Label { get; }

    public string AcceptText { get; }

    public string SecondPlaceholder { get; }

    /// <summary>
    ///  A second value is asked for.
    /// </summary>
    public bool HasSecondValue => SecondPlaceholder.Length > 0;

    [ObservableProperty]
    public partial string Value { get; set; }

    [ObservableProperty]
    public partial string SecondValue { get; set; }

    [RelayCommand]
    private void Accept()
    {
        string value = Value.Trim();
        Host.Close(value.Length == 0 && !_allowEmpty ? null : new PromptResult(value, SecondValue.Trim()));
    }

    [RelayCommand]
    private void Cancel() => Host.Close(null);
}

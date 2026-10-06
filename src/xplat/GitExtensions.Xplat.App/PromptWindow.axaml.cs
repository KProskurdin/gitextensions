namespace GitExtensions.Xplat.App;

/// <summary>
///  What the user entered in a <see cref="PromptWindow"/>: the name, and the optional second value (e.g. a tag message).
/// </summary>
public sealed record PromptResult(string Value, string SecondValue);

/// <summary>
///  Asks for a name, such as a new branch or tag name. Closes with null when cancelled or when the name is empty.
/// </summary>
public partial class PromptWindow : Avalonia.Controls.Window
{
    private readonly bool _allowEmpty;

    /// <param name="allowEmpty">Accept an empty value (a script's user input may be empty, as upstream's prompt allows).</param>
    public PromptWindow(string title, string label, string acceptText, string secondPlaceholder = "",
        string initialValue = "",
        bool allowEmpty = false)
    {
        _allowEmpty = allowEmpty;
        InitializeComponent();
        Title = title;
        LabelText.Text = label;
        OkButton.Content = acceptText;
        ValueBox.Text = initialValue;
        SecondValueBox.IsVisible = secondPlaceholder.Length > 0;
        SecondValueBox.PlaceholderText = secondPlaceholder;
        CancelButton.Click += (_, _) => Close(null);
        OkButton.Click += (_, _) => Accept();
        Opened += (_, _) => ValueBox.Focus();
    }

    private void Accept()
    {
        string value = ValueBox.Text?.Trim() ?? "";
        Close(value.Length == 0 && !_allowEmpty ? null : new PromptResult(value, SecondValueBox.Text?.Trim() ?? ""));
    }
}

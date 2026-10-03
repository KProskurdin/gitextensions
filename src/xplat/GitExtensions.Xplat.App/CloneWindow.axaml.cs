using Avalonia.Controls;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Asks for the source and the target folder of a clone. Closes with a <see cref="CloneRequest"/>, or null when cancelled.
/// </summary>
public partial class CloneWindow : Window
{
    public CloneWindow()
    {
        InitializeComponent();
        CancelButton.Click += (_, _) => Close(null);
        CloneButton.Click += (_, _) => Close(CreateRequest());
    }

    private CloneRequest? CreateRequest()
    {
        string url = UrlBox.Text?.Trim() ?? "";
        string target = TargetBox.Text?.Trim() ?? "";
        return url.Length == 0 || target.Length == 0 ? null : new CloneRequest(url, target);
    }
}

public sealed record CloneRequest(string Url, string TargetPath);

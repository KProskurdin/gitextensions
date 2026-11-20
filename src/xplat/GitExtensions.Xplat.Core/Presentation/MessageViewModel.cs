using CommunityToolkit.Mvvm.Input;

namespace GitExtensions.Xplat.Core.Presentation;

/// <summary>
///  The view model of a window that shows a text and closes with one button (the shell's error window).
/// </summary>
public sealed partial class MessageViewModel(IDialogHost host, string message, string details = "")
    : DialogViewModel(host)
{
    public string Message { get; } = message;

    /// <summary>
    ///  Upstream's details of a failed command (exit code, command, arguments, folder); empty for none.
    /// </summary>
    public string Details { get; } = details.TrimEnd();

    public bool HasDetails => Details.Length > 0;

    [RelayCommand]
    private void Close() => Host.Close(null);
}

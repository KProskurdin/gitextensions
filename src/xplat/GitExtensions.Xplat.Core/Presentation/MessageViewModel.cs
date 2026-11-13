using CommunityToolkit.Mvvm.Input;

namespace GitExtensions.Xplat.Core.Presentation;

/// <summary>
///  The view model of a window that shows a text and closes with one button (the shell's error window).
/// </summary>
public sealed partial class MessageViewModel(IDialogHost host, string message) : DialogViewModel(host)
{
    public string Message { get; } = message;

    [RelayCommand]
    private void Close() => Host.Close(null);
}

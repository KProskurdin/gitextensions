using CommunityToolkit.Mvvm.Input;

namespace GitExtensions.Xplat.Core.Presentation;

/// <summary>
///  The view model of a window that shows a titled text to read and copy from (upstream's
///  <c>SimpleHelpDisplayDialog</c>), closed with one button.
/// </summary>
public sealed partial class TextViewModel(IDialogHost host, string title, string content) : DialogViewModel(host)
{
    public string Title { get; } = title;

    public string Content { get; } = content;

    [RelayCommand]
    private void Close() => Host.Close(null);
}

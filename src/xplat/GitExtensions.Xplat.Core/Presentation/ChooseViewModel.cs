using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace GitExtensions.Xplat.Core.Presentation;

/// <summary>
///  The view model of upstream's <c>FormQuickGitRefSelector</c> and <c>FormQuickStringSelector</c>: pick one of several
///  refs or remotes. Closes its window with the chosen one, or null.
/// </summary>
public sealed partial class ChooseViewModel : DialogViewModel
{
    public ChooseViewModel(IDialogHost host, IReadOnlyList<string> options)
        : base(host)
    {
        Options = options;
        Selected = options.Count > 0 ? options[0] : null;
    }

    public IReadOnlyList<string> Options { get; }

    [ObservableProperty]
    public partial string? Selected { get; set; }

    /// <summary>
    ///  Closes with the chosen option; the window also runs it on a double click.
    /// </summary>
    [RelayCommand]
    private void Select() => Host.Close(Selected);

    [RelayCommand]
    private void Cancel() => Host.Close(null);
}

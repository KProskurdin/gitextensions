using Avalonia.Controls;
using GitExtensions.Xplat.Core.Presentation;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Picks one of several values, e.g. which of a commit's branches a script means, bound to <see cref="ChooseViewModel"/>.
///  Closes with the value, or with null when cancelled.
/// </summary>
public partial class ChooseWindow : Window
{
    /// <summary>
    ///  The design-time constructor (<see cref="DesignTime"/>): the window's XAML only.
    /// </summary>
    [Obsolete(DesignTime.ConstructorOnly)]
#pragma warning disable CS8618 // The fields the window's data fills stay unset: no code of the window runs.
    public ChooseWindow() => InitializeComponent();
#pragma warning restore CS8618

    public ChooseWindow(IReadOnlyList<string> options)
    {
        ViewModel = new ChooseViewModel(new WindowDialogHost(this), options);
        DataContext = ViewModel;
        InitializeComponent();

        // View concerns: a double click chooses, and the list has the focus.
        OptionList.DoubleTapped += (_, _) => ViewModel.SelectCommand.Execute(null);
        Opened += (_, _) => OptionList.Focus();
    }

    public ChooseViewModel ViewModel { get; }
}

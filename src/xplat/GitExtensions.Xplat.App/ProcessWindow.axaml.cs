using System.ComponentModel;
using Avalonia.Controls;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Settings;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Shows the output of a remote operation (fetch, pull, push, clone) while git runs: the new shell's version of upstream
///  <c>FormRemoteProcess</c>, bound to <see cref="ProcessViewModel"/>. Abort stops git. Closing the window does not stop
///  the operation.
/// </summary>
public partial class ProcessWindow : Window
{
    /// <summary>
    ///  The design-time constructor (<see cref="DesignTime"/>): the window's XAML only.
    /// </summary>
    [Obsolete(DesignTime.ConstructorOnly)]
#pragma warning disable CS8618 // The fields the window's data fills stay unset: no code of the window runs.
    public ProcessWindow() => InitializeComponent();
#pragma warning restore CS8618

    public ProcessWindow(RepositoryOperationsViewModel actions, IAppPreferences preferences)
    {
        ViewModel = new ProcessViewModel(new WindowDialogHost(this), actions, preferences);
        DataContext = ViewModel;
        InitializeComponent();

        // View concerns: the newest line in view, and the state in the theme's colors.
        ViewModel.PropertyChanged += OnViewModelChanged;
        Closed += (_, _) =>
        {
            ViewModel.PropertyChanged -= OnViewModelChanged;
            ViewModel.Detach();
        };
        ShowLastLine();
        ShowStateColor();
    }

    public ProcessViewModel ViewModel { get; }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ProcessViewModel.OutputLines):
                ShowLastLine();
                break;
            case nameof(ProcessViewModel.IsFailed):
                ShowStateColor();
                break;
        }
    }

    private void ShowLastLine()
    {
        if (ViewModel.OutputLines.Count > 0)
        {
            OutputList.ScrollIntoView(ViewModel.OutputLines.Count - 1);
        }
    }

    private void ShowStateColor()
        => StateText.Foreground = ViewModel.IsFailed ? ThemeBrushes.Current.Removed : ThemeBrushes.Current.Context;
}

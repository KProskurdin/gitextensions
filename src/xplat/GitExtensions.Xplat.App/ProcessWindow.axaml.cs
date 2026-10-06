using System.ComponentModel;
using Avalonia.Controls;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Settings;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Shows the output of a remote operation (fetch, pull, push, clone) while git runs: the new shell's version of upstream
///  <c>FormRemoteProcess</c>. Abort stops git. Closing the window does not stop the operation.
/// </summary>
public partial class ProcessWindow : Window
{
    private readonly RepositoryOperationsViewModel _actions;
    private readonly IAppPreferences _preferences;

    public ProcessWindow(RepositoryOperationsViewModel actions, IAppPreferences preferences)
    {
        _actions = actions;
        _preferences = preferences;
        InitializeComponent();
        AbortButton.Click += (_, _) => _actions.Cancel();
        CloseButton.Click += (_, _) => Close();
        _actions.PropertyChanged += OnActionsChanged;
        Closed += (_, _) => _actions.PropertyChanged -= OnActionsChanged;
        ShowOutput();
        ShowState();
    }

    private void OnActionsChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(RepositoryOperationsViewModel.OutputLines):
                ShowOutput();
                break;
            case nameof(RepositoryOperationsViewModel.RemoteState):
            case nameof(RepositoryOperationsViewModel.ErrorMessage):
                ShowState();
                break;
        }
    }

    private void ShowOutput()
    {
        OutputList.ItemsSource = _actions.OutputLines;
        if (_actions.OutputLines.Count > 0)
        {
            OutputList.ScrollIntoView(_actions.OutputLines.Count - 1);
        }
    }

    private void ShowState()
    {
        Title = _actions.OutputTitle;
        RemoteOperationState state = _actions.RemoteState;
        StateText.Text = state switch
        {
            RemoteOperationState.Running => "Running...",
            RemoteOperationState.Succeeded => "Done",
            RemoteOperationState.Cancelled => "Aborted",
            RemoteOperationState.Failed => "Failed" + (_actions.ErrorMessage is { } error ? $": {error}" : ""),
            _ => "",
        };
        StateText.Foreground = state == RemoteOperationState.Failed ? ThemeBrushes.Current.Removed : ThemeBrushes.Current.Context;
        AbortButton.IsEnabled = _actions.CanCancel;
        CloseButton.IsEnabled = state != RemoteOperationState.Running;

        if (state == RemoteOperationState.Succeeded && _preferences.CloseProcessDialog)
        {
            Close();
        }
    }
}

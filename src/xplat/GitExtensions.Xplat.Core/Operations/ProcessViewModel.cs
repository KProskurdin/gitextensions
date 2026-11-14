using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitExtensions.Xplat.Core.Presentation;
using GitExtensions.Xplat.Core.Settings;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  The view model of upstream's <c>FormRemoteProcess</c>: the output of the operation <see cref="RepositoryOperationsViewModel"/>
///  runs, its state, "Keep dialog open", Abort while it runs and Close when it is done. Closes its window after a success
///  unless the dialog is kept open. Call <see cref="Detach"/> when the window closes.
/// </summary>
public sealed partial class ProcessViewModel : DialogViewModel
{
    private readonly RepositoryOperationsViewModel _actions;
    private readonly IAppPreferences _preferences;
    private bool _keepDialogOpen;

    /// <param name="actions">The operations whose output and state are shown.</param>
    /// <param name="preferences">Where upstream's <c>CloseProcessDialog</c> is kept.</param>
    public ProcessViewModel(IDialogHost host, RepositoryOperationsViewModel actions, IAppPreferences preferences)
        : base(host)
    {
        _actions = actions;
        _preferences = preferences;
        _keepDialogOpen = !preferences.CloseProcessDialog;
        _actions.PropertyChanged += OnActionsChanged;
    }

    /// <summary>
    ///  What the operation does, e.g. "Push to origin": the window's title.
    /// </summary>
    public string Title => _actions.OutputTitle;

    public IReadOnlyList<string> OutputLines => _actions.OutputLines;

    public string StateText => _actions.RemoteState switch
    {
        RemoteOperationState.Running => "Running...",
        RemoteOperationState.Succeeded => "Done",
        RemoteOperationState.Cancelled => "Aborted",
        RemoteOperationState.Failed => "Failed" + (_actions.ErrorMessage is { } error ? $": {error}" : ""),
        _ => "",
    };

    public bool IsFailed => _actions.RemoteState == RemoteOperationState.Failed;

    /// <summary>
    ///  Upstream hides "Keep dialog open" for a dialog that ignores the setting (<c>useDialogSettings: false</c>).
    /// </summary>
    public bool ShowsKeepDialogOpen => !_actions.KeepOutputOpen;

    /// <summary>
    ///  Upstream's "Keep dialog open" (the inverse of <c>CloseProcessDialog</c>). Turned off after the operation has
    ///  succeeded, the window closes as it would have (upstream's <c>KeepDialogOpen_CheckedChanged</c>).
    /// </summary>
    public bool KeepDialogOpen
    {
        get => _keepDialogOpen;
        set
        {
            if (!SetProperty(ref _keepDialogOpen, value))
            {
                return;
            }

            _preferences.CloseProcessDialog = !value;
            _preferences.Save();
            if (!value && _actions.RemoteState == RemoteOperationState.Succeeded)
            {
                Host.Close(null);
            }
        }
    }

    /// <summary>
    ///  Stops following the operations, when the window closes; closing the window does not stop the operation.
    /// </summary>
    public void Detach() => _actions.PropertyChanged -= OnActionsChanged;

    private bool CanAbort => _actions.CanCancel;

    private bool CanClose => _actions.RemoteState != RemoteOperationState.Running;

    [RelayCommand(CanExecute = nameof(CanAbort))]
    private void Abort() => _actions.Cancel();

    [RelayCommand(CanExecute = nameof(CanClose))]
    private void Close() => Host.Close(null);

    private void OnActionsChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(RepositoryOperationsViewModel.OutputLines):
                OnPropertyChanged(nameof(OutputLines));
                break;
            case nameof(RepositoryOperationsViewModel.OutputTitle):
                OnPropertyChanged(nameof(Title));
                break;
            case nameof(RepositoryOperationsViewModel.KeepOutputOpen):
                OnPropertyChanged(nameof(ShowsKeepDialogOpen));
                break;
            case nameof(RepositoryOperationsViewModel.ErrorMessage):
                OnPropertyChanged(nameof(StateText));
                break;
            case nameof(RepositoryOperationsViewModel.RemoteState):
                OnPropertyChanged(nameof(StateText));
                OnPropertyChanged(nameof(IsFailed));
                AbortCommand.NotifyCanExecuteChanged();
                CloseCommand.NotifyCanExecuteChanged();
                if (_actions.RemoteState == RemoteOperationState.Succeeded && _preferences.CloseProcessDialog
                                                                           && !_actions.KeepOutputOpen)
                {
                    Host.Close(null);
                }

                break;
        }
    }
}

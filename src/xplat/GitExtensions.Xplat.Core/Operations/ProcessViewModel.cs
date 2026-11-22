using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitExtensions.Xplat.Core.Presentation;
using GitExtensions.Xplat.Core.Settings;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  The view model of upstream's <c>FormRemoteProcess</c>: the output of the operation <see cref="RepositoryOperationsViewModel"/>
///  runs, its state, "Keep dialog open", "Show password input" with its box for git's input while it runs, Abort while
///  it runs and Close when it is done. Closes its window after a success unless the dialog is kept open. Call
///  <see cref="Detach"/> when the window closes.
/// </summary>
public sealed partial class ProcessViewModel : DialogViewModel
{
    private readonly RepositoryOperationsViewModel _actions;
    private readonly IAppPreferences _preferences;
    private bool _keepDialogOpen;
    private bool? _showPasswordInput;
    private string _password = "";
    private bool _isPasswordRevealed;

    /// <param name="actions">The operations whose output and state are shown.</param>
    /// <param name="preferences">Where upstream's <c>CloseProcessDialog</c> is kept.</param>
    public ProcessViewModel(IDialogHost host, RepositoryOperationsViewModel actions, IAppPreferences preferences)
        : base(host)
    {
        _actions = actions;
        _preferences = preferences;
        _keepDialogOpen = !preferences.CloseProcessDialog;
        _showPasswordInput = preferences.ShowProcessDialogPasswordInput;
        _actions.PropertyChanged += OnActionsChanged;
        _actions.InputRequested += OnInputRequested;
    }

    /// <summary>
    ///  Raised when git asks for input and the box shows for it: the view puts the focus in the box (upstream's
    ///  <c>PasswordInput.Focus</c>).
    /// </summary>
    public event EventHandler? PasswordInputFocusRequested;

    /// <summary>
    ///  Upstream's "Show password input": checked keeps the box shown (and is stored in upstream's
    ///  <c>ShowProcessDialogPasswordInput</c>), unchecked hides it, and neither is the box shown because git asked for
    ///  input while it was hidden.
    /// </summary>
    public bool? ShowPasswordInput
    {
        get => _showPasswordInput;
        set
        {
            if (!SetProperty(ref _showPasswordInput, value))
            {
                return;
            }

            // Upstream's ShowPassword_CheckedChanged.
            _preferences.ShowProcessDialogPasswordInput = value == true;
            _preferences.Save();
            OnPropertyChanged(nameof(IsPasswordInputVisible));
        }
    }

    /// <summary>
    ///  "Show password input" shows while the operation runs, as upstream hides it when the operation is done.
    /// </summary>
    public bool IsRunning => _actions.RemoteState == RemoteOperationState.Running;

    /// <summary>
    ///  Upstream's <c>PasswordInput</c>: shown while the operation runs and "Show password input" is not unchecked.
    /// </summary>
    public bool IsPasswordInputVisible => IsRunning && ShowPasswordInput != false;

    /// <summary>
    ///  The text for git, hidden unless <see cref="IsPasswordRevealed"/>.
    /// </summary>
    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    /// <summary>
    ///  Upstream's eye button: the text is shown as typed.
    /// </summary>
    public bool IsPasswordRevealed
    {
        get => _isPasswordRevealed;
        private set => SetProperty(ref _isPasswordRevealed, value);
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
    public void Detach()
    {
        _actions.PropertyChanged -= OnActionsChanged;
        _actions.InputRequested -= OnInputRequested;
    }

    private bool CanAbort => _actions.CanCancel;

    private bool CanClose => _actions.RemoteState != RemoteOperationState.Running;

    [RelayCommand(CanExecute = nameof(CanAbort))]
    private void Abort() => _actions.Cancel();

    [RelayCommand(CanExecute = nameof(CanClose))]
    private void Close() => Host.Close(null);

    // Upstream's PasswordInput.SendInput_Click: the line goes to git and the box is emptied.
    [RelayCommand(CanExecute = nameof(IsRunning))]
    private void SendInput()
    {
        _actions.SendInput(Password);
        Password = "";
    }

    // Upstream's PasswordInput.ShowPassword_Click.
    [RelayCommand]
    private void RevealPassword() => IsPasswordRevealed = !IsPasswordRevealed;

    // Upstream's FormProcess.DataReceived: text without a line end shows the hidden box and puts the focus in it.
    private void OnInputRequested(object? sender, EventArgs e)
    {
        if (ShowPasswordInput == false)
        {
            ShowPasswordInput = null;
        }

        if (IsPasswordInputVisible)
        {
            PasswordInputFocusRequested?.Invoke(this, EventArgs.Empty);
        }
    }

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
                OnPropertyChanged(nameof(IsRunning));
                OnPropertyChanged(nameof(IsPasswordInputVisible));
                AbortCommand.NotifyCanExecuteChanged();
                CloseCommand.NotifyCanExecuteChanged();
                SendInputCommand.NotifyCanExecuteChanged();
                if (_actions.RemoteState == RemoteOperationState.Succeeded && _preferences.CloseProcessDialog
                                                                           && !_actions.KeepOutputOpen)
                {
                    Host.Close(null);
                }

                break;
        }
    }
}

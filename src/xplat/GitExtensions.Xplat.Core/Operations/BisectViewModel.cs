using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitCommands.Git;
using GitExtensions.Xplat.Core.Presentation;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  A step the bisect window asks for: mark the checked-out commit, or end the bisect.
/// </summary>
public enum BisectStep
{
    Good,
    Bad,
    Skip,
    Stop,
}

/// <summary>
///  The view model of upstream's <c>FormBisect</c>. Start runs while the window stays open, so the first mark can follow at
///  once, as upstream; the other commands close the window with their <see cref="BisectStep"/>, which the caller runs.
/// </summary>
public sealed partial class BisectViewModel : DialogViewModel
{
    private readonly Func<Task<bool>> _start;

    /// <param name="isBisecting">Whether a bisect is in progress.</param>
    /// <param name="start">Starts the bisect; true when it started.</param>
    public BisectViewModel(IDialogHost host, bool isBisecting, Func<Task<bool>> start)
        : base(host)
    {
        _start = start;
        IsBisecting = isBisecting;
    }

    /// <summary>
    ///  Upstream's <c>UpdateButtonsState</c>: Start while no bisect is in progress, the marks and Stop while one is.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    [NotifyCanExecuteChangedFor(nameof(MarkCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    public partial bool IsBisecting { get; private set; }

    /// <summary>
    ///  The git option for a marking step; null for <see cref="BisectStep.Stop"/>.
    /// </summary>
    public static GitBisectOption? OptionFor(BisectStep step) => step switch
    {
        BisectStep.Good => GitBisectOption.Good,
        BisectStep.Bad => GitBisectOption.Bad,
        BisectStep.Skip => GitBisectOption.Skip,
        _ => null,
    };

    private bool CanStart => !IsBisecting;

    // The command is disabled while it runs (the toolkit's default), as upstream disabled the button.
    [RelayCommand(CanExecute = nameof(CanStart))]
    private Task StartAsync() => GuardAsync(async () => IsBisecting = await _start());

    [RelayCommand(CanExecute = nameof(IsBisecting))]
    private void Mark(BisectStep step) => Host.Close(step);

    [RelayCommand(CanExecute = nameof(IsBisecting))]
    private void Stop() => Host.Close(BisectStep.Stop);
}

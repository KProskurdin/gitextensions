using Avalonia.Controls;
using GitCommands.Git;

namespace GitExtensions.Xplat.App;

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
///  The new shell's version of upstream <c>FormBisect</c>. Start runs while the window stays open, so the first mark can
///  follow at once, as upstream; the other buttons close the window with their step, which the browse window runs. Not
///  ported: starting with a range from two selected commits, because the grid selects one commit.
/// </summary>
public partial class BisectWindow : Window
{
    private readonly Func<Task<bool>> _start;
    private bool _isBisecting;

    public BisectWindow(bool isBisecting, Func<Task<bool>> start)
    {
        _isBisecting = isBisecting;
        _start = start;
        InitializeComponent();
        StartButton.Click += (_, _) => UiActions.Run(StartAsync, _ => UpdateButtons());
        BadButton.Click += (_, _) => Close(BisectStep.Bad);
        GoodButton.Click += (_, _) => Close(BisectStep.Good);
        SkipButton.Click += (_, _) => Close(BisectStep.Skip);
        StopButton.Click += (_, _) => Close(BisectStep.Stop);
        UpdateButtons();
    }

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

    private async Task StartAsync()
    {
        StartButton.IsEnabled = false;
        _isBisecting = await _start();
        UpdateButtons();
    }

    // Upstream's UpdateButtonsState.
    private void UpdateButtons()
    {
        StartButton.IsEnabled = !_isBisecting;
        GoodButton.IsEnabled = _isBisecting;
        BadButton.IsEnabled = _isBisecting;
        StopButton.IsEnabled = _isBisecting;
        SkipButton.IsEnabled = _isBisecting;
    }
}

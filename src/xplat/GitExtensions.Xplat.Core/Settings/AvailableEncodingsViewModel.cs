using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitExtensions.Xplat.Core.Presentation;

namespace GitExtensions.Xplat.Core.Settings;

/// <summary>
///  The view model of upstream's <c>FormAvailableEncodings</c>: the encodings offered, the others, a command each way, and
///  OK, which stores the offered ones (rules in <see cref="AvailableEncodingsDialog"/>). Closes its window with true when
///  they were stored.
/// </summary>
public sealed partial class AvailableEncodingsViewModel : DialogViewModel
{
    private readonly IDictionary<string, Encoding> _settings;
    private readonly Action _save;

    /// <param name="settings">The offered encodings by web name (upstream's <c>AppSettings.AvailableEncodings</c>).</param>
    /// <param name="save">Saves the settings once OK changed them.</param>
    public AvailableEncodingsViewModel(IDialogHost host, IDictionary<string, Encoding> settings, Action save)
        : base(host)
    {
        _settings = settings;
        _save = save;
        Included = new ObservableCollection<Encoding>(settings.Values);
        Available = new ObservableCollection<Encoding>(AvailableEncodingsDialog.Selectable(settings.AsReadOnly()));
    }

    /// <summary>
    ///  Upstream's "Selected" list: the encodings offered.
    /// </summary>
    public ObservableCollection<Encoding> Included { get; }

    /// <summary>
    ///  Upstream's "Available" list: the encodings that can be added.
    /// </summary>
    public ObservableCollection<Encoding> Available { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ToRightCommand))]
    public partial Encoding? SelectedIncluded { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ToLeftCommand))]
    public partial Encoding? SelectedAvailable { get; set; }

    private bool CanMoveToLeft => SelectedAvailable is not null;

    // Upstream's ListIncludedEncodings_SelectedValueChanged: the default encodings stay.
    private bool CanMoveToRight => AvailableEncodingsDialog.CanRemove(SelectedIncluded);

    [RelayCommand(CanExecute = nameof(CanMoveToLeft))]
    private void ToLeft() => Move(SelectedAvailable, Available, Included);

    [RelayCommand(CanExecute = nameof(CanMoveToRight))]
    private void ToRight() => Move(SelectedIncluded, Included, Available);

    // Upstream's settings window saves this with the rest; here OK saves it.
    [RelayCommand]
    private void Ok()
    {
        AvailableEncodingsDialog.Apply(_settings, Included);
        _save();
        Host.Close(true);
    }

    [RelayCommand]
    private void Cancel() => Host.Close(false);

    private static void Move(Encoding? encoding, ObservableCollection<Encoding> from, ObservableCollection<Encoding> to)
    {
        if (encoding is not null)
        {
            from.Remove(encoding);
            to.Add(encoding);
        }
    }
}

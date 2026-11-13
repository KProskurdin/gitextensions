using System.Collections.ObjectModel;
using System.Text;
using Avalonia.Controls;
using Avalonia.Data;
using GitCommands;
using GitExtensions.Xplat.Core.Settings;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormAvailableEncodings</c>: which encodings the app offers. It closes with true when they were changed.
/// </summary>
public partial class AvailableEncodingsWindow : Window
{
    private readonly ObservableCollection<Encoding> _included = new(AppSettings.AvailableEncodings.Values);
    private readonly ObservableCollection<Encoding> _available = new(AvailableEncodingsDialog.Selectable(AppSettings.AvailableEncodings));

    public AvailableEncodingsWindow()
    {
        InitializeComponent();
        ListIncludedEncodings.DisplayMemberBinding = CompiledBinding.Create<Encoding, string>(encoding => encoding.EncodingName);
        ListAvailableEncodings.DisplayMemberBinding = CompiledBinding.Create<Encoding, string>(encoding => encoding.EncodingName);
        ListIncludedEncodings.ItemsSource = _included;
        ListAvailableEncodings.ItemsSource = _available;
        ListIncludedEncodings.SelectionChanged += (_, _) =>
            ToRight.IsEnabled = AvailableEncodingsDialog.CanRemove(ListIncludedEncodings.SelectedItem as Encoding);
        ListAvailableEncodings.SelectionChanged += (_, _) => ToLeft.IsEnabled = ListAvailableEncodings.SelectedItem is not null;
        ToLeft.Click += (_, _) => Move(ListAvailableEncodings, _available, _included);
        ToRight.Click += (_, _) => Move(ListIncludedEncodings, _included, _available);
        ButtonOk.Click += (_, _) =>
        {
            AvailableEncodingsDialog.Apply(AppSettings.AvailableEncodings, _included);

            // Upstream's settings window saves this with the rest; only SaveSettings writes the list.
            AppSettings.SaveSettings();
            Close(true);
        };
        ButtonCancel.Click += (_, _) => Close(false);
    }

    private static void Move(ListBox list, ObservableCollection<Encoding> from, ObservableCollection<Encoding> to)
    {
        if (list.SelectedItem is Encoding encoding)
        {
            from.Remove(encoding);
            to.Add(encoding);
        }
    }
}

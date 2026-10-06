using Avalonia.Controls;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Picks one of several values, e.g. which of a commit's branches a script means. Closes with the value, or with null when
///  cancelled.
/// </summary>
public partial class ChooseWindow : Window
{
    public ChooseWindow(IReadOnlyList<string> options)
    {
        InitializeComponent();
        OptionList.ItemsSource = options;
        OptionList.SelectedIndex = 0;
        OptionList.DoubleTapped += (_, _) => Close(OptionList.SelectedItem as string);
        OkButton.Click += (_, _) => Close(OptionList.SelectedItem as string);
        CancelButton.Click += (_, _) => Close(null);
        Opened += (_, _) => OptionList.Focus();
    }
}

using Avalonia.Controls;
using GitExtensions.Xplat.Core.Repository;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Lists HEAD's reflog. Closes with the hash of the entry to reset to, or null when closed without a choice.
/// </summary>
public partial class ReflogWindow : Window
{
    private const int EntryLimit = 200;

    private readonly string _repositoryPath;

    public ReflogWindow(string repositoryPath)
    {
        _repositoryPath = repositoryPath;
        InitializeComponent();
        CloseButton.Click += (_, _) => Close(null);
        ResetButton.Click += (_, _) => Close(EntryList.SelectedItem is ReflogEntry entry ? entry.Hash : null);
        EntryList.SelectionChanged += (_, _) => ResetButton.IsEnabled = EntryList.SelectedItem is not null;
        Opened += (_, _) => UiActions.Run(LoadAsync, ex => ErrorText.Text = ex.Message);
    }

    private async Task LoadAsync()
    {
        EntryList.ItemsSource = await Reflog.LoadAsync(_repositoryPath, EntryLimit);
    }
}

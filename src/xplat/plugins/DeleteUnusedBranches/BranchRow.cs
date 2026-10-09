using System.ComponentModel;

namespace GitExtensions.Plugins.DeleteUnusedBranches;

/// <summary>
///  A row of the branch list: upstream's <see cref="Branch"/>, with a change notice for its check box.
/// </summary>
public sealed class BranchRow(Branch branch) : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public Branch Branch => branch;

    public string Name => branch.Name;

    public string Date => branch.Date.ToString("g");

    public string Author => branch.Author;

    public string Message => branch.Message;

    public bool Delete
    {
        get => branch.Delete;
        set
        {
            if (branch.Delete != value)
            {
                branch.Delete = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Delete)));
            }
        }
    }
}

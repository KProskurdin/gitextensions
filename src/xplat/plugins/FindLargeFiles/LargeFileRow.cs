using System.ComponentModel;

namespace GitExtensions.Plugins.FindLargeFiles;

/// <summary>
///  A row of the large-file list: upstream's <see cref="GitObject"/>, with change notices so the list follows the scan, as
///  upstream's binding list does with <c>ResetItem</c>.
/// </summary>
public sealed class LargeFileRow(GitObject file) : INotifyPropertyChanged
{
    private bool _isEditable;

    public event PropertyChangedEventHandler? PropertyChanged;

    public GitObject File => file;

    public string Sha => file.SHA;

    public string Path => file.Path;

    public string Size => file.Size;

    public string CompressedSize => file.CompressedSize;

    public int CommitCount => file.CommitCount;

    public string LastCommitDate => file.LastCommitDate.ToString("g");

    public bool Delete
    {
        get => file.Delete;
        set
        {
            file.Delete = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Delete)));
        }
    }

    /// <summary>
    ///  False while the scan runs: upstream's grid is read-only until it ends.
    /// </summary>
    public bool IsEditable
    {
        get => _isEditable;
        set
        {
            _isEditable = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEditable)));
        }
    }

    /// <summary>
    ///  Tells the list that the scan changed the file's sizes, commits or date.
    /// </summary>
    public void Refresh() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
}

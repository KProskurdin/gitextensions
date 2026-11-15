using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitUI.ScriptsEngine;

namespace GitExtensions.Xplat.Core.Scripts;

/// <summary>
///  The view model of upstream's scripts settings page: the scripts in the order they run, the selected one (whose values
///  the page edits), and Add, Delete, Up and Down. The scripts are stored with the settings, only when they changed.
/// </summary>
public sealed partial class ScriptsPageViewModel : ObservableObject
{
    private IReadOnlyList<ScriptDefinition> _loaded;

    public ScriptsPageViewModel(IReadOnlyList<ScriptDefinition> scripts)
    {
        _loaded = scripts;
        Editor = new ScriptListEditor(scripts);
        Selected = Editor.Items.FirstOrDefault();
    }

    /// <summary>
    ///  The script list being edited.
    /// </summary>
    public ScriptListEditor Editor { get; }

    public ReadOnlyObservableCollection<ScriptListItem> Items => Editor.Items;

    public IReadOnlyList<ScriptEvent> Events { get; } = Enum.GetValues<ScriptEvent>();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand), nameof(MoveUpCommand), nameof(MoveDownCommand))]
    public partial ScriptListItem? Selected { get; set; }

    public bool HasSelection => Selected is not null;

    /// <summary>
    ///  Raised after Add selected the new script, so the window puts the caret in its name.
    /// </summary>
    public event EventHandler? ScriptAdded;

    /// <summary>
    ///  Upstream stores the page's scripts on every save; this one only when they changed, to keep the file untouched.
    ///  True when <paramref name="save"/> stored them.
    /// </summary>
    public bool StoreIfChanged(Action<IReadOnlyList<ScriptDefinition>> save)
    {
        IReadOnlyList<ScriptDefinition> scripts = Editor.Scripts;
        if (ScriptsXml.Write(scripts) == ScriptsXml.Write(_loaded))
        {
            return false;
        }

        save(scripts);
        _loaded = scripts;
        return true;
    }

    [RelayCommand]
    private void Add()
    {
        Selected = Editor.Add();
        ScriptAdded?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Delete()
    {
        if (Selected is not { } item)
        {
            return;
        }

        int index = Items.IndexOf(item);
        Editor.Remove(item);
        Selected = Items.Count > 0 ? Items[Math.Min(index, Items.Count - 1)] : null;
    }

    private bool CanMoveUp => Selected is { } item && Editor.CanMoveUp(item);

    private bool CanMoveDown => Selected is { } item && Editor.CanMoveDown(item);

    [RelayCommand(CanExecute = nameof(CanMoveUp))]
    private void MoveUp() => Move(Editor.MoveUp);

    [RelayCommand(CanExecute = nameof(CanMoveDown))]
    private void MoveDown() => Move(Editor.MoveDown);

    // Moving an item in the list can drop the list's selection, which is put back.
    private void Move(Action<ScriptListItem> move)
    {
        if (Selected is not { } item)
        {
            return;
        }

        move(item);
        Selected = item;
        MoveUpCommand.NotifyCanExecuteChanged();
        MoveDownCommand.NotifyCanExecuteChanged();
    }
}

using Avalonia.Controls;
using Avalonia.Threading;
using GitCommands;

namespace GitExtensions.Xplat.App;

// Upstream FormBrowse's focus hotkeys over its tabs (FocusCommitInfo, FocusTabOf, FocusNextTab). The focus moves once the
// newly shown tab is laid out.
public partial class MainWindow
{
    // Upstream's FocusCommitInfo: the commit tab shows first while the commit info is below the grid.
    private void FocusCommitInfo()
    {
        if (_preferences.CommitInfoPosition == CommitInfoPosition.BelowList)
        {
            DetailTabs.SelectedItem = CommitInfoTab;
        }

        Dispatcher.UIThread.Post(() => RevisionInfo.Focus(), DispatcherPriority.Loaded);
    }

    // Upstream's FocusTabOf with RevisionDiffControl.SwitchFocus: the tab shows with its file list focused, or, when the
    // list had the focus already, the diff (the blame while it shows).
    private void FocusFilesTab(TabItem tab, Control files, DiffView diff, Control blame)
    {
        bool filesFocused = files.IsKeyboardFocusWithin;
        DetailTabs.SelectedItem = tab;
        Dispatcher.UIThread.Post(() =>
        {
            if (!filesFocused)
            {
                FocusList(files);
            }
            else if (blame.IsVisible)
            {
                blame.Focus();
            }
            else
            {
                diff.FocusText();
            }
        }, DispatcherPriority.Loaded);
    }

    // Upstream's FocusTabOf for a tab that shows only when its setting asks for it (GPG, the build report).
    private void FocusTab(TabItem tab, Control control)
    {
        if (!tab.IsVisible)
        {
            return;
        }

        DetailTabs.SelectedItem = tab;
        Dispatcher.UIThread.Post(() => control.Focus(), DispatcherPriority.Loaded);
    }

    // A list whose rows take the focus (not the list itself): its selected row, else its first.
    private static void FocusList(Control control)
    {
        if (control is ListBox { Focusable: false } list)
        {
            list.ContainerFromIndex(Math.Max(0, list.SelectedIndex))?.Focus();
        }
        else
        {
            control.Focus();
        }
    }

    // Upstream's FocusNextTab: the next or previous of the tabs shown, round.
    private void FocusNextTab(bool forward)
    {
        List<TabItem> tabs = [.. DetailTabs.Items.OfType<TabItem>().Where(tab => tab.IsVisible)];
        if (tabs.Count == 0)
        {
            return;
        }

        int index = tabs.IndexOf(DetailTabs.SelectedItem as TabItem ?? tabs[0]);
        DetailTabs.SelectedItem = tabs[(index + (forward ? 1 : tabs.Count - 1)) % tabs.Count];
    }
}

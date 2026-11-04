using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace GitExtensions.Xplat.App;

public partial class MainWindow
{
    // Upstream's OnGridViewMouseDown and the commit info's _RevisionHeader_MouseDown / rtbRevisionHeader_MouseDown: the
    // mouse's back and forward buttons navigate the grid's history, over the grid and over the commit's details.
    private void WireMouseNavigation()
    {
        CommitList.AddHandler(PointerPressedEvent, OnNavigationButtonPressed, RoutingStrategies.Tunnel);
        RevisionInfo.AddHandler(PointerPressedEvent, OnNavigationButtonPressed, RoutingStrategies.Tunnel);
    }

    private void OnNavigationButtonPressed(object? sender, PointerPressedEventArgs e)
    {
        PointerPointProperties pressed = e.GetCurrentPoint(sender as Visual).Properties;
        GridCommand? command = pressed.IsXButton1Pressed ? GridCommand.NavigateBackward
            : pressed.IsXButton2Pressed ? GridCommand.NavigateForward
            : null;
        if (command is { } navigate)
        {
            e.Handled = true;
            RunGridHotkey(navigate);
        }
    }
}

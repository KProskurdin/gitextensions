using Avalonia.Controls;
using GitExtensions.Xplat.Core.Settings;

namespace GitExtensions.Xplat.App;

/// <summary>
///  A three-state check box that steps through its states in the order of upstream's shell extension menu list
///  (<see cref="ShellMenuEntry.Next"/>) instead of Avalonia's.
/// </summary>
public sealed class ShellMenuCheckBox : CheckBox
{
    protected override Type StyleKeyOverride => typeof(CheckBox);

    protected override void Toggle() => SetCurrentValue(IsCheckedProperty, ShellMenuEntry.Next(IsChecked));
}

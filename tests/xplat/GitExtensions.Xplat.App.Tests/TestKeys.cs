using Avalonia.Input;

namespace GitExtensions.Xplat.App.Tests;

/// <summary>
///  The raw form of <see cref="Hotkeys.CommandModifier"/>, for tests that press keys on the window.
/// </summary>
internal static class TestKeys
{
    public static RawInputModifiers Command => OperatingSystem.IsMacOS() ? RawInputModifiers.Meta : RawInputModifiers.Control;
}

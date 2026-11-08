using Avalonia.Input;

namespace GitExtensions.Xplat.App.Tests;

/// <summary>
///  The raw form of <see cref="Hotkeys.CommandModifier"/>, for tests that press keys on the window.
/// </summary>
internal static class TestKeys
{
    public static RawInputModifiers Command => OperatingSystem.IsMacOS() ? RawInputModifiers.Meta : RawInputModifiers.Control;

    /// <summary>
    ///  The raw form of a gesture's modifiers, for pressing a key from a hotkey table.
    /// </summary>
    public static RawInputModifiers Raw(KeyModifiers modifiers)
    {
        RawInputModifiers raw = RawInputModifiers.None;
        raw |= modifiers.HasFlag(KeyModifiers.Control) ? RawInputModifiers.Control : RawInputModifiers.None;
        raw |= modifiers.HasFlag(KeyModifiers.Meta) ? RawInputModifiers.Meta : RawInputModifiers.None;
        raw |= modifiers.HasFlag(KeyModifiers.Alt) ? RawInputModifiers.Alt : RawInputModifiers.None;
        raw |= modifiers.HasFlag(KeyModifiers.Shift) ? RawInputModifiers.Shift : RawInputModifiers.None;
        return raw;
    }
}

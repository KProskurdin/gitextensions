using CommonTestUtils;

[assembly: GitExtUtilsTests.XplatSkipOnNonWindows]

namespace GitExtUtilsTests;

/// <summary>
///  Upstream tests of this assembly that are ignored off Windows (see <see cref="XplatSkipOnNonWindowsAttributeBase"/>).
/// </summary>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class XplatSkipOnNonWindowsAttribute : XplatSkipOnNonWindowsAttributeBase
{
    protected override IReadOnlyList<(string Test, string Reason)> Skips { get; } =
    [
        ("GitExtUtilsTests.AdaptLightnessTests.AdaptLightness_should_leave_the_original_untouched", WindowsGdi),
        ("GitExtUtilsTests.AdaptLightnessTests.AdaptLightness_should_transform_a_bitmap_backed_by_a_closed_stream",
            WindowsGdi),

        // DpiUtil reads the screen DPI through gdi32/user32 in its static constructor.
        ("GitExtUtilsTests.GitUI.DpiUtilTests", WindowsGdi),
    ];
}

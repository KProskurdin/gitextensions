using CommonTestUtils;

[assembly: ResourceManagerTests.XplatSkipOnNonWindows]

namespace ResourceManagerTests;

/// <summary>
///  Upstream tests of this assembly that are ignored off Windows (see <see cref="XplatSkipOnNonWindowsAttributeBase"/>).
/// </summary>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class XplatSkipOnNonWindowsAttribute : XplatSkipOnNonWindowsAttributeBase
{
    protected override IReadOnlyList<(string Test, string Reason)> Skips { get; } =
    [
        // The real header renderer measures its labels with DpiUtil, which reads the screen DPI through gdi32/user32.
        ("ResourceManagerTests.CommitDataRenders.CommitDataHeaderRendererIntegrationTests.Render_with_spaces_no_links", WindowsGdi),
        ("ResourceManagerTests.CommitDataRenders.CommitDataHeaderRendererIntegrationTests.Render_with_spaces_with_links", WindowsGdi),
        ("ResourceManagerTests.CommitDataRenders.CommitDataHeaderRendererIntegrationTests.Render_with_tabs_and_links", WindowsGdi),
        ("ResourceManagerTests.CommitDataRenders.CommitDataHeaderRendererIntegrationTests.Render_with_tabs_no_links", WindowsGdi),
        ("ResourceManagerTests.CommitDataRenders.CommitDataHeaderRendererIntegrationTests.RenderPlain_with_spaces", WindowsGdi),
        ("ResourceManagerTests.CommitDataRenders.CommitDataHeaderRendererIntegrationTests.RenderPlain_with_tabs", WindowsGdi),

        // Needs a System.Drawing.Graphics (XplatControlGraphics.cs).
        ("ResourceManagerTests.CommitDataRenders.CommitDataHeaderRendererTests.GetFont_should_get_font_from_style_provider", WindowsGdi),
    ];
}

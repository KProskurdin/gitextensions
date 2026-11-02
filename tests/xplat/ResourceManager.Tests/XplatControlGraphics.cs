namespace ResourceManagerTests;

/// <summary>
///  Lets <c>CommitDataHeaderRendererTests.GetFont_should_get_font_from_style_provider</c> compile: the shim's
///  <see cref="Control"/> has no window, so it cannot create a <see cref="Graphics"/>. The test only passes the
///  object on, so one drawing on a bitmap stands in. System.Drawing works on Windows only; elsewhere the test is
///  ignored (see <see cref="XplatSkipOnNonWindowsAttribute"/>).
/// </summary>
internal static class XplatControlGraphics
{
    public static Graphics CreateGraphics(this Control control)
    {
        Bitmap bitmap = new(1, 1);
        return Graphics.FromImage(bitmap);
    }
}

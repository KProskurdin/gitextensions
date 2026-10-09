using System.Text;
using Avalonia.Input;

namespace GitExtensions.Plugins.ReleaseNotesGenerator;

/// <summary>
///  The clipboard part of upstream's <c>HtmlFragment</c>: an HTML fragment as plain text (the HTML code) and as HTML, which
///  word processors paste as a table. On Windows the HTML is upstream's CF_HTML text with its offsets header; elsewhere the
///  platforms take the plain HTML document under their own format name.
/// </summary>
public static class HtmlClipboard
{
    private const string WindowsFormat = "HTML Format";
    private const string MacFormat = "public.html";
    private const string FreedesktopFormat = "text/html";

    public static DataTransfer Create(string htmlFragment)
    {
        DataTransferItem item = new();
        item.SetText(htmlFragment);
        if (OperatingSystem.IsWindows())
        {
            item.Set(DataFormat.CreateStringPlatformFormat(WindowsFormat), CreateCfHtml(htmlFragment));
        }
        else
        {
            string format = OperatingSystem.IsMacOS() ? MacFormat : FreedesktopFormat;
            item.Set(DataFormat.CreateStringPlatformFormat(format), $"<html><body>{htmlFragment}</body></html>");
        }

        DataTransfer data = new();
        data.Add(item);
        return data;
    }

    /// <summary>
    ///  Upstream's <c>CreateHtmlFormatClipboardDataObject</c>: the CF_HTML header with the offsets of the document and the
    ///  fragment, then the document.
    /// </summary>
    public static string CreateCfHtml(string htmlFragment)
    {
        // The placeholders are as long as the 8-digit offsets that replace them, and "<" cannot appear in escaped HTML.
        const string header = "Version:0.9\r\n" +
                              "StartHTML:<<<<<<<1\r\n" +
                              "EndHTML:<<<<<<<2\r\n" +
                              "StartFragment:<<<<<<<3\r\n" +
                              "EndFragment:<<<<<<<4\r\n";
        const string pre = "<html><body>\r\n<!--StartFragment-->";
        const string post = "<!--EndFragment-->\r\n</body></html>";

        StringBuilder sb = new(header);
        int startHtml = sb.Length;
        sb.Append(pre);
        int fragmentStart = sb.Length;
        sb.Append(htmlFragment);
        int fragmentEnd = sb.Length;
        sb.Append(post);
        int endHtml = sb.Length;

        sb.Replace("<<<<<<<1", To8DigitString(startHtml));
        sb.Replace("<<<<<<<2", To8DigitString(endHtml));
        sb.Replace("<<<<<<<3", To8DigitString(fragmentStart));
        sb.Replace("<<<<<<<4", To8DigitString(fragmentEnd));
        return sb.ToString();
    }

    private static string To8DigitString(int x) => $"{x:00000000}";
}

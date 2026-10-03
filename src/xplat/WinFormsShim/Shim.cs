#pragma warning disable SA1402, SA1649 // Many small stand-in types in one file on purpose.

namespace System.Windows.Forms;

public interface IWin32Window
{
    IntPtr Handle { get; }
}

public enum DialogResult
{
    None = 0,
    OK = 1,
    Cancel = 2,
    Abort = 3,
    Retry = 4,
    Ignore = 5,
    Yes = 6,
    No = 7,
    TryAgain = 10,
    Continue = 11,
}

public enum MessageBoxButtons
{
    OK = 0,
    OKCancel = 1,
    AbortRetryIgnore = 2,
    YesNoCancel = 3,
    YesNo = 4,
    RetryCancel = 5,
    CancelTryContinue = 6,
}

public enum MessageBoxIcon
{
    None = 0,
    Error = 16,
    Hand = 16,
    Stop = 16,
    Question = 32,
    Warning = 48,
    Exclamation = 48,
    Information = 64,
    Asterisk = 64,
}

/// <summary>
///  Lets the host application show its own folder picker for <see cref="FolderBrowserDialog"/> users in the shared layers.
/// </summary>
public class FolderBrowserDialog : System.ComponentModel.Component
{
    public static Func<IWin32Window?, string?, string?>? Handler { get; set; }

    public string SelectedPath { get; set; } = string.Empty;

    public DialogResult ShowDialog(IWin32Window? owner)
    {
        string? picked = Handler?.Invoke(owner, SelectedPath);
        if (picked is null)
        {
            return DialogResult.Cancel;
        }

        SelectedPath = picked;
        return DialogResult.OK;
    }
}

public static class TextRenderer
{
    private const float AverageGlyphWidthInEm = 0.55f;

    /// <summary>
    ///  Lets the host application measure text with its own font stack; the default is a rough estimate.
    /// </summary>
    public static Func<string, Font, Size>? Measurer { get; set; }

    public static Size MeasureText(string? text, Font? font)
    {
        if (text is null || font is null)
        {
            return Size.Empty;
        }

        return Measurer?.Invoke(text, font)
            ?? new Size((int)(text.Length * font.Size * AverageGlyphWidthInEm), (int)Math.Ceiling(font.Height * 1.0));
    }
}

public enum MessageBoxDefaultButton
{
    Button1 = 0,
    Button2 = 256,
    Button3 = 512,
}

public class Control : System.ComponentModel.Component, IWin32Window
{
    public IntPtr Handle => IntPtr.Zero;

    public string Text { get; set; } = string.Empty;

    public int Height { get; set; }

    public Color ForeColor { get; set; }

    public Color BackColor { get; set; }

    public bool IsDisposed { get; private set; }

    protected override void Dispose(bool disposing)
    {
        IsDisposed = true;
        base.Dispose(disposing);
    }
}

/// <summary>
///  Formats a key combination like WinForms does ("Ctrl+Shift+A"); the host can localize later.
/// </summary>
public class KeysConverter : System.ComponentModel.TypeConverter
{
    public override object? ConvertTo(System.ComponentModel.ITypeDescriptorContext? context, System.Globalization.CultureInfo? culture, object? value, Type destinationType)
    {
        if (destinationType != typeof(string) || value is not Keys keys)
        {
            return base.ConvertTo(context, culture, value, destinationType);
        }

        List<string> parts = [];
        if (keys.HasFlag(Keys.Control))
        {
            parts.Add("Ctrl");
        }

        if (keys.HasFlag(Keys.Shift))
        {
            parts.Add("Shift");
        }

        if (keys.HasFlag(Keys.Alt))
        {
            parts.Add("Alt");
        }

        parts.Add((keys & Keys.KeyCode).ToString());
        return string.Join('+', parts);
    }
}

public class ButtonBase : Control
{
    public Image? Image { get; set; }
}

public class ToolStripItem : System.ComponentModel.Component
{
    public Image? Image { get; set; }

    public bool IsDisposed { get; private set; }

    protected override void Dispose(bool disposing)
    {
        IsDisposed = true;
        base.Dispose(disposing);
    }
}

public class ToolStripMenuItem : ToolStripItem
{
}

public enum SystemColorMode
{
    System = 0,
    Classic = 1,
    Dark = 2,
}

public readonly struct Padding(int left, int top, int right, int bottom)
{
    public int Left { get; } = left;

    public int Top { get; } = top;

    public int Right { get; } = right;

    public int Bottom { get; } = bottom;
}

public enum BorderStyle
{
    None = 0,
    FixedSingle = 1,
    Fixed3D = 2,
}

public class UserControl : Control
{
}

public class Form : Control
{
    public static Form? ActiveForm => null;
}

public class TextBox : Control
{
    public bool ReadOnly { get; set; }

    public BorderStyle BorderStyle { get; set; }

    public bool Multiline { get; set; }
}

public class ListBox : Control
{
}

public class ToolTip
{
    private readonly Dictionary<Control, string?> _tips = [];

    public string ToolTipTitle { get; set; } = string.Empty;

    public string? GetToolTip(Control control) => _tips.GetValueOrDefault(control);

    public void SetToolTip(Control control, string? caption) => _tips[control] = caption;
}

public enum CheckState
{
    Unchecked = 0,
    Checked = 1,
    Indeterminate = 2,
}

public class CheckBox : Control
{
    public bool Checked { get; set; }

    public CheckState CheckState { get; set; }
}

/// <summary>
///  Lets the host application decide how message boxes raised by the shared layers are shown.
/// </summary>
public static class MessageBox
{
    public static Func<IWin32Window?, string, string, MessageBoxButtons, MessageBoxIcon, MessageBoxDefaultButton, DialogResult>? Handler { get; set; }

    public static DialogResult Show(IWin32Window? owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton)
        => Handler?.Invoke(owner, text, caption, buttons, icon, defaultButton) ?? DialogResult.None;
}

/// <summary>
///  Lets the host application put text on its platform's clipboard.
/// </summary>
public static class Clipboard
{
    public static Action<object>? Handler { get; set; }

    public static void SetDataObject(object data, bool copy, int retryTimes, int retryDelay)
        => Handler?.Invoke(data);
}

public static class Application
{
    public static string ExecutablePath => Environment.ProcessPath ?? string.Empty;

    public static string ProductName => "Git Extensions";

    public static string ProductVersion => System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "0.0.0.0";

    public static bool IsDarkModeEnabled => SystemColorMode == SystemColorMode.Dark;

    public static SystemColorMode SystemColorMode { get; set; } = SystemColorMode.System;

    /// <summary>
    ///  Like WinForms, creates the directory: upstream relies on it existing (e.g. before writing GitExtensions.settings).
    ///  ApplicationData is ~/.config on Linux, ~/Library/Application Support on macOS.
    /// </summary>
    public static string UserAppDataPath
    {
        get
        {
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GitExtensions");
            Directory.CreateDirectory(path);
            return path;
        }
    }

    /// <summary>
    ///  Raised instead of WinForms' thread exception dialog; the host application subscribes.
    /// </summary>
    public static event System.Threading.ThreadExceptionEventHandler? ThreadException;

    public static void OnThreadException(Exception t) => ThreadException?.Invoke(null, new System.Threading.ThreadExceptionEventArgs(t));

    /// <summary>
    ///  There is no message pump to run; kept so WinForms-era test helpers still compile.
    /// </summary>
    public static void DoEvents()
    {
    }
}

public class ComboBox : Control
{
}

public class Label : Control
{
}

public class TableLayoutPanel : Control
{
}

public class ContextMenuStrip : Control
{
}

public class DataGridViewColumn
{
    public bool Visible { get; set; }
}

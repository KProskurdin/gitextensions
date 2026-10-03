namespace System.IO;

// Lives in WindowsBase (WPF) on Windows; upstream code and tests catch it for malformed legacy history files.
public class FileFormatException : FormatException
{
    public FileFormatException()
    {
    }

    public FileFormatException(string? message)
        : base(message)
    {
    }

    public FileFormatException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}

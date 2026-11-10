using System.Runtime.InteropServices;

namespace GitExtensions.Xplat.App;

/// <summary>
///  The Dock's icon of the running app on macOS (<c>NSApplication.applicationIconImage</c>). macOS gives a window no icon of
///  its own, so Avalonia's <c>Window.Icon</c> does nothing there, and Avalonia offers no call for the application's icon:
///  only a bundle's .icns gives one, and the app started outside a bundle (dotnet run, the executable) shows the generic icon.
/// </summary>
internal static class MacApplicationIcon
{
    /// <summary>
    ///  Shows <paramref name="png"/> as the application's icon. Call it on the main thread once Avalonia has set up the
    ///  application; false when macOS cannot read the picture.
    /// </summary>
    public static bool Set(byte[] png)
    {
        GCHandle pinned = GCHandle.Alloc(png, GCHandleType.Pinned);
        IntPtr data = IntPtr.Zero;
        IntPtr image = IntPtr.Zero;
        try
        {
            // alloc and init, not the class's convenience constructors: their objects are autoreleased, and no autorelease
            // pool may be in place before the run loop starts.
            data = NativeMethods.Send(NativeMethods.Send(Class("NSData"), Selector("alloc")), Selector("initWithBytes:length:"),
                pinned.AddrOfPinnedObject(), (nuint)png.Length);
            image = NativeMethods.Send(NativeMethods.Send(Class("NSImage"), Selector("alloc")), Selector("initWithData:"), data);
            if (image == IntPtr.Zero)
            {
                return false;
            }

            IntPtr application = NativeMethods.Send(Class("NSApplication"), Selector("sharedApplication"));
            NativeMethods.Send(application, Selector("setApplicationIconImage:"), image);
            return true;
        }
        finally
        {
            // The application keeps its own reference to the image.
            Release(image);
            Release(data);
            pinned.Free();
        }
    }

    private static IntPtr Class(string name) => NativeMethods.objc_getClass(name);

    private static IntPtr Selector(string name) => NativeMethods.sel_registerName(name);

    private static void Release(IntPtr instance)
    {
        if (instance != IntPtr.Zero)
        {
            NativeMethods.Send(instance, Selector("release"));
        }
    }

    private static class NativeMethods
    {
        private const string ObjectiveCLibrary = "/usr/lib/libobjc.A.dylib";

        [DllImport(ObjectiveCLibrary)]
        public static extern IntPtr objc_getClass([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

        [DllImport(ObjectiveCLibrary)]
        public static extern IntPtr sel_registerName([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

        // objc_msgSend takes the arguments of the method it calls, so each signature used gets its own declaration.
        [DllImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
        public static extern IntPtr Send(IntPtr receiver, IntPtr selector);

        [DllImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
        public static extern IntPtr Send(IntPtr receiver, IntPtr selector, IntPtr argument);

        [DllImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
        public static extern IntPtr Send(IntPtr receiver, IntPtr selector, IntPtr bytes, nuint length);
    }
}

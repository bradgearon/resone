using System.Runtime.InteropServices;

namespace Wds.Resone.Updater;

internal static class WindowsTrayNotice
{
    public static void Show(string title, string message)
    {
        if (!OperatingSystem.IsWindows()) { try { Console.Error.WriteLine(title + ": " + message); } catch { } return; }
        try
        {
            string cls = "Wds.Resone.UpdateNotice." + Environment.ProcessId;
            WndProc callback = Proc;
            var wc = new WindowClass { proc = callback, instance = GetModuleHandle(null), name = cls };
            RegisterClassW(ref wc);
            nint window = CreateWindowExW(0, cls, "Resone Update", 0, 0, 0, 0, 0, 0, 0, wc.instance, 0);
            if (window == 0) throw new InvalidOperationException("Unable to create notification window.");
            nint icon = LoadImageW(0, Path.Combine(AppContext.BaseDirectory, "Resone.ico"), 1, 32, 32, 0x10);
            var data = new Notify { size = (uint)Marshal.SizeOf<Notify>(), window = window, id = 1, flags = 1 | 2 | 4, message = 0x8001, icon = icon, tip = "Resone updater", info = "", title = "" };
            Shell_NotifyIconW(0, ref data);
            data.flags = 0x10;
            data.title = title.Length > 63 ? title[..63] : title;
            data.info = message.Length > 255 ? message[..255] : message;
            data.infoFlags = 3;
            Shell_NotifyIconW(1, ref data);
            Thread.Sleep(TimeSpan.FromSeconds(8));
            Shell_NotifyIconW(2, ref data);
            if (icon != 0) DestroyIcon(icon);
            DestroyWindow(window);
            UnregisterClassW(cls, wc.instance);
            GC.KeepAlive(callback);
        }
        catch
        {
            try { MessageBoxW(0, message, title, 0x10 | 0x10000); } catch { }
        }
    }

    private static nint Proc(nint w, uint m, nuint wp, nint lp) => DefWindowProcW(w, m, wp, lp);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate nint WndProc(nint w, uint m, nuint wp, nint lp);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct WindowClass { public uint style; public WndProc proc; public int clsExtra, winExtra; public nint instance, icon, cursor, background; public string? menu; public string name; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct Notify { public uint size; public nint window; public uint id, flags, message; public nint icon; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string tip; public uint state, stateMask; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string info; public uint timeout; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string title; public uint infoFlags; public Guid guid; public nint balloon; }
    [DllImport("kernel32", CharSet = CharSet.Unicode)] private static extern nint GetModuleHandle(string? name);
    [DllImport("user32", CharSet = CharSet.Unicode)] private static extern ushort RegisterClassW(ref WindowClass c);
    [DllImport("user32", CharSet = CharSet.Unicode)] private static extern bool UnregisterClassW(string name, nint instance);
    [DllImport("user32", CharSet = CharSet.Unicode)] private static extern nint CreateWindowExW(uint ex, string cls, string title, uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint param);
    [DllImport("user32")] private static extern nint DefWindowProcW(nint w, uint m, nuint wp, nint lp);
    [DllImport("user32")] private static extern bool DestroyWindow(nint w);
    [DllImport("user32", CharSet = CharSet.Unicode)] private static extern nint LoadImageW(nint instance, string path, uint type, int width, int height, uint flags);
    [DllImport("user32")] private static extern bool DestroyIcon(nint icon);
    [DllImport("shell32", CharSet = CharSet.Unicode)] private static extern bool Shell_NotifyIconW(uint action, ref Notify data);
    [DllImport("user32", CharSet = CharSet.Unicode)] private static extern int MessageBoxW(nint owner, string text, string caption, uint type);
}

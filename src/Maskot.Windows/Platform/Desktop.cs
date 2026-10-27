using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;

namespace Maskot.Windows.Platform;

internal static class Desktop
{
    [StructLayout(LayoutKind.Sequential)] internal struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] internal struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct MONITORINFO
    {
        public int Size; public RECT Monitor, Work; public int Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Device;
    }
    private delegate bool MonitorProc(IntPtr monitor, IntPtr dc, ref RECT rect, IntPtr data);
    private delegate bool WindowProc(IntPtr hwnd, IntPtr data);
    [DllImport("user32.dll")] private static extern bool EnumDisplayMonitors(IntPtr dc, IntPtr clip, MonitorProc proc, IntPtr data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);
    [DllImport("shcore.dll")] private static extern int GetDpiForMonitor(IntPtr monitor, int type, out uint x, out uint y);
    [DllImport("user32.dll")] private static extern bool EnumWindows(WindowProc proc, IntPtr data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hwnd, StringBuilder buffer, int count);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT point);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hwnd, int id);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr hwnd, uint command);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attr, out int value, int size);
    [DllImport("user32.dll")] internal static extern bool DestroyIcon(IntPtr icon);

    public static PointD Cursor { get { GetCursorPos(out var p); return new(p.X, p.Y); } }
    public static bool EscapeDown => GetAsyncKeyState(0x1B) < 0;
    public static bool LeftDown => GetAsyncKeyState(0x01) < 0;
    internal const int DragEscapeId = 0x57A0;
    public static void CaptureEscape(Window window) => RegisterHotKey(new WindowInteropHelper(window).Handle, DragEscapeId, 0x4000, 0x1B);
    public static void ReleaseEscape(Window window) => UnregisterHotKey(new WindowInteropHelper(window).Handle, DragEscapeId);

    public static IReadOnlyList<DisplayArea> Displays()
    {
        var bars = new List<RECT>();
        EnumWindows((h, _) =>
        {
            var name = new StringBuilder(128);
            GetClassName(h, name, name.Capacity);
            if (name.ToString() is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" && IsWindowVisible(h) && GetWindowRect(h, out var r))
            {
                if (DwmGetWindowAttribute(h, 14, out var cloaked, sizeof(int)) != 0 || cloaked == 0) bars.Add(r);
            }
            return true;
        }, IntPtr.Zero);
        var result = new List<DisplayArea>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr h, IntPtr dc, ref RECT r, IntPtr data) =>
        {
            var i = new MONITORINFO { Size = Marshal.SizeOf<MONITORINFO>(), Device = "" };
            if (!GetMonitorInfo(h, ref i)) return true;
            var scale = GetDpiForMonitor(h, 0, out var dpi, out _) == 0 ? dpi / 96.0 : 1;
            RectD ToRect(RECT a) => new(a.Left, a.Top, a.Right - a.Left, a.Bottom - a.Top);
            var bounds = ToRect(i.Monitor);
            var bottom = bounds.Bottom - 5 * scale; // Leave the auto-hide reveal edge unobstructed.
            foreach (var b in bars)
            {
                var visibleHeight = Math.Min(b.Bottom, bounds.Bottom) - Math.Max(b.Top, bounds.Y);
                var overlapWidth = Math.Min(b.Right, bounds.Right) - Math.Max(b.Left, bounds.X);
                if (overlapWidth > bounds.Width * 0.65 && visibleHeight > 5 * scale && b.Top > bounds.Center.Y)
                    bottom = Math.Min(bottom, b.Top - 3 * scale);
            }
            result.Add(new(i.Device, bounds, ToRect(i.Work), scale, bottom));
            return true;
        }, IntPtr.Zero);
        if (result.Count == 0) result.Add(new("primary", new(0, 0, 1920, 1080), new(0, 0, 1920, 1040), 1, 1035));
        return result;
    }

    public static bool FullscreenOn(DisplayArea display)
    {
        var h = GetForegroundWindow();
        if (h == IntPtr.Zero || !GetWindowRect(h, out var r)) return false;
        var cls = new StringBuilder(128); GetClassName(h, cls, cls.Capacity);
        if (cls.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd") return false;
        var b = display.Bounds;
        return r.Left <= b.X && r.Top <= b.Y && r.Right >= b.Right && r.Bottom >= b.Bottom;
    }

    public static void MakePassive(Window w, bool clickThrough = false)
    {
        var h = new WindowInteropHelper(w).EnsureHandle();
        var style = GetWindowLongPtr(h, -20).ToInt64() | 0x08000000L | 0x80L; // NOACTIVATE, TOOLWINDOW
        if (clickThrough) style |= 0x20L;
        SetWindowLongPtr(h, -20, new(style));
    }

    public static void Position(Window w, PointD p, double? width = null, double? height = null)
    {
        var h = new WindowInteropHelper(w).Handle;
        if (h == IntPtr.Zero) return;
        var x = (int)Math.Round(p.X); var y = (int)Math.Round(p.Y);
        var cx = (int)Math.Ceiling(width ?? 0); var cy = (int)Math.Ceiling(height ?? 0);
        var flags = 0x10u | (width is null ? 0x1u : 0);
        if (GetWindowRect(h, out var old))
        {
            var samePosition = old.Left == x && old.Top == y;
            var sameSize = width is null || old.Right - old.Left == cx && old.Bottom - old.Top == cy;
            if (samePosition && sameSize) return;
            if (samePosition) flags |= 0x2;
            if (sameSize) flags |= 0x1;
        }
        if (!SetWindowPos(h, new(-1), x, y, cx, cy, flags))
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Windows tidak dapat memindahkan Momo.");
    }
    public static RectD WindowBounds(Window window)
    {
        GetWindowRect(new WindowInteropHelper(window).Handle, out var r);
        return new(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
    }
    public static void KeepAbove(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle != IntPtr.Zero && window.IsVisible)
            SetWindowPos(handle, new(-1), 0, 0, 0, 0, 0x13); // NOMOVE | NOSIZE | NOACTIVATE
    }
    public static bool IsAbove(Window window, Window other)
    {
        var target = new WindowInteropHelper(window).Handle;
        var h = new WindowInteropHelper(other).Handle;
        for (var i = 0; i < 1000 && h != IntPtr.Zero; i++)
        { h = GetWindow(h, 3); if (h == target) return true; }
        return false;
    }
    public static bool IsPassiveClickThrough(Window window)
    {
        var style = GetWindowLongPtr(new WindowInteropHelper(window).Handle, -20).ToInt64();
        return (style & 0x08000020L) == 0x08000020L;
    }
}

using System;
using System.Runtime.InteropServices;

namespace ProtectEye.Services;

public static class Win32Api
{
    [StructLayout(LayoutKind.Sequential)]
    public struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll")]
    public static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    // 闲置时间（秒）
    public static double GetIdleTimeSeconds()
    {
        var lastInputInfo = new LASTINPUTINFO();
        lastInputInfo.cbSize = (uint)Marshal.SizeOf(lastInputInfo);
        
        if (GetLastInputInfo(ref lastInputInfo))
        {
            uint idleTicks = (uint)Environment.TickCount - lastInputInfo.dwTime;
            return idleTicks / 1000.0;
        }

        return 0;
    }
    
    [DllImport("gdi32.dll")]
    public static extern bool DeleteObject(IntPtr hObject);
    
    // For transparent window
    public const int WS_EX_TRANSPARENT = 0x00000020;
    public const int GWL_EXSTYLE = -20;

    [DllImport("user32.dll")]
    public static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll")]
    public static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);
    
    public static void SetWindowExTransparent(IntPtr hwnd)
    {
        var extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT);
    }
    
    // To check if a full screen app is running (DND Mode)
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetDesktopWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetShellWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowRect(IntPtr hwnd, out RECT rc);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    private const uint MONITOR_DEFAULTTONEAREST = 2;

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [StructLayout(LayoutKind.Sequential)]
    public struct MONITORINFO
    {
        public uint cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    public static bool IsForegroundFullScreen()
    {
        IntPtr foregroundWindow = GetForegroundWindow();
        IntPtr desktopWindow = GetDesktopWindow();
        IntPtr shellWindow = GetShellWindow();

        if (foregroundWindow == desktopWindow || foregroundWindow == shellWindow || foregroundWindow == IntPtr.Zero)
        {
            return false;
        }

        GetWindowRect(foregroundWindow, out RECT wndRect);
        
        IntPtr monitor = MonitorFromWindow(foregroundWindow, MONITOR_DEFAULTTONEAREST);
        MONITORINFO mi = new MONITORINFO();
        mi.cbSize = (uint)Marshal.SizeOf(mi);
        if (GetMonitorInfo(monitor, ref mi))
        {
            // 检测窗口是否覆盖了整个显示器（如果是最大化普通窗口，bottom 通常会小于显示器底部，因为有任务栏）
            return wndRect.left <= mi.rcMonitor.left &&
                   wndRect.top <= mi.rcMonitor.top &&
                   wndRect.right >= mi.rcMonitor.right &&
                   wndRect.bottom >= mi.rcMonitor.bottom;
        }

        return false;
    }

    [DllImport("user32.dll")]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}

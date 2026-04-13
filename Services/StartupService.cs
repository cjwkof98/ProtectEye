using Microsoft.Win32;

namespace ProtectEye.Services;

public static class StartupService
{
    private const string RegKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppKey = "ProtectEye";

    public static bool IsStartupEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegKey, false);
            return key?.GetValue(AppKey) != null;
        }
        catch { return false; }
    }

    public static void SetStartup(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegKey, true)!;
            if (enable)
            {
                var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                if (exePath != null) key.SetValue(AppKey, $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue(AppKey, false);
            }
        }
        catch { }
    }
}

using Microsoft.Win32;

namespace Maskot.Windows.Platform;

internal static class LoginStartup
{
    private const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(Key);
        return key?.GetValue("Maskot") is string;
    }
    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(Key);
        if (enabled)
        {
            var path = Environment.ProcessPath ?? throw new InvalidOperationException("Lokasi aplikasi tidak tersedia.");
            if (Path.GetFileNameWithoutExtension(path).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Aktifkan jalankan saat login dari build Maskot.exe, bukan dotnet run.");
            key.SetValue("Maskot", "\"" + path + "\"");
        }
        else key.DeleteValue("Maskot", false);
    }
}

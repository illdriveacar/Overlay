using Microsoft.Win32;

namespace CECOMPuang.Services;

internal static class StartupRegistry
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovedKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    private const string ValueName = "CECOMPuang";

    // StartupApproved\Run uses a 12-byte REG_BINARY value. The first byte determines
    // enabled (0x02) / disabled (0x03). The remaining 11 bytes are a FILETIME of when
    // the user last toggled the state — zeros are fine for "enabled now".
    private static readonly byte[] EnabledFlag = { 0x02, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

    public static bool IsEnabled()
    {
        using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        if (runKey == null) return false;

        if (runKey.GetValue(ValueName) is not string raw || string.IsNullOrEmpty(raw))
            return false;

        var exePath = GetExecutablePath();
        if (!string.Equals(Unquote(raw), exePath, StringComparison.OrdinalIgnoreCase))
            return false;

        // If Task Manager / Settings has disabled the entry, the first byte will be 0x03.
        using var approvedKey = Registry.CurrentUser.OpenSubKey(ApprovedKeyPath, writable: false);
        if (approvedKey?.GetValue(ValueName) is byte[] bytes && bytes.Length > 0 && bytes[0] == 0x03)
            return false;

        return true;
    }

    public static void Enable()
    {
        using (var runKey = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true))
        {
            runKey?.SetValue(ValueName, $"\"{GetExecutablePath()}\"");
        }
        using (var approvedKey = Registry.CurrentUser.CreateSubKey(ApprovedKeyPath, writable: true))
        {
            approvedKey?.SetValue(ValueName, EnabledFlag, RegistryValueKind.Binary);
        }
    }

    public static void Disable()
    {
        using (var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true))
        {
            if (runKey?.GetValue(ValueName) != null)
                runKey.DeleteValue(ValueName, throwOnMissingValue: false);
        }
        using (var approvedKey = Registry.CurrentUser.OpenSubKey(ApprovedKeyPath, writable: true))
        {
            if (approvedKey?.GetValue(ValueName) != null)
                approvedKey.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }

    private static string GetExecutablePath() =>
        Environment.ProcessPath
        ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName
        ?? "";

    private static string Unquote(string s) =>
        s.Length >= 2 && s.StartsWith('"') && s.EndsWith('"') ? s[1..^1] : s;
}

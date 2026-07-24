using System;
using Microsoft.Win32;

namespace Viper.Utilities;

/// <summary>
/// Manages Viper's "Launch at Startup" behavior via the Windows Registry
/// (HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run).
/// No admin rights required for HKCU.
/// </summary>
public static class StartupManager
{
    private const string RegistryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "Viper";

    /// <summary>
    /// Returns true if Viper is registered to launch at startup.
    /// </summary>
    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryKey, false);
        return key?.GetValue(AppName) is not null;
    }

    /// <summary>
    /// Registers or unregisters Viper from Windows startup.
    /// </summary>
    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryKey, true);
        if (key is null) return;

        if (enabled)
        {
            string exePath = Environment.ProcessPath ?? string.Empty;
            if (!string.IsNullOrEmpty(exePath))
                key.SetValue(AppName, $"\"{exePath}\" --background");
        }
        else
        {
            key.DeleteValue(AppName, throwOnMissingValue: false);
        }
    }
}

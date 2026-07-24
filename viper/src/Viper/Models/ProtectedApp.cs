namespace Viper.Models;

/// <summary>
/// An application registered for password protection.
/// Stored as part of <see cref="ViperConfig"/>.
/// </summary>
public sealed class ProtectedApp
{
    /// <summary>
    /// Display name shown in the UI and lock screen (e.g. "Google Chrome").
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Executable file name to match against running processes (e.g. "chrome.exe").
    /// This is the process name, not a full path.
    /// </summary>
    public string ExecutableName { get; set; } = string.Empty;
}

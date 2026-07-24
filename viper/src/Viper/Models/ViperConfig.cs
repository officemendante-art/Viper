using System.Collections.Generic;

namespace Viper.Models;

/// <summary>
/// Root configuration model. Serialized to %ProgramData%\Viper\config.json.
/// Single password. No master password. No lockdown mode.
/// </summary>
public sealed class ViperConfig
{
    /// <summary>
    /// Argon2id hash of the user's password. Null until first-run setup completes.
    /// </summary>
    public byte[]? PasswordHash { get; set; }

    /// <summary>
    /// Random 16-byte salt used when hashing the password.
    /// </summary>
    public byte[]? PasswordSalt { get; set; }

    /// <summary>
    /// Argon2id parameters stored alongside the hash for algorithm agility.
    /// If null, defaults are used.
    /// </summary>
    public Argon2Params? HashParameters { get; set; }

    /// <summary>
    /// Applications protected by Viper.
    /// </summary>
    public List<ProtectedApp> ProtectedApps { get; set; } = new();

    /// <summary>
    /// Whether Viper should launch at Windows startup.
    /// </summary>
    public bool LaunchAtStartup { get; set; } = true;

    /// <summary>
    /// Returns true if initial setup has been completed (password is set).
    /// </summary>
    public bool IsSetupComplete => PasswordHash is not null && PasswordSalt is not null;
}

/// <summary>
/// Stored Argon2id parameters for algorithm agility.
/// </summary>
public sealed class Argon2Params
{
    public int MemoryKib { get; set; } = 19456;
    public int Iterations { get; set; } = 2;
    public int Parallelism { get; set; } = 1;

    public static Argon2Params Default => new();
}

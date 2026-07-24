using System;
using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using Viper.Models;

namespace Viper.Services;

/// <summary>
/// Argon2id password hashing and verification.
/// Preserves the security guarantees from the original Viper.Security module:
/// - 16-byte random salt per password
/// - Constant-time comparison via FixedTimeEquals
/// - ZeroMemory cleanup in finally blocks
/// </summary>
public static class PasswordService
{
    private const int SaltSizeBytes = 16;
    private const int HashSizeBytes = 32;

    /// <summary>
    /// Hashes a plaintext password and returns (hash, salt, parameters).
    /// </summary>
    public static (byte[] Hash, byte[] Salt, Argon2Params Parameters) HashPassword(string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        var parameters = Argon2Params.Default;
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);

        try
        {
            byte[] hash = ComputeHash(passwordBytes, salt, parameters);
            return (hash, salt, parameters);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
        }
    }

    /// <summary>
    /// Verifies a plaintext password against stored hash, salt, and parameters.
    /// Uses constant-time comparison to prevent timing side-channels.
    /// </summary>
    public static bool Verify(string password, byte[] storedHash, byte[] storedSalt, Argon2Params? parameters = null)
    {
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(storedHash);
        ArgumentNullException.ThrowIfNull(storedSalt);

        parameters ??= Argon2Params.Default;
        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);

        try
        {
            byte[] computedHash = ComputeHash(passwordBytes, storedSalt, parameters);
            return CryptographicOperations.FixedTimeEquals(computedHash, storedHash);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
        }
    }

    /// <summary>
    /// Convenience overload that verifies against a <see cref="ViperConfig"/>.
    /// Returns false if the config has no password set.
    /// </summary>
    public static bool Verify(string password, ViperConfig config)
    {
        if (config.PasswordHash is null || config.PasswordSalt is null)
            return false;

        return Verify(password, config.PasswordHash, config.PasswordSalt, config.HashParameters);
    }

    private static byte[] ComputeHash(byte[] passwordBytes, byte[] salt, Argon2Params parameters)
    {
        using var argon2 = new Argon2id(passwordBytes)
        {
            Salt = salt,
            DegreeOfParallelism = parameters.Parallelism,
            Iterations = parameters.Iterations,
            MemorySize = parameters.MemoryKib,
        };

        return argon2.GetBytes(HashSizeBytes);
    }
}

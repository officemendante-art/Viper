using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace Viper.Security;

/// <summary>
/// Hashes and verifies passwords using Argon2id, per SPEC.md §3.
///
/// This class is the trust root of the entire Viper project (SPEC.md §6):
/// it has zero dependencies on any other Viper module, and every other
/// module that touches a password goes through here rather than calling
/// a hashing library directly. That boundary is deliberate — it's what
/// keeps "how passwords are hashed" a one-file decision instead of a
/// scattered one.
///
/// Threading: stateless and safe to use from multiple threads — each
/// call allocates its own buffers and disposes them before returning.
/// </summary>
public static class PasswordHasher
{
    private const int SaltSizeBytes = 16;   // SPEC.md §3.1
    private const int HashSizeBytes = 32;   // 256-bit output

    /// <summary>
    /// Hashes a plaintext password into a new <see cref="PasswordRecord"/>
    /// using a fresh, cryptographically random salt. The caller is
    /// responsible for clearing <paramref name="password"/> after this
    /// returns if they hold it in a mutable buffer they control
    /// (SPEC.md §3.1 memory hygiene) — this method does not, and cannot,
    /// clear a .NET <see cref="string"/> the caller still holds a
    /// reference to elsewhere.
    /// </summary>
    public static PasswordRecord Hash(string password, Argon2Parameters? parameters = null)
    {
        ArgumentNullException.ThrowIfNull(password);
        parameters ??= Argon2Parameters.Default;

        byte[] salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);

        try
        {
            byte[] hash = ComputeHash(passwordBytes, salt, parameters);
            return new PasswordRecord(
                Algorithm: PasswordRecord.Argon2IdAlgorithmName,
                FormatVersion: PasswordRecord.CurrentFormatVersion,
                Salt: salt,
                Hash: hash,
                Parameters: parameters);
        }
        finally
        {
            // SPEC.md §3.1 — clear password material from memory as soon
            // as we're done with it, not when the GC eventually gets to it.
            CryptographicOperations.ZeroMemory(passwordBytes);
        }
    }

    /// <summary>
    /// Verifies a plaintext password against a stored record using a
    /// constant-time comparison (SPEC.md §3.1), so a failed check takes
    /// the same time regardless of where the mismatch occurred.
    ///
    /// Re-derives the hash using the exact parameters stored in
    /// <paramref name="record"/> — never the current
    /// <see cref="Argon2Parameters.Default"/> — which is what makes old
    /// records still verify correctly after a future version changes the
    /// default tuning (SPEC.md §3.2, algorithm agility).
    /// </summary>
    public static bool Verify(string password, PasswordRecord record)
    {
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(record);

        if (record.Algorithm != PasswordRecord.Argon2IdAlgorithmName)
        {
            // A future algorithm migration adds a branch here, not a
            // redesign — see SPEC.md §3.2.
            throw new NotSupportedException(
                $"Unsupported password algorithm: '{record.Algorithm}'.");
        }

        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
        try
        {
            byte[] computedHash = ComputeHash(passwordBytes, record.Salt, record.Parameters);

            // SPEC.md §3.1 — constant-time comparison, specifically to
            // avoid a timing side-channel on early-exit byte comparison.
            return CryptographicOperations.FixedTimeEquals(computedHash, record.Hash);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
        }
    }

    private static byte[] ComputeHash(byte[] passwordBytes, byte[] salt, Argon2Parameters parameters)
    {
        using var argon2 = new Argon2id(passwordBytes)
        {
            Salt = salt,
            DegreeOfParallelism = parameters.Parallelism,
            Iterations = parameters.Iterations,
            MemorySize = parameters.MemoryKib,
        };

        // GetBytes is synchronous and CPU-bound by design — Argon2id's
        // whole purpose is to make this expensive. Callers on a UI thread
        // (the lock screen, SPEC.md §4) must invoke this off the UI
        // thread; that is a Viper.UI concern, not this module's.
        return argon2.GetBytes(HashSizeBytes);
    }
}

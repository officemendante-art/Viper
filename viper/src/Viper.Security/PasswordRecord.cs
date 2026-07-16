namespace Viper.Security;

/// <summary>
/// What actually gets written to disk for a password. Never contains
/// plaintext, and never contains anything the plaintext can be recovered
/// from — only a salted hash plus the parameters needed to reproduce it
/// (SPEC.md §3.1, §3.2).
///
/// This is a plain data record with no behavior, so it serializes cleanly
/// to the config store (Viper.Config module, per SPEC.md §6) without
/// pulling any hashing logic along with it.
/// </summary>
public sealed record PasswordRecord(
    string Algorithm,      // e.g. "argon2id" — always explicit, never assumed
    int FormatVersion,     // bumped only if the record shape itself changes
    byte[] Salt,
    byte[] Hash,
    Argon2Parameters Parameters)
{
    public const string Argon2IdAlgorithmName = "argon2id";
    public const int CurrentFormatVersion = 1;
}

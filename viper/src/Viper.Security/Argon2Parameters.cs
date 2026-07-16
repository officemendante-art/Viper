namespace Viper.Security;

/// <summary>
/// Tunable Argon2id parameters, stored alongside every password record
/// (SPEC.md §3.2 — algorithm agility). Never hard-code these inline at a
/// call site; always go through a named <see cref="Argon2Parameters"/>
/// instance so old records keep verifying correctly even after the
/// default tuning changes in a later version.
/// </summary>
/// <param name="MemoryKib">Memory cost, in KiB.</param>
/// <param name="Iterations">Time cost (number of passes).</param>
/// <param name="Parallelism">Degree of parallelism.</param>
public sealed record Argon2Parameters(int MemoryKib, int Iterations, int Parallelism)
{
    /// <summary>
    /// Starting point in line with current OWASP guidance for interactive
    /// login use (SPEC.md §3.1). Deliberately conservative rather than
    /// maximal — this runs synchronously on the lock-screen's critical
    /// path, and an unlock that takes multiple seconds is itself a
    /// usability problem worth avoiding.
    /// </summary>
    public static Argon2Parameters Default { get; } = new(
        MemoryKib: 19456,   // 19 MiB
        Iterations: 2,
        Parallelism: 1);
}

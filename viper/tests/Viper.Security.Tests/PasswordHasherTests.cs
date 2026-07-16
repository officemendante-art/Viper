using Viper.Security;
using Xunit;

namespace Viper.Security.Tests;

public class PasswordHasherTests
{
    // Use cheap parameters for fast tests — the real strength lives in
    // Argon2Parameters.Default, tested separately below. Test-suite speed
    // matters here because these tests run on every module change
    // (SPEC.md §9, Milestone A step 1 says "unit-tested in isolation" —
    // that only stays true in practice if the suite is fast enough to
    // actually run every time).
    private static readonly Argon2Parameters FastTestParams = new(
        MemoryKib: 8192, Iterations: 1, Parallelism: 1);

    [Fact]
    public void Verify_ReturnsTrue_ForCorrectPassword()
    {
        var record = PasswordHasher.Hash("correct horse battery staple", FastTestParams);

        Assert.True(PasswordHasher.Verify("correct horse battery staple", record));
    }

    [Fact]
    public void Verify_ReturnsFalse_ForIncorrectPassword()
    {
        var record = PasswordHasher.Hash("correct horse battery staple", FastTestParams);

        Assert.False(PasswordHasher.Verify("wrong password entirely", record));
    }

    [Fact]
    public void Verify_ReturnsFalse_ForCloseButNotExactPassword()
    {
        // Off-by-one-character check — guards against any accidental
        // prefix-matching or truncation bug in the comparison path.
        var record = PasswordHasher.Hash("correct horse battery staple", FastTestParams);

        Assert.False(PasswordHasher.Verify("correct horse battery staplE", record));
    }

    [Fact]
    public void Hash_NeverStoresPlaintext()
    {
        const string plaintext = "correct horse battery staple";
        var record = PasswordHasher.Hash(plaintext, FastTestParams);

        // The whole point of SPEC.md §3.1: the plaintext must not appear
        // anywhere in the persisted record, in any form.
        Assert.DoesNotContain(plaintext, Convert.ToBase64String(record.Hash));
        Assert.DoesNotContain(plaintext, Convert.ToBase64String(record.Salt));
    }

    [Fact]
    public void Hash_GeneratesUniqueSalt_ForEachCall()
    {
        // SPEC.md §3.1 — "unique cryptographically secure random salt for
        // each password", and explicitly "never reused" for the two
        // password purposes in §3. This test proves it at the mechanism
        // level: hashing the exact same password twice must not produce
        // the same salt.
        var record1 = PasswordHasher.Hash("same password", FastTestParams);
        var record2 = PasswordHasher.Hash("same password", FastTestParams);

        Assert.False(record1.Salt.SequenceEqual(record2.Salt));
        // A direct consequence: identical passwords must not produce
        // identical hashes either (defeats rainbow-table-style comparison
        // between two Viper installs, or between the App Unlock and
        // Master password if a user reused a password across the two).
        Assert.False(record1.Hash.SequenceEqual(record2.Hash));
    }

    [Fact]
    public void Verify_UsesStoredParameters_NotCurrentDefault()
    {
        // SPEC.md §3.2 — algorithm agility. A record hashed under old
        // parameters must still verify correctly even if
        // Argon2Parameters.Default changes in a later version, because
        // Verify() must re-derive using record.Parameters, not
        // Argon2Parameters.Default. This test uses two deliberately
        // different parameter sets to prove Verify() doesn't silently
        // fall back to the default.
        var oldParams = new Argon2Parameters(MemoryKib: 8192, Iterations: 1, Parallelism: 1);
        var newParams = new Argon2Parameters(MemoryKib: 12288, Iterations: 1, Parallelism: 1);

        var oldRecord = PasswordHasher.Hash("a password", oldParams);
        var newRecord = PasswordHasher.Hash("a password", newParams);

        Assert.True(PasswordHasher.Verify("a password", oldRecord));
        Assert.True(PasswordHasher.Verify("a password", newRecord));
        // And a record's stored hash must genuinely depend on which
        // parameters produced it — different memory cost, same password
        // and salt-generation call, different hash bytes.
        Assert.False(oldRecord.Hash.SequenceEqual(newRecord.Hash));
    }

    [Fact]
    public void Verify_Throws_ForUnsupportedAlgorithm()
    {
        var bogusRecord = new PasswordRecord(
            Algorithm: "md5",   // deliberately not argon2id
            FormatVersion: 1,
            Salt: new byte[16],
            Hash: new byte[32],
            Parameters: Argon2Parameters.Default);

        Assert.Throws<NotSupportedException>(() =>
            PasswordHasher.Verify("anything", bogusRecord));
    }

    [Fact]
    public void Hash_SupportsLongHighEntropyPasswords()
    {
        // SPEC.md §3.4 — "no practical length ceiling — support at
        // minimum 256 characters". Uses the exact class of password
        // described in the spec: long, mixed-case, digits, symbols.
        string longPassword = string.Concat(Enumerable.Repeat(
            "0[=q@vc*vjFy;-0~:}@6hRR4+W/IPS;J0G?/^F^E27-R]wWR-l", 6)); // 300 chars

        var record = PasswordHasher.Hash(longPassword, FastTestParams);

        Assert.True(PasswordHasher.Verify(longPassword, record));
    }

    [Fact]
    public void Hash_SupportsUnicodePasswords()
    {
        const string unicodePassword = "пароль-密码-🔒-mot de passe";

        var record = PasswordHasher.Hash(unicodePassword, FastTestParams);

        Assert.True(PasswordHasher.Verify(unicodePassword, record));
    }

    [Fact]
    public void Hash_DefaultParameters_MatchSpecifiedOwaspBaseline()
    {
        // Locks in SPEC.md §3.1's stated defaults as an explicit,
        // reviewable assertion — if someone changes Argon2Parameters
        // .Default later, this test forces them to consciously update
        // this expectation rather than silently drifting the security
        // baseline.
        Assert.Equal(19456, Argon2Parameters.Default.MemoryKib);
        Assert.Equal(2, Argon2Parameters.Default.Iterations);
        Assert.Equal(1, Argon2Parameters.Default.Parallelism);
    }

    [Fact]
    public void AppUnlockAndMasterPassword_ProduceIndependentRecords_EvenIfIdentical()
    {
        // SPEC.md §3 — "Two independent secrets ... never shared, never
        // interchangeable". Simulates the scenario where an owner
        // (unwisely) reuses the same string for both. Even then, the two
        // PasswordRecord instances must be fully independent (different
        // salt, different hash) — Viper.Config (a different module) is
        // what enforces they're stored under different keys; this test
        // just proves the hasher itself gives no help to correlating them.
        const string samePassword = "ReusedAcrossBothPurposes123!";

        var appUnlockRecord = PasswordHasher.Hash(samePassword, FastTestParams);
        var masterRecord = PasswordHasher.Hash(samePassword, FastTestParams);

        Assert.False(appUnlockRecord.Salt.SequenceEqual(masterRecord.Salt));
        Assert.False(appUnlockRecord.Hash.SequenceEqual(masterRecord.Hash));
    }
}

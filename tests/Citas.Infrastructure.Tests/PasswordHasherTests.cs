using Citas.Infrastructure.Security;

namespace Citas.Infrastructure.Tests;

public class PasswordHasherTests
{
    private readonly IdentityPasswordHasher _hasher = new();

    [Fact]
    public void Hash_IsNotThePassword_AndVerifiesWithIt()
    {
        string hash = _hasher.Hash("Una-clave-larga-1");

        Assert.DoesNotContain("Una-clave-larga-1", hash, StringComparison.Ordinal);
        Assert.True(_hasher.Verify(hash, "Una-clave-larga-1"));
    }

    [Fact]
    public void Verify_WrongPassword_IsFalse()
    {
        string hash = _hasher.Hash("Una-clave-larga-1");

        Assert.False(_hasher.Verify(hash, "una-clave-larga-1"));
        Assert.False(_hasher.Verify(hash, string.Empty));
    }

    [Fact]
    public void Hash_SamePasswordTwice_GivesDifferentHashes_BothValid()
    {
        string first = _hasher.Hash("Una-clave-larga-1");
        string second = _hasher.Hash("Una-clave-larga-1");

        Assert.NotEqual(first, second);
        Assert.True(_hasher.Verify(first, "Una-clave-larga-1"));
        Assert.True(_hasher.Verify(second, "Una-clave-larga-1"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Verify_WithoutAHash_IsFalse_EvenForAnyPassword(string? hash)
    {
        Assert.False(_hasher.Verify(hash, "Una-clave-larga-1"));
        Assert.False(_hasher.Verify(hash, string.Empty));
    }

    [Fact]
    public void Verify_AMalformedHash_IsFalseAndDoesNotThrow()
    {
        Assert.False(_hasher.Verify("esto-no-es-un-hash", "Una-clave-larga-1"));
    }

    [Fact]
    public void Verify_WithoutAHash_StillDoesTheWorkOfAHash()
    {
        // Same order of magnitude as a real check: both PBKDF2 runs take far more than a plain string comparison.
        string hash = _hasher.Hash("x");
        var real = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < 3; i++)
        {
            _hasher.Verify(hash, "y");
        }

        real.Stop();
        var decoy = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < 3; i++)
        {
            _hasher.Verify(null, "y");
        }

        decoy.Stop();

        Assert.True(decoy.Elapsed > real.Elapsed / 5, $"real {real.Elapsed.TotalMilliseconds} ms, decoy {decoy.Elapsed.TotalMilliseconds} ms");
    }
}

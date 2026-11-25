using Citas.Application.Abstractions;

namespace Citas.Application.Tests.Fakes;

/// <summary>Not a hash at all: "hash:" + password. Counts every check so tests can prove the password is always verified.</summary>
public sealed class FakePasswordHasher : IPasswordHasher
{
    public int VerifyCalls { get; private set; }

    public string Hash(string password) => "hash:" + password;

    public bool Verify(string? hash, string password)
    {
        VerifyCalls++;
        return hash is not null && hash == Hash(password);
    }
}

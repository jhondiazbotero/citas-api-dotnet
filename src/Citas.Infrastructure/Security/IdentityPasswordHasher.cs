using Citas.Application.Abstractions;
using Citas.Domain.Staff;
using Microsoft.AspNetCore.Identity;

namespace Citas.Infrastructure.Security;

/// <summary>
/// <see cref="IPasswordHasher"/> over the hasher of ASP.NET Core Identity (PBKDF2 with a random salt per password, versioned format).
/// Only the hasher is used, not the rest of Identity. Safe to share between threads.
/// </summary>
public sealed class IdentityPasswordHasher : IPasswordHasher
{
    private static readonly StaffUser Nobody = StaffUser.Create("nobody@invalid.local", "x", StaffRole.Admin).Value;

    private readonly PasswordHasher<StaffUser> _hasher = new();

    // A real hash of a random password, made once: verifying against it costs the same as verifying against a stored one.
    private readonly string _decoy;

    public IdentityPasswordHasher()
    {
        _decoy = _hasher.HashPassword(Nobody, Guid.NewGuid().ToString("N"));
    }

    public string Hash(string password) => _hasher.HashPassword(Nobody, password);

    public bool Verify(string? hash, string password)
    {
        if (!string.IsNullOrWhiteSpace(hash))
        {
            try
            {
                return _hasher.VerifyHashedPassword(Nobody, hash, password) != PasswordVerificationResult.Failed;
            }
            catch (FormatException)
            {
                // Not a hash this hasher made (for example a corrupted column): same answer as a wrong password.
            }
        }

        _hasher.VerifyHashedPassword(Nobody, _decoy, password);
        return false;
    }
}

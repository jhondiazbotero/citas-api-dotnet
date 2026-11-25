namespace Citas.Application.Abstractions;

public interface IPasswordHasher
{
    /// <summary>A salted, slow hash of the password; only this string is ever stored.</summary>
    string Hash(string password);

    /// <summary>
    /// True when the password matches the hash. A null or unreadable hash is compared against a made-up one and gives false,
    /// so a login for a user that does not exist costs the same time as one for a user that does.
    /// </summary>
    bool Verify(string? hash, string password);
}

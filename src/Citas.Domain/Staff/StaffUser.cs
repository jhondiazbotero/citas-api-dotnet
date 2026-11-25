using Citas.Domain.Common;
using Citas.Domain.Errors;

namespace Citas.Domain.Staff;

/// <summary>A member of the clinic's staff who can sign in. Only a user with the Professional role is linked to a professional.</summary>
public sealed class StaffUser
{
    public const int MaxEmailLength = 254;

    private StaffUser(string email, string passwordHash, StaffRole role, Guid? professionalId)
    {
        Id = Guid.NewGuid();
        Email = email;
        PasswordHash = passwordHash;
        Role = role;
        ProfessionalId = professionalId;
        IsActive = true;
    }

    public Guid Id { get; }

    public string Email { get; }

    public string PasswordHash { get; }

    public StaffRole Role { get; }

    public Guid? ProfessionalId { get; }

    public bool IsActive { get; private set; }

    /// <summary>Trimmed and lower case, the form in which emails are stored and looked up. Null becomes an empty string.</summary>
    public static string NormalizeEmail(string? email) => (email ?? string.Empty).Trim().ToLowerInvariant();

    /// <param name="passwordHash">Already hashed: the domain never sees a plain password.</param>
    public static Result<StaffUser> Create(string? email, string? passwordHash, StaffRole role, Guid? professionalId = null)
    {
        string normalized = NormalizeEmail(email);
        if (!IsValidEmail(normalized))
        {
            return StaffUserErrors.InvalidEmail;
        }

        if (!Enum.IsDefined(role))
        {
            return StaffUserErrors.InvalidRole;
        }

        if (role == StaffRole.Professional && (professionalId is null || professionalId == Guid.Empty))
        {
            return StaffUserErrors.ProfessionalRequired;
        }

        if (role != StaffRole.Professional && professionalId is not null)
        {
            return StaffUserErrors.ProfessionalNotAllowed;
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            return StaffUserErrors.InvalidPasswordHash;
        }

        return new StaffUser(normalized, passwordHash, role, professionalId);
    }

    public void Deactivate() => IsActive = false;

    // One '@', something before it, and a domain with a dot that does not start or end with it. No spaces.
    private static bool IsValidEmail(string email)
    {
        if (email.Length is 0 or > MaxEmailLength || email.Any(char.IsWhiteSpace))
        {
            return false;
        }

        int at = email.IndexOf('@', StringComparison.Ordinal);
        if (at < 1 || at != email.LastIndexOf('@'))
        {
            return false;
        }

        string domain = email[(at + 1)..];
        return domain.Contains('.', StringComparison.Ordinal)
            && !domain.StartsWith('.')
            && !domain.EndsWith('.');
    }
}

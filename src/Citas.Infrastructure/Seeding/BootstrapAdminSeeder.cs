using Citas.Application.Abstractions;
using Citas.Application.StaffUsers;
using Citas.Domain.Common;
using Citas.Domain.Staff;
using Citas.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Citas.Infrastructure.Seeding;

/// <summary>
/// Creates the first administrator of a real deployment, the one who then creates everybody else through the API.
/// It only acts when both <c>Bootstrap:AdminEmail</c> and <c>Bootstrap:AdminPassword</c> are set and the database has no
/// administrator at all, so running it on every start is harmless. The password comes from the environment, is hashed
/// before it is stored and is never written to a log or to an error message.
/// </summary>
public sealed class BootstrapAdminSeeder(
    CitasDbContext db,
    IConfiguration configuration,
    IPasswordHasher passwordHasher,
    ILogger<BootstrapAdminSeeder> logger)
{
    public const string EmailKey = "Bootstrap:AdminEmail";

    public const string PasswordKey = "Bootstrap:AdminPassword";

    /// <summary>Returns true when it created the administrator. A wrong setting stops the startup with an explanation.</summary>
    public async Task<bool> EnsureAdminAsync(CancellationToken cancellationToken)
    {
        string? email = configuration[EmailKey];
        string? password = configuration[PasswordKey];
        bool hasEmail = !string.IsNullOrWhiteSpace(email);
        bool hasPassword = !string.IsNullOrWhiteSpace(password);

        if (!hasEmail && !hasPassword)
        {
            return false;
        }

        if (hasEmail != hasPassword)
        {
            logger.LogWarning(
                "The first administrator was not created: {EmailSetting} and {PasswordSetting} must both be set",
                EmailKey,
                PasswordKey);
            return false;
        }

        if (await db.StaffUsers.AnyAsync(u => u.Role == StaffRole.Admin, cancellationToken))
        {
            return false;
        }

        if (!CreateStaffUserValidator.IsAcceptablePassword(password))
        {
            throw new InvalidOperationException(
                $"'{PasswordKey}' must be {CreateStaffUserValidator.MinPasswordLength} to {CreateStaffUserValidator.MaxPasswordLength} " +
                "characters and not only spaces. The first administrator was not created.");
        }

        Result<StaffUser> created = StaffUser.Create(email, passwordHasher.Hash(password!), StaffRole.Admin);
        if (created.IsFailure)
        {
            throw new InvalidOperationException($"'{EmailKey}' is not valid: {created.Error.Message}");
        }

        StaffUser admin = created.Value;
        if (await db.StaffUsers.AnyAsync(u => u.Email == admin.Email, cancellationToken))
        {
            throw new InvalidOperationException(
                $"'{EmailKey}' already belongs to a user who is not an administrator. Use another address.");
        }

        db.StaffUsers.Add(admin);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Another instance starting at the same moment may have created the same administrator first.
            db.Entry(admin).State = EntityState.Detached;
            if (await db.StaffUsers.AnyAsync(u => u.Role == StaffRole.Admin, cancellationToken))
            {
                return false;
            }

            throw;
        }

        logger.LogInformation("The first administrator was created for {Email}", admin.Email);
        return true;
    }
}

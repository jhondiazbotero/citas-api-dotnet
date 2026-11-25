using Citas.Application.Abstractions;
using Citas.Domain.Common;
using Citas.Domain.Errors;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Citas.Infrastructure.Persistence;

/// <summary>
/// Saves the changes and turns the database rejections that map to a business rule into domain errors.
/// The database is the last line of defense: two requests can pass the in-memory checks at the same time.
/// </summary>
public sealed class UnitOfWork(CitasDbContext db) : IUnitOfWork
{
    public async Task<Result> SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(AppointmentErrors.ConcurrencyConflict);
        }
        catch (DbUpdateException ex) when (Translate(ex) is { } error)
        {
            return Result.Failure(error);
        }
    }

    private static Error? Translate(DbUpdateException exception) =>
        exception.InnerException is PostgresException postgres
            ? (postgres.SqlState, postgres.ConstraintName) switch
            {
                (PostgresErrorCodes.ExclusionViolation, ConstraintNames.ProfessionalOverlap) => AppointmentErrors.Overlap,
                (PostgresErrorCodes.ExclusionViolation, ConstraintNames.PatientOverlap) => AppointmentErrors.PatientOverlap,
                (PostgresErrorCodes.UniqueViolation, ConstraintNames.PatientDocumentUnique) => PatientErrors.DuplicateDocument,
                _ => null,
            }
            : null;
}

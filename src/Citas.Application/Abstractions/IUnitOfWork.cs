using Citas.Domain.Common;

namespace Citas.Application.Abstractions;

public interface IUnitOfWork
{
    /// <summary>
    /// Persists every change made through the repositories. Fails with a domain error when the store rejects
    /// the change (for example <c>AppointmentErrors.Overlap</c> when two bookings collide at the same time).
    /// </summary>
    Task<Result> SaveChangesAsync(CancellationToken cancellationToken);
}

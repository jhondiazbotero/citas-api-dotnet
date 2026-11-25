using Citas.Application.Abstractions;
using Citas.Domain.Appointments;
using Citas.Domain.ValueObjects;
using Citas.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Citas.Infrastructure.Repositories;

public sealed class AppointmentRepository(CitasDbContext db) : IAppointmentRepository
{
    public Task<Appointment?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Appointments.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public void Add(Appointment appointment) => db.Appointments.Add(appointment);

    public async Task<IReadOnlyList<Appointment>> GetScheduledOverlappingAsync(
        Guid professionalId,
        Guid patientId,
        TimeRange range,
        CancellationToken cancellationToken)
    {
        // Same rule as TimeRange.Overlaps: each one starts before the other ends, so touching edges do not count.
        DateTime start = range.Start;
        DateTime end = range.End;

        return await db.Appointments
            .Where(a => a.Status == AppointmentStatus.Scheduled
                && (a.ProfessionalId == professionalId || a.PatientId == patientId)
                && a.TimeRange.Start < end
                && start < a.TimeRange.End)
            .OrderBy(a => a.TimeRange.Start)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Appointment>> GetByProfessionalAsync(
        Guid professionalId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        // Filter by START date, both ends included: from 00:00 of "from" up to (not including) 00:00 of the day after "to".
        DateTime lower = from.ToDateTime(TimeOnly.MinValue);
        IQueryable<Appointment> query = db.Appointments.Where(a => a.ProfessionalId == professionalId && a.TimeRange.Start >= lower);

        query = to == DateOnly.MaxValue
            ? query
            : query.Where(a => a.TimeRange.Start < to.AddDays(1).ToDateTime(TimeOnly.MinValue));

        return await query.OrderBy(a => a.TimeRange.Start).ToListAsync(cancellationToken);
    }
}

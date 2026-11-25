using Citas.Domain.Appointments;
using Citas.Domain.ValueObjects;

namespace Citas.Application.Abstractions;

public interface IAppointmentRepository
{
    Task<Appointment?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    void Add(Appointment appointment);

    /// <summary>Scheduled appointments of the professional OR of the patient that overlap the range.</summary>
    Task<IReadOnlyList<Appointment>> GetScheduledOverlappingAsync(
        Guid professionalId,
        Guid patientId,
        TimeRange range,
        CancellationToken cancellationToken);

    /// <summary>
    /// Appointments of a professional, in any status, whose start date falls between the two dates (both included).
    /// </summary>
    Task<IReadOnlyList<Appointment>> GetByProfessionalAsync(
        Guid professionalId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken);
}

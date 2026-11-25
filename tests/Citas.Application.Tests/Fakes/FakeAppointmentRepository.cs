using Citas.Application.Abstractions;
using Citas.Domain.Appointments;
using Citas.Domain.ValueObjects;

namespace Citas.Application.Tests.Fakes;

public sealed class FakeAppointmentRepository : IAppointmentRepository
{
    public List<Appointment> Items { get; } = [];

    public Task<Appointment?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(a => a.Id == id));

    public void Add(Appointment appointment) => Items.Add(appointment);

    public Task<IReadOnlyList<Appointment>> GetScheduledOverlappingAsync(
        Guid professionalId,
        Guid patientId,
        TimeRange range,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Appointment>>(Items
            .Where(a => a.Status == AppointmentStatus.Scheduled
                && (a.ProfessionalId == professionalId || a.PatientId == patientId)
                && a.TimeRange.Overlaps(range))
            .ToList());

    public Task<IReadOnlyList<Appointment>> GetByProfessionalAsync(
        Guid professionalId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Appointment>>(Items
            .Where(a => a.ProfessionalId == professionalId
                && DateOnly.FromDateTime(a.TimeRange.Start) >= from
                && DateOnly.FromDateTime(a.TimeRange.Start) <= to)
            .ToList());
}

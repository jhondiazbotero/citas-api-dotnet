using Citas.Domain.Appointments;

namespace Citas.Application.Appointments;

/// <summary>Times are local clinic time. <c>Status</c> is the name of the state: Scheduled, Cancelled, Completed or NoShow.</summary>
public sealed record AppointmentResponse(
    Guid Id,
    Guid ProfessionalId,
    Guid PatientId,
    DateTime Start,
    DateTime End,
    string Status,
    string? Reason,
    string? CancellationReason,
    DateTime CreatedAt,
    DateTime? CancelledAt)
{
    internal static AppointmentResponse From(Appointment appointment) =>
        new(
            appointment.Id,
            appointment.ProfessionalId,
            appointment.PatientId,
            appointment.TimeRange.Start,
            appointment.TimeRange.End,
            appointment.Status.ToString(),
            appointment.Reason,
            appointment.CancellationReason,
            appointment.CreatedAt,
            appointment.CancelledAt);
}

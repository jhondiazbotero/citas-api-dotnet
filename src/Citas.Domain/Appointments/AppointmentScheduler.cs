using Citas.Domain.Common;
using Citas.Domain.Errors;
using Citas.Domain.Patients;
using Citas.Domain.Professionals;
using Citas.Domain.ValueObjects;

namespace Citas.Domain.Appointments;

/// <summary>
/// Domain service with the booking rules that need more than one appointment to decide.
/// All times are local clinic time; "now" always comes from the caller.
/// </summary>
public static class AppointmentScheduler
{
    private static readonly TimeSpan MinLeadTime = TimeSpan.FromHours(1);
    private static readonly TimeSpan MinNoticeToReschedule = TimeSpan.FromHours(24);

    /// <summary>
    /// Books a new appointment. Rules, in order: lead time, active professional, working hours,
    /// no overlap with the professional's scheduled appointments, no overlap with the patient's.
    /// </summary>
    public static Result<Appointment> Schedule(
        Professional professional,
        Patient patient,
        TimeRange range,
        string? reason,
        IEnumerable<Appointment> existingAppointments,
        DateTime now)
    {
        Result check = CheckBookingRules(
            professional.Id, patient.Id, professional, range, existingAppointments, ignoredAppointmentId: null, now);
        if (check.IsFailure)
        {
            return check.Error;
        }

        return Appointment.Create(professional.Id, patient.Id, range, reason, now);
    }

    /// <summary>
    /// Moves a scheduled appointment to a new range. Needs 24 hours before the ORIGINAL start;
    /// the new range passes the same rules as a new booking, ignoring the appointment itself.
    /// </summary>
    public static Result Reschedule(
        Appointment appointment,
        Professional professional,
        TimeRange newRange,
        IEnumerable<Appointment> existingAppointments,
        DateTime now)
    {
        if (appointment.Status != AppointmentStatus.Scheduled)
        {
            return Result.Failure(AppointmentErrors.NotScheduled);
        }

        if (appointment.ProfessionalId != professional.Id)
        {
            return Result.Failure(AppointmentErrors.ProfessionalMismatch);
        }

        if (appointment.TimeRange.Start - now < MinNoticeToReschedule)
        {
            return Result.Failure(AppointmentErrors.RescheduleTooLate);
        }

        Result check = CheckBookingRules(
            appointment.ProfessionalId, appointment.PatientId, professional, newRange, existingAppointments, appointment.Id, now);
        if (check.IsFailure)
        {
            return check;
        }

        appointment.MoveTo(newRange);
        return Result.Success();
    }

    /// <summary>
    /// Whether a professional could take a new appointment in this range right now: same rules as <see cref="Schedule"/>
    /// (lead time, active, working hours, no overlap with the professional's appointments), without needing a patient.
    /// Used to list free slots without copying the rules.
    /// </summary>
    public static Result CheckSlot(
        Professional professional,
        TimeRange range,
        IEnumerable<Appointment> existingAppointments,
        DateTime now) =>
        CheckBookingRules(
            professional.Id,
            Guid.Empty,
            professional,
            range,
            existingAppointments.Where(a => a.ProfessionalId == professional.Id),
            ignoredAppointmentId: null,
            now);

    private static Result CheckBookingRules(
        Guid professionalId,
        Guid patientId,
        Professional professional,
        TimeRange range,
        IEnumerable<Appointment> existingAppointments,
        Guid? ignoredAppointmentId,
        DateTime now)
    {
        if (range.Start < now)
        {
            return Result.Failure(AppointmentErrors.InThePast);
        }

        if (range.Start - now < MinLeadTime)
        {
            return Result.Failure(AppointmentErrors.TooSoon);
        }

        if (!professional.IsActive)
        {
            return Result.Failure(ProfessionalErrors.Inactive);
        }

        if (!professional.IsAvailable(range))
        {
            return Result.Failure(AppointmentErrors.OutsideWorkingHours);
        }

        List<Appointment> blocking = existingAppointments
            .Where(a => a.Status == AppointmentStatus.Scheduled
                && a.Id != ignoredAppointmentId
                && a.TimeRange.Overlaps(range))
            .ToList();

        if (blocking.Any(a => a.ProfessionalId == professionalId))
        {
            return Result.Failure(AppointmentErrors.Overlap);
        }

        if (blocking.Any(a => a.PatientId == patientId))
        {
            return Result.Failure(AppointmentErrors.PatientOverlap);
        }

        return Result.Success();
    }
}

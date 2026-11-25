using Citas.Domain.Common;
using Citas.Domain.Errors;
using Citas.Domain.ValueObjects;

namespace Citas.Domain.Appointments;

/// <summary>
/// A booking of one professional and one patient for a time range, in local clinic time.
/// Creation rules that involve other appointments live in <see cref="AppointmentScheduler"/>.
/// </summary>
public sealed class Appointment
{
    private static readonly TimeSpan MinNoticeForPatientCancellation = TimeSpan.FromHours(24);

    private Appointment(Guid professionalId, Guid patientId, TimeRange timeRange, string? reason, DateTime createdAt)
    {
        Id = Guid.NewGuid();
        ProfessionalId = professionalId;
        PatientId = patientId;
        TimeRange = timeRange;
        Reason = reason;
        CreatedAt = createdAt;
        Status = AppointmentStatus.Scheduled;
    }

    public Guid Id { get; }

    public Guid ProfessionalId { get; }

    public Guid PatientId { get; }

    public TimeRange TimeRange { get; private set; }

    public AppointmentStatus Status { get; private set; }

    public string? Reason { get; }

    public string? CancellationReason { get; private set; }

    public DateTime CreatedAt { get; }

    public DateTime? CancelledAt { get; private set; }

    /// <summary>Internal on purpose: the scheduler is the only way to create an appointment that respects the booking rules.</summary>
    internal static Result<Appointment> Create(
        Guid professionalId,
        Guid patientId,
        TimeRange timeRange,
        string? reason,
        DateTime now)
    {
        if (reason is { Length: > 250 })
        {
            return AppointmentErrors.ReasonTooLong;
        }

        return new Appointment(professionalId, patientId, timeRange, reason, now);
    }

    /// <summary>
    /// Cancels a scheduled appointment. A patient needs at least 24 hours of notice (exactly 24 is allowed);
    /// the clinic can cancel any time before the start.
    /// </summary>
    public Result Cancel(string? reason, DateTime now, bool byClinic)
    {
        if (Status != AppointmentStatus.Scheduled)
        {
            return Result.Failure(AppointmentErrors.NotScheduled);
        }

        if (string.IsNullOrWhiteSpace(reason) || reason.Length < 3 || reason.Length > 250)
        {
            return Result.Failure(AppointmentErrors.InvalidCancellationReason);
        }

        if (byClinic)
        {
            if (now >= TimeRange.Start)
            {
                return Result.Failure(AppointmentErrors.AlreadyStarted);
            }
        }
        else if (TimeRange.Start - now < MinNoticeForPatientCancellation)
        {
            return Result.Failure(AppointmentErrors.CancellationTooLate);
        }

        Status = AppointmentStatus.Cancelled;
        CancellationReason = reason;
        CancelledAt = now;
        return Result.Success();
    }

    public Result Complete(DateTime now) => Close(AppointmentStatus.Completed, now);

    public Result MarkNoShow(DateTime now) => Close(AppointmentStatus.NoShow, now);

    /// <summary>Moves the appointment to a new range. The scheduler validates every rule before calling this.</summary>
    internal void MoveTo(TimeRange newRange) => TimeRange = newRange;

    private Result Close(AppointmentStatus finalStatus, DateTime now)
    {
        if (Status != AppointmentStatus.Scheduled)
        {
            return Result.Failure(AppointmentErrors.NotScheduled);
        }

        if (now < TimeRange.Start)
        {
            return Result.Failure(AppointmentErrors.NotStartedYet);
        }

        Status = finalStatus;
        return Result.Success();
    }
}

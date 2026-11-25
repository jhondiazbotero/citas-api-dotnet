using Citas.Domain.Common;

namespace Citas.Domain.Errors;

public static class AppointmentErrors
{
    public static readonly Error InThePast =
        new("Appointment.InThePast", "The appointment starts in the past.");

    public static readonly Error TooSoon =
        new("Appointment.TooSoon", "The appointment must start at least 1 hour from now.");

    public static readonly Error OutsideWorkingHours =
        new("Appointment.OutsideWorkingHours", "The appointment is outside the professional's working hours.");

    public static readonly Error Overlap =
        new("Appointment.Overlap", "The professional already has an appointment at that time.");

    public static readonly Error PatientOverlap =
        new("Appointment.PatientOverlap", "The patient already has an appointment at that time.");

    public static readonly Error ReasonTooLong =
        new("Appointment.ReasonTooLong", "Reason must be at most 250 characters.");

    public static readonly Error NotScheduled =
        new("Appointment.NotScheduled", "Only a scheduled appointment can change state.");

    public static readonly Error InvalidCancellationReason =
        new("Appointment.InvalidCancellationReason", "Cancellation reason must be 3 to 250 characters.");

    public static readonly Error CancellationTooLate =
        new("Appointment.CancellationTooLate", "Patients must cancel at least 24 hours before the start.");

    public static readonly Error RescheduleTooLate =
        new("Appointment.RescheduleTooLate", "Rescheduling requires at least 24 hours before the original start.");

    public static readonly Error AlreadyStarted =
        new("Appointment.AlreadyStarted", "The appointment has already started.");

    public static readonly Error NotStartedYet =
        new("Appointment.NotStartedYet", "The appointment has not started yet.");

    public static readonly Error ProfessionalMismatch =
        new("Appointment.ProfessionalMismatch", "The professional does not own this appointment.");

    public static readonly Error NotFound =
        new("Appointment.NotFound", "The appointment does not exist.");
}

using System.ComponentModel;
using Citas.Application.Common;

namespace Citas.Api.Contracts;

[Description("Data to register a patient.")]
public sealed record RegisterPatientRequest(
    [property: Description("Full name, 2 to 100 characters.")] string FullName,
    [property: Description("Identity document number, 5 to 20 digits. It must be unique.")] string DocumentNumber,
    [property: Description("Optional phone number, 7 to 15 digits.")] string? Phone);

[Description("Data to register a professional with the weekly hours in which they see patients.")]
public sealed record RegisterProfessionalRequest(
    [property: Description("Full name, 2 to 100 characters.")] string FullName,
    [property: Description("Specialty, 2 to 60 characters.")] string Specialty,
    [property: Description("Weekly working windows. At least one; windows of the same day cannot overlap.")]
    IReadOnlyList<WorkingSlotDto> WorkingHours);

[Description("The complete weekly schedule that replaces the current one.")]
public sealed record SetWorkingHoursRequest(
    [property: Description("Weekly working windows. At least one; windows of the same day cannot overlap.")]
    IReadOnlyList<WorkingSlotDto> WorkingHours);

[Description("Data to book an appointment.")]
public sealed record ScheduleAppointmentRequest(
    [property: Description("Id of the professional.")] Guid ProfessionalId,
    [property: Description("Id of the patient.")] Guid PatientId,
    [property: Description("Start in local clinic time, yyyy-MM-ddTHH:mm[:ss], without zone or offset.")] DateTime Start,
    [property: Description("Length in minutes: 15 to 120, in steps of 15.")] int DurationMinutes,
    [property: Description("Optional reason for the visit, up to 250 characters.")] string? Reason);

[Description("The new time for an appointment.")]
public sealed record RescheduleAppointmentRequest(
    [property: Description("New start in local clinic time, yyyy-MM-ddTHH:mm[:ss], without zone or offset.")] DateTime NewStart,
    [property: Description("Length in minutes: 15 to 120, in steps of 15.")] int DurationMinutes);

[Description("Why an appointment is cancelled and who cancels it.")]
public sealed record CancelAppointmentRequest(
    [property: Description("Cancellation reason, 3 to 250 characters.")] string Reason,
    [property: Description("True when the clinic cancels (allowed until the start); false when the patient does (24 hours ahead).")]
    bool ByClinic = false);

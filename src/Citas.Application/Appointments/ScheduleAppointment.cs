using Citas.Application.Abstractions;
using Citas.Application.Common;
using Citas.Domain.Appointments;
using Citas.Domain.Common;
using Citas.Domain.Errors;
using Citas.Domain.Patients;
using Citas.Domain.Professionals;
using Citas.Domain.ValueObjects;
using FluentValidation;

namespace Citas.Application.Appointments;

public sealed record ScheduleAppointmentCommand(
    Guid ProfessionalId,
    Guid PatientId,
    DateTime Start,
    int DurationMinutes,
    string? Reason);

public sealed class ScheduleAppointmentValidator : AbstractValidator<ScheduleAppointmentCommand>
{
    public ScheduleAppointmentValidator()
    {
        RuleFor(c => c.ProfessionalId).NotEmpty().WithMessage("Professional id is required.");
        RuleFor(c => c.PatientId).NotEmpty().WithMessage("Patient id is required.");
        RuleFor(c => c.Start).NotEqual(default(DateTime)).WithMessage("Start is required.");
        RuleFor(c => c.DurationMinutes).GreaterThan(0).WithMessage("Duration is required.");
    }
}

public sealed class ScheduleAppointmentHandler(
    IValidator<ScheduleAppointmentCommand> validator,
    IProfessionalRepository professionals,
    IPatientRepository patients,
    IAppointmentRepository appointments,
    IClinicClock clock,
    IUnitOfWork unitOfWork)
{
    public async Task<Result<AppointmentResponse>> HandleAsync(
        ScheduleAppointmentCommand command,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationErrors.From(validation);
        }

        Result<TimeRange> range = TimeRange.Create(command.Start, TimeSpan.FromMinutes(command.DurationMinutes));
        if (range.IsFailure)
        {
            return range.Error;
        }

        Professional? professional = await professionals.GetByIdAsync(command.ProfessionalId, cancellationToken);
        if (professional is null)
        {
            return ProfessionalErrors.NotFound;
        }

        Patient? patient = await patients.GetByIdAsync(command.PatientId, cancellationToken);
        if (patient is null)
        {
            return PatientErrors.NotFound;
        }

        IReadOnlyList<Appointment> existing = await appointments.GetScheduledOverlappingAsync(
            professional.Id, patient.Id, range.Value, cancellationToken);

        Result<Appointment> scheduled = AppointmentScheduler.Schedule(
            professional, patient, range.Value, command.Reason, existing, clock.Now);
        if (scheduled.IsFailure)
        {
            return scheduled.Error;
        }

        appointments.Add(scheduled.Value);
        Result saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        if (saved.IsFailure)
        {
            return saved.Error;
        }

        return AppointmentResponse.From(scheduled.Value);
    }
}

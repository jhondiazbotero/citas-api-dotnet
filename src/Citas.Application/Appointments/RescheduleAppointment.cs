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

public sealed record RescheduleAppointmentCommand(Guid AppointmentId, DateTime NewStart, int DurationMinutes);

public sealed class RescheduleAppointmentValidator : AbstractValidator<RescheduleAppointmentCommand>
{
    public RescheduleAppointmentValidator()
    {
        RuleFor(c => c.AppointmentId).NotEmpty().WithMessage("Appointment id is required.");
        RuleFor(c => c.NewStart).NotEqual(default(DateTime)).WithMessage("New start is required.");
        RuleFor(c => c.DurationMinutes).GreaterThan(0).WithMessage("Duration is required.");
    }
}

public sealed class RescheduleAppointmentHandler(
    IValidator<RescheduleAppointmentCommand> validator,
    IProfessionalRepository professionals,
    IAppointmentRepository appointments,
    IClinicClock clock,
    IUnitOfWork unitOfWork)
{
    public async Task<Result<AppointmentResponse>> HandleAsync(
        RescheduleAppointmentCommand command,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationErrors.From(validation);
        }

        Result<TimeRange> range = TimeRange.Create(command.NewStart, TimeSpan.FromMinutes(command.DurationMinutes));
        if (range.IsFailure)
        {
            return range.Error;
        }

        Appointment? appointment = await appointments.GetByIdAsync(command.AppointmentId, cancellationToken);
        if (appointment is null)
        {
            return AppointmentErrors.NotFound;
        }

        Professional? professional = await professionals.GetByIdAsync(appointment.ProfessionalId, cancellationToken);
        if (professional is null)
        {
            return ProfessionalErrors.NotFound;
        }

        IReadOnlyList<Appointment> existing = await appointments.GetScheduledOverlappingAsync(
            appointment.ProfessionalId, appointment.PatientId, range.Value, cancellationToken);

        Result moved = AppointmentScheduler.Reschedule(appointment, professional, range.Value, existing, clock.Now);
        if (moved.IsFailure)
        {
            return moved.Error;
        }

        Result saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        if (saved.IsFailure)
        {
            return saved.Error;
        }

        return AppointmentResponse.From(appointment);
    }
}

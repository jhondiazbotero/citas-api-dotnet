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

public sealed record CompleteAppointmentCommand(Guid AppointmentId);

public sealed class CompleteAppointmentValidator : AbstractValidator<CompleteAppointmentCommand>
{
    public CompleteAppointmentValidator()
    {
        RuleFor(c => c.AppointmentId).NotEmpty().WithMessage("Appointment id is required.");
    }
}

/// <summary>Marks a scheduled appointment as attended. The domain requires that it has already started.</summary>
public sealed class CompleteAppointmentHandler(
    IValidator<CompleteAppointmentCommand> validator,
    IAppointmentRepository appointments,
    IClinicClock clock,
    IUnitOfWork unitOfWork)
{
    public async Task<Result> HandleAsync(CompleteAppointmentCommand command, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure(ValidationErrors.From(validation));
        }

        Appointment? appointment = await appointments.GetByIdAsync(command.AppointmentId, cancellationToken);
        if (appointment is null)
        {
            return Result.Failure(AppointmentErrors.NotFound);
        }

        Result changed = appointment.Complete(clock.Now);
        if (changed.IsFailure)
        {
            return changed;
        }

        return await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

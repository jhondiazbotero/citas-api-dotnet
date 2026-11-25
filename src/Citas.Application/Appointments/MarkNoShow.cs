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

public sealed record MarkNoShowCommand(Guid AppointmentId);

public sealed class MarkNoShowValidator : AbstractValidator<MarkNoShowCommand>
{
    public MarkNoShowValidator()
    {
        RuleFor(c => c.AppointmentId).NotEmpty().WithMessage("Appointment id is required.");
    }
}

/// <summary>Marks a scheduled appointment as missed by the patient. The domain requires that it has already started.</summary>
public sealed class MarkNoShowHandler(
    IValidator<MarkNoShowCommand> validator,
    IAppointmentRepository appointments,
    IClinicClock clock,
    IUnitOfWork unitOfWork)
{
    public async Task<Result> HandleAsync(MarkNoShowCommand command, CancellationToken cancellationToken)
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

        Result changed = appointment.MarkNoShow(clock.Now);
        if (changed.IsFailure)
        {
            return changed;
        }

        return await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

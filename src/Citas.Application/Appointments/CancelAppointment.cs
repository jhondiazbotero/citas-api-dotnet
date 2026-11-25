using Citas.Application.Abstractions;
using Citas.Application.Common;
using Citas.Domain.Appointments;
using Citas.Domain.Common;
using Citas.Domain.Errors;
using FluentValidation;

namespace Citas.Application.Appointments;

/// <summary><c>ByClinic</c> comes from the caller (the API sets it from <c>onBehalfOfPatient</c>): the clinic can cancel up to the start, a patient needs 24 hours.</summary>
public sealed record CancelAppointmentCommand(Guid AppointmentId, string Reason, bool ByClinic);

public sealed class CancelAppointmentValidator : AbstractValidator<CancelAppointmentCommand>
{
    public CancelAppointmentValidator()
    {
        RuleFor(c => c.AppointmentId).NotEmpty().WithMessage("Appointment id is required.");
        RuleFor(c => c.Reason).NotNull().WithMessage("Cancellation reason is required.");
    }
}

public sealed class CancelAppointmentHandler(
    IValidator<CancelAppointmentCommand> validator,
    IAppointmentRepository appointments,
    IClinicClock clock,
    IUnitOfWork unitOfWork)
{
    public async Task<Result> HandleAsync(CancelAppointmentCommand command, CancellationToken cancellationToken)
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

        Result cancelled = appointment.Cancel(command.Reason, clock.Now, command.ByClinic);
        if (cancelled.IsFailure)
        {
            return cancelled;
        }

        return await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

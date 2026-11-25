using Citas.Application.Abstractions;
using Citas.Application.Common;
using Citas.Domain.Appointments;
using Citas.Domain.Common;
using Citas.Domain.Errors;
using Citas.Domain.Professionals;
using Citas.Domain.ValueObjects;
using FluentValidation;

namespace Citas.Application.Appointments;

public sealed record GetAppointmentQuery(Guid AppointmentId);

public sealed class GetAppointmentValidator : AbstractValidator<GetAppointmentQuery>
{
    public GetAppointmentValidator()
    {
        RuleFor(q => q.AppointmentId).NotEmpty().WithMessage("Appointment id is required.");
    }
}

public sealed class GetAppointmentHandler(IValidator<GetAppointmentQuery> validator, IAppointmentRepository appointments)
{
    public async Task<Result<AppointmentResponse>> HandleAsync(GetAppointmentQuery query, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationErrors.From(validation);
        }

        Appointment? appointment = await appointments.GetByIdAsync(query.AppointmentId, cancellationToken);
        if (appointment is null)
        {
            return AppointmentErrors.NotFound;
        }

        return AppointmentResponse.From(appointment);
    }
}

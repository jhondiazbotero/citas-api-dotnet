using Citas.Application.Abstractions;
using Citas.Application.Common;
using Citas.Domain.Appointments;
using Citas.Domain.Common;
using Citas.Domain.Errors;
using Citas.Domain.Professionals;
using FluentValidation;

namespace Citas.Application.Appointments;

/// <summary>Appointments of a professional whose start date is between <c>From</c> and <c>To</c>, both included. At most 31 days.</summary>
public sealed record GetProfessionalAgendaQuery(Guid ProfessionalId, DateOnly From, DateOnly To);

public sealed class GetProfessionalAgendaValidator : AbstractValidator<GetProfessionalAgendaQuery>
{
    public const int MaxDays = 31;

    public GetProfessionalAgendaValidator()
    {
        RuleFor(q => q.ProfessionalId).NotEmpty().WithMessage("Professional id is required.");
        RuleFor(q => q.To).GreaterThanOrEqualTo(q => q.From).WithMessage("The end date cannot be before the start date.");
        RuleFor(q => q).Must(q => q.To.DayNumber - q.From.DayNumber < MaxDays)
            .WithMessage($"The agenda covers at most {MaxDays} days.")
            .WithName("Range");
    }
}

public sealed class GetProfessionalAgendaHandler(
    IValidator<GetProfessionalAgendaQuery> validator,
    IProfessionalRepository professionals,
    IAppointmentRepository appointments)
{
    public async Task<Result<IReadOnlyList<AppointmentResponse>>> HandleAsync(
        GetProfessionalAgendaQuery query,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationErrors.From(validation);
        }

        Professional? professional = await professionals.GetByIdAsync(query.ProfessionalId, cancellationToken);
        if (professional is null)
        {
            return ProfessionalErrors.NotFound;
        }

        IReadOnlyList<Appointment> found = await appointments.GetByProfessionalAsync(
            professional.Id, query.From, query.To, cancellationToken);

        IReadOnlyList<AppointmentResponse> agenda =
            [.. found.OrderBy(a => a.TimeRange.Start).Select(AppointmentResponse.From)];
        return Result.Success(agenda);
    }
}

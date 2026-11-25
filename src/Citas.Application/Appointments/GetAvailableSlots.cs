using Citas.Application.Abstractions;
using Citas.Application.Common;
using Citas.Domain.Appointments;
using Citas.Domain.Common;
using Citas.Domain.Errors;
using Citas.Domain.Professionals;
using Citas.Domain.ValueObjects;
using FluentValidation;

namespace Citas.Application.Appointments;

/// <summary>Free start times on one date for an appointment of the given length.</summary>
public sealed record GetAvailableSlotsQuery(Guid ProfessionalId, DateOnly Date, int DurationMinutes);

public sealed class GetAvailableSlotsValidator : AbstractValidator<GetAvailableSlotsQuery>
{
    public GetAvailableSlotsValidator()
    {
        RuleFor(q => q.ProfessionalId).NotEmpty().WithMessage("Professional id is required.");
        RuleFor(q => q.Date).NotEqual(default(DateOnly)).WithMessage("Date is required.");
        RuleFor(q => q.Date).LessThan(DateOnly.MaxValue).WithMessage("Date is out of range.");
        RuleFor(q => q.DurationMinutes).GreaterThan(0).WithMessage("Duration is required.");
    }
}

/// <summary>
/// Tries every quarter hour of the day and keeps the starts the domain accepts (<see cref="TimeRange.Create"/> and
/// <see cref="AppointmentScheduler.CheckSlot"/>), so the rules live in one place. An inactive professional has no slots.
/// </summary>
public sealed class GetAvailableSlotsHandler(
    IValidator<GetAvailableSlotsQuery> validator,
    IProfessionalRepository professionals,
    IAppointmentRepository appointments,
    IClinicClock clock)
{
    private const int StepMinutes = 15;
    private const int StepsPerDay = 24 * 60 / StepMinutes;

    public async Task<Result<IReadOnlyList<SlotResponse>>> HandleAsync(
        GetAvailableSlotsQuery query,
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

        TimeSpan duration = TimeSpan.FromMinutes(query.DurationMinutes);
        DateTime midnight = query.Date.ToDateTime(TimeOnly.MinValue);
        Result<TimeRange> probe = TimeRange.Create(midnight, duration);
        if (probe.IsFailure)
        {
            return probe.Error;
        }

        IReadOnlyList<Appointment> booked = await appointments.GetByProfessionalAsync(
            professional.Id, query.Date, query.Date, cancellationToken);
        DateTime now = clock.Now;

        List<SlotResponse> slots = [];
        for (int step = 0; step < StepsPerDay; step++)
        {
            Result<TimeRange> range = TimeRange.Create(midnight.AddMinutes(step * StepMinutes), duration);
            if (range.IsSuccess && AppointmentScheduler.CheckSlot(professional, range.Value, booked, now).IsSuccess)
            {
                slots.Add(new SlotResponse(range.Value.Start, range.Value.End));
            }
        }

        IReadOnlyList<SlotResponse> result = slots;
        return Result.Success(result);
    }
}

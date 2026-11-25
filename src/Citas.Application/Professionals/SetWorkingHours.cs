using Citas.Application.Abstractions;
using Citas.Application.Common;
using Citas.Domain.Common;
using Citas.Domain.Errors;
using Citas.Domain.Professionals;
using FluentValidation;

namespace Citas.Application.Professionals;

public sealed record SetWorkingHoursCommand(Guid ProfessionalId, IReadOnlyList<WorkingSlotDto> WorkingHours);

public sealed class SetWorkingHoursValidator : AbstractValidator<SetWorkingHoursCommand>
{
    public SetWorkingHoursValidator()
    {
        RuleFor(c => c.ProfessionalId).NotEmpty().WithMessage("Professional id is required.");
        RuleFor(c => c.WorkingHours).NotNull().WithMessage("Working hours are required.");
        RuleForEach(c => c.WorkingHours).Must(s => s is not null && Enum.IsDefined(s.Day))
            .WithMessage("Each working slot needs a valid day of the week.");
    }
}

public sealed class SetWorkingHoursHandler(
    IValidator<SetWorkingHoursCommand> validator,
    IProfessionalRepository professionals,
    IUnitOfWork unitOfWork)
{
    public async Task<Result<ProfessionalResponse>> HandleAsync(
        SetWorkingHoursCommand command,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationErrors.From(validation);
        }

        Professional? professional = await professionals.GetByIdAsync(command.ProfessionalId, cancellationToken);
        if (professional is null)
        {
            return ProfessionalErrors.NotFound;
        }

        var slots = WorkingSlotMapper.ToDomain(command.WorkingHours);
        if (slots.IsFailure)
        {
            return slots.Error;
        }

        Result hours = professional.SetWorkingHours(slots.Value);
        if (hours.IsFailure)
        {
            return hours.Error;
        }

        Result saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        if (saved.IsFailure)
        {
            return saved.Error;
        }

        return ProfessionalResponse.From(professional);
    }
}

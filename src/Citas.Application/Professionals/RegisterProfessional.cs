using Citas.Application.Abstractions;
using Citas.Application.Common;
using Citas.Domain.Common;
using Citas.Domain.Professionals;
using FluentValidation;

namespace Citas.Application.Professionals;

public sealed record RegisterProfessionalCommand(string FullName, string Specialty, IReadOnlyList<WorkingSlotDto> WorkingHours);

public sealed class RegisterProfessionalValidator : AbstractValidator<RegisterProfessionalCommand>
{
    public RegisterProfessionalValidator()
    {
        RuleFor(c => c.FullName).NotNull().WithMessage("Full name is required.");
        RuleFor(c => c.Specialty).NotNull().WithMessage("Specialty is required.");
        RuleFor(c => c.WorkingHours).NotNull().WithMessage("Working hours are required.");
        RuleForEach(c => c.WorkingHours).Must(s => s is not null && Enum.IsDefined(s.Day))
            .WithMessage("Each working slot needs a valid day of the week.");
    }
}

public sealed class RegisterProfessionalHandler(
    IValidator<RegisterProfessionalCommand> validator,
    IProfessionalRepository professionals,
    IUnitOfWork unitOfWork)
{
    public async Task<Result<ProfessionalResponse>> HandleAsync(
        RegisterProfessionalCommand command,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationErrors.From(validation);
        }

        Result<Professional> created = Professional.Create(command.FullName, command.Specialty);
        if (created.IsFailure)
        {
            return created.Error;
        }

        var slots = WorkingSlotMapper.ToDomain(command.WorkingHours);
        if (slots.IsFailure)
        {
            return slots.Error;
        }

        Professional professional = created.Value;
        Result hours = professional.SetWorkingHours(slots.Value);
        if (hours.IsFailure)
        {
            return hours.Error;
        }

        professionals.Add(professional);
        Result saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        if (saved.IsFailure)
        {
            return saved.Error;
        }

        return ProfessionalResponse.From(professional);
    }
}

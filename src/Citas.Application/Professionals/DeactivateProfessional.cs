using Citas.Application.Abstractions;
using Citas.Application.Common;
using Citas.Domain.Common;
using Citas.Domain.Errors;
using Citas.Domain.Professionals;
using FluentValidation;

namespace Citas.Application.Professionals;

public sealed record DeactivateProfessionalCommand(Guid ProfessionalId);

public sealed class DeactivateProfessionalValidator : AbstractValidator<DeactivateProfessionalCommand>
{
    public DeactivateProfessionalValidator()
    {
        RuleFor(c => c.ProfessionalId).NotEmpty().WithMessage("Professional id is required.");
    }
}

/// <summary>Deactivating is idempotent. Appointments already scheduled stay as they are.</summary>
public sealed class DeactivateProfessionalHandler(
    IValidator<DeactivateProfessionalCommand> validator,
    IProfessionalRepository professionals,
    IUnitOfWork unitOfWork)
{
    public async Task<Result> HandleAsync(DeactivateProfessionalCommand command, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure(ValidationErrors.From(validation));
        }

        Professional? professional = await professionals.GetByIdAsync(command.ProfessionalId, cancellationToken);
        if (professional is null)
        {
            return Result.Failure(ProfessionalErrors.NotFound);
        }

        professional.Deactivate();
        return await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

using Citas.Application.Abstractions;
using Citas.Application.Common;
using Citas.Domain.Common;
using Citas.Domain.Errors;
using Citas.Domain.Professionals;
using FluentValidation;

namespace Citas.Application.Professionals;

public sealed record GetProfessionalQuery(Guid ProfessionalId);

public sealed class GetProfessionalValidator : AbstractValidator<GetProfessionalQuery>
{
    public GetProfessionalValidator()
    {
        RuleFor(q => q.ProfessionalId).NotEmpty().WithMessage("Professional id is required.");
    }
}

public sealed class GetProfessionalHandler(IValidator<GetProfessionalQuery> validator, IProfessionalRepository professionals)
{
    public async Task<Result<ProfessionalResponse>> HandleAsync(GetProfessionalQuery query, CancellationToken cancellationToken)
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

        return ProfessionalResponse.From(professional);
    }
}

using Citas.Application.Abstractions;
using Citas.Application.Common;
using Citas.Domain.Common;
using Citas.Domain.Errors;
using Citas.Domain.Patients;
using FluentValidation;

namespace Citas.Application.Patients;

public sealed record GetPatientQuery(Guid PatientId);

public sealed class GetPatientValidator : AbstractValidator<GetPatientQuery>
{
    public GetPatientValidator()
    {
        RuleFor(q => q.PatientId).NotEmpty().WithMessage("Patient id is required.");
    }
}

public sealed class GetPatientHandler(IValidator<GetPatientQuery> validator, IPatientRepository patients)
{
    public async Task<Result<PatientResponse>> HandleAsync(GetPatientQuery query, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationErrors.From(validation);
        }

        Patient? patient = await patients.GetByIdAsync(query.PatientId, cancellationToken);
        if (patient is null)
        {
            return PatientErrors.NotFound;
        }

        return PatientResponse.From(patient);
    }
}

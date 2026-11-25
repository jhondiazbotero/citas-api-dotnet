using Citas.Application.Abstractions;
using Citas.Application.Common;
using Citas.Domain.Common;
using Citas.Domain.Errors;
using Citas.Domain.Patients;
using FluentValidation;

namespace Citas.Application.Patients;

public sealed record RegisterPatientCommand(string FullName, string DocumentNumber, string? Phone);

public sealed class RegisterPatientValidator : AbstractValidator<RegisterPatientCommand>
{
    public RegisterPatientValidator()
    {
        RuleFor(c => c.FullName).NotNull().WithMessage("Full name is required.");
        RuleFor(c => c.DocumentNumber).NotNull().WithMessage("Document number is required.");
    }
}

public sealed class RegisterPatientHandler(
    IValidator<RegisterPatientCommand> validator,
    IPatientRepository patients,
    IUnitOfWork unitOfWork)
{
    public async Task<Result<PatientResponse>> HandleAsync(RegisterPatientCommand command, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationErrors.From(validation);
        }

        Result<Patient> created = Patient.Create(command.FullName, command.DocumentNumber, command.Phone);
        if (created.IsFailure)
        {
            return created.Error;
        }

        if (await patients.ExistsByDocumentAsync(command.DocumentNumber, cancellationToken))
        {
            return PatientErrors.DuplicateDocument;
        }

        patients.Add(created.Value);
        Result saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        if (saved.IsFailure)
        {
            return saved.Error;
        }

        return PatientResponse.From(created.Value);
    }
}

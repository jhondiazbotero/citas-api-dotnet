using Citas.Domain.Common;
using Citas.Domain.Errors;

namespace Citas.Domain.Patients;

public sealed class Patient
{
    private Patient(string fullName, string documentNumber, string? phone)
    {
        Id = Guid.NewGuid();
        FullName = fullName;
        DocumentNumber = documentNumber;
        Phone = phone;
    }

    public Guid Id { get; }

    public string FullName { get; }

    public string DocumentNumber { get; }

    public string? Phone { get; }

    public static Result<Patient> Create(string fullName, string documentNumber, string? phone = null)
    {
        if (!TextRules.IsTrimmedWithin(fullName, 2, 100))
        {
            return PatientErrors.InvalidName;
        }

        if (!TextRules.IsDigitsWithin(documentNumber, 5, 20))
        {
            return PatientErrors.InvalidDocument;
        }

        if (phone is not null && !TextRules.IsDigitsWithin(phone, 7, 15))
        {
            return PatientErrors.InvalidPhone;
        }

        return new Patient(fullName, documentNumber, phone);
    }
}

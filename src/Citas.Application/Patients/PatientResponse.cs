using Citas.Domain.Patients;

namespace Citas.Application.Patients;

public sealed record PatientResponse(Guid Id, string FullName, string DocumentNumber, string? Phone)
{
    internal static PatientResponse From(Patient patient) =>
        new(patient.Id, patient.FullName, patient.DocumentNumber, patient.Phone);
}

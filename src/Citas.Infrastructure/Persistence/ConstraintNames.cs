namespace Citas.Infrastructure.Persistence;

/// <summary>Names of the database constraints that the code needs to recognize when PostgreSQL rejects a write.</summary>
public static class ConstraintNames
{
    public const string PatientDocumentUnique = "ix_patients_document_number";

    public const string ProfessionalOverlap = "ex_appointments_professional_overlap";

    public const string PatientOverlap = "ex_appointments_patient_overlap";
}

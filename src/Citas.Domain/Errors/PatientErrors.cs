using Citas.Domain.Common;

namespace Citas.Domain.Errors;

public static class PatientErrors
{
    public static readonly Error InvalidName =
        new("Patient.InvalidName", "Full name must be 2 to 100 characters with no surrounding spaces.");

    public static readonly Error InvalidDocument =
        new("Patient.InvalidDocument", "Document number must be 5 to 20 digits.");

    public static readonly Error InvalidPhone =
        new("Patient.InvalidPhone", "Phone must be 7 to 15 digits.");

    public static readonly Error DuplicateDocument =
        new("Patient.DuplicateDocument", "A patient with this document number already exists.");

    public static readonly Error NotFound =
        new("Patient.NotFound", "The patient does not exist.");
}

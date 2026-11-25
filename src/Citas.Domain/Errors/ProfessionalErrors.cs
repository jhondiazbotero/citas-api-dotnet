using Citas.Domain.Common;

namespace Citas.Domain.Errors;

public static class ProfessionalErrors
{
    public static readonly Error InvalidName =
        new("Professional.InvalidName", "Full name must be 2 to 100 characters with no surrounding spaces.");

    public static readonly Error InvalidSpecialty =
        new("Professional.InvalidSpecialty", "Specialty must be 2 to 60 characters.");

    public static readonly Error InvalidSlot =
        new("Professional.InvalidSlot", "A working slot must end after it starts.");

    public static readonly Error NoWorkingHours =
        new("Professional.NoWorkingHours", "At least one working slot is required.");

    public static readonly Error OverlappingWorkingHours =
        new("Professional.OverlappingWorkingHours", "Working slots on the same day cannot overlap.");

    public static readonly Error Inactive =
        new("Professional.Inactive", "The professional is not active.");
}

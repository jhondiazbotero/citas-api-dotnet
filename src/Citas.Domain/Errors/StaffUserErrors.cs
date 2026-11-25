using Citas.Domain.Common;

namespace Citas.Domain.Errors;

public static class StaffUserErrors
{
    public static readonly Error InvalidEmail =
        new("StaffUser.InvalidEmail", "Email must be a valid address of at most 254 characters.");

    public static readonly Error InvalidRole =
        new("StaffUser.InvalidRole", "Role must be Admin, Reception or Professional.");

    public static readonly Error ProfessionalRequired =
        new("StaffUser.ProfessionalRequired", "A user with the Professional role must be linked to a professional.");

    public static readonly Error ProfessionalNotAllowed =
        new("StaffUser.ProfessionalNotAllowed", "Only a user with the Professional role can be linked to a professional.");

    public static readonly Error DuplicateEmail =
        new("StaffUser.DuplicateEmail", "A user with this email already exists.");

    public static readonly Error InvalidPasswordHash =
        new("StaffUser.InvalidPasswordHash", "A password hash is required.");
}

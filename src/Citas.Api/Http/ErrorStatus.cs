using Citas.Domain.Common;
using Citas.Domain.Errors;

namespace Citas.Api.Http;

/// <summary>Decides which HTTP status a domain error becomes.</summary>
internal static class ErrorStatus
{
    private const string ValidationCode = "Validation";
    private const string TimeRangePrefix = "TimeRange.";
    private const string NotFoundSuffix = ".NotFound";

    // The input is malformed: wrong shape, length or format. The caller can fix it without knowing anything about the clinic's state.
    private static readonly HashSet<string> BadInput =
    [
        PatientErrors.InvalidName.Code,
        PatientErrors.InvalidDocument.Code,
        PatientErrors.InvalidPhone.Code,
        ProfessionalErrors.InvalidName.Code,
        ProfessionalErrors.InvalidSpecialty.Code,
        ProfessionalErrors.InvalidSlot.Code,
        AppointmentErrors.ReasonTooLong.Code,
        AppointmentErrors.InvalidCancellationReason.Code,
        StaffUserErrors.InvalidEmail.Code,
        StaffUserErrors.InvalidRole.Code,
        StaffUserErrors.RoleNotAllowed.Code,
        StaffUserErrors.ProfessionalRequired.Code,
        StaffUserErrors.ProfessionalNotAllowed.Code,
        StaffUserErrors.InvalidPasswordHash.Code,
    ];

    // The request is well formed but clashes with something that already exists or changed in the meantime.
    private static readonly HashSet<string> Conflicts =
    [
        AppointmentErrors.Overlap.Code,
        AppointmentErrors.PatientOverlap.Code,
        PatientErrors.DuplicateDocument.Code,
        AppointmentErrors.ConcurrencyConflict.Code,
        StaffUserErrors.DuplicateEmail.Code,
    ];

    // Who you are and what you may do. 401: the caller did not prove who they are. 403: they did, but may not.
    private static readonly Dictionary<string, int> Auth = new()
    {
        [AuthErrors.InvalidCredentials.Code] = StatusCodes.Status401Unauthorized,
        [AuthErrors.Unauthorized.Code] = StatusCodes.Status401Unauthorized,
        [AuthErrors.Forbidden.Code] = StatusCodes.Status403Forbidden,
        [AuthErrors.TooManyAttempts.Code] = StatusCodes.Status429TooManyRequests,
    };

    /// <summary>401, 403 and 429 for the sign-in and access errors, 404 for missing things, 400 for malformed input, 409 for clashes, 422 for any other business rule.</summary>
    public static int For(Error error)
    {
        string code = error.Code;

        if (Auth.TryGetValue(code, out int authStatus))
        {
            return authStatus;
        }

        if (code == ValidationCode
            || code.StartsWith(TimeRangePrefix, StringComparison.Ordinal)
            || BadInput.Contains(code))
        {
            return StatusCodes.Status400BadRequest;
        }

        if (code.EndsWith(NotFoundSuffix, StringComparison.Ordinal))
        {
            return StatusCodes.Status404NotFound;
        }

        return Conflicts.Contains(code)
            ? StatusCodes.Status409Conflict
            : StatusCodes.Status422UnprocessableEntity;
    }
}

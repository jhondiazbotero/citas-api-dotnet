using System.Reflection;
using Citas.Api.Http;
using Citas.Domain.Common;
using Citas.Domain.Errors;

namespace Citas.Api.Tests.Http;

public class ErrorStatusTests
{
    private static readonly Type[] Catalogs =
    [
        typeof(AppointmentErrors),
        typeof(ProfessionalErrors),
        typeof(PatientErrors),
        typeof(TimeRangeErrors),
        typeof(StaffUserErrors),
        typeof(AuthErrors),
    ];

    // Every domain error, written down on purpose: adding an error to the domain makes the catalog test below fail
    // until somebody decides which HTTP status it deserves.
    private static readonly Dictionary<string, int> Expected = new()
    {
        ["Validation"] = 400,

        ["TimeRange.InvalidDuration"] = 400,
        ["TimeRange.InvalidStart"] = 400,
        ["TimeRange.CrossesMidnight"] = 400,
        ["TimeRange.OutOfRange"] = 400,

        ["Patient.InvalidName"] = 400,
        ["Patient.InvalidDocument"] = 400,
        ["Patient.InvalidPhone"] = 400,
        ["Professional.InvalidName"] = 400,
        ["Professional.InvalidSpecialty"] = 400,
        ["Professional.InvalidSlot"] = 400,
        ["Appointment.ReasonTooLong"] = 400,
        ["Appointment.InvalidCancellationReason"] = 400,
        ["StaffUser.InvalidEmail"] = 400,
        ["StaffUser.InvalidRole"] = 400,
        ["StaffUser.ProfessionalRequired"] = 400,
        ["StaffUser.ProfessionalNotAllowed"] = 400,
        ["StaffUser.InvalidPasswordHash"] = 400,

        ["Auth.InvalidCredentials"] = 401,
        ["Auth.Unauthorized"] = 401,
        ["Auth.Forbidden"] = 403,
        ["Auth.TooManyAttempts"] = 429,

        ["Patient.NotFound"] = 404,
        ["Professional.NotFound"] = 404,
        ["Appointment.NotFound"] = 404,

        ["Appointment.Overlap"] = 409,
        ["Appointment.PatientOverlap"] = 409,
        ["Patient.DuplicateDocument"] = 409,
        ["Appointment.ConcurrencyConflict"] = 409,
        ["StaffUser.DuplicateEmail"] = 409,

        ["Appointment.InThePast"] = 422,
        ["Appointment.TooSoon"] = 422,
        ["Appointment.OutsideWorkingHours"] = 422,
        ["Appointment.NotScheduled"] = 422,
        ["Appointment.CancellationTooLate"] = 422,
        ["Appointment.RescheduleTooLate"] = 422,
        ["Appointment.AlreadyStarted"] = 422,
        ["Appointment.NotStartedYet"] = 422,
        ["Appointment.ProfessionalMismatch"] = 422,
        ["Professional.NoWorkingHours"] = 422,
        ["Professional.OverlappingWorkingHours"] = 422,
        ["Professional.Inactive"] = 422,
    };

    private static IEnumerable<Error> CatalogErrors() =>
        Catalogs.SelectMany(c => c
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(Error))
            .Select(f => (Error)f.GetValue(null)!));

    public static TheoryData<string, int> Table()
    {
        var data = new TheoryData<string, int>();
        foreach ((string code, int status) in Expected)
        {
            data.Add(code, status);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Table))]
    public void For_MapsEachCodeToItsStatus(string code, int expected)
    {
        Assert.Equal(expected, ErrorStatus.For(new Error(code, "message")));
    }

    [Fact]
    public void Table_CoversEveryErrorOfTheDomain()
    {
        string[] missing = [.. CatalogErrors().Select(e => e.Code).Where(c => !Expected.ContainsKey(c))];

        Assert.Empty(missing);
    }

    [Fact]
    public void Table_HasNoCodeThatDoesNotExistInTheDomain()
    {
        HashSet<string> real = [.. CatalogErrors().Select(e => e.Code), "Validation"];

        string[] unknown = [.. Expected.Keys.Where(c => !real.Contains(c))];

        Assert.Empty(unknown);
    }

    [Fact]
    public void For_UnknownNotFoundCodeIs404_AndUnknownRuleIs422()
    {
        Assert.Equal(404, ErrorStatus.For(new Error("Thing.NotFound", "x")));
        Assert.Equal(422, ErrorStatus.For(new Error("Something.Else", "x")));
    }
}

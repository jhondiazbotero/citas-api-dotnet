using System.Reflection;
using Citas.Domain.Common;
using Citas.Domain.Errors;

namespace Citas.Domain.Tests.Errors;

public class ErrorCatalogTests
{
    private static readonly Type[] Catalogs =
    [
        typeof(AppointmentErrors),
        typeof(ProfessionalErrors),
        typeof(PatientErrors),
        typeof(TimeRangeErrors),
    ];

    private static IEnumerable<(Type Catalog, Error Error)> AllErrors() =>
        Catalogs.SelectMany(c => c
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(Error))
            .Select(f => (c, (Error)f.GetValue(null)!)));

    [Fact]
    public void Codes_AcrossAllCatalogs_AreUnique()
    {
        var codes = AllErrors().Select(e => e.Error.Code).ToList();

        Assert.Equal(codes.Count, codes.Distinct().Count());
    }

    [Fact]
    public void Codes_InEachCatalog_StartWithTheCatalogPrefix()
    {
        foreach ((Type catalog, Error error) in AllErrors())
        {
            string prefix = catalog.Name.Replace("Errors", string.Empty, StringComparison.Ordinal) + ".";
            Assert.StartsWith(prefix, error.Code, StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(error.Message));
        }
    }

    [Fact]
    public void NotFoundAndDuplicateErrors_HaveTheExpectedCodes()
    {
        Assert.Equal("Professional.NotFound", ProfessionalErrors.NotFound.Code);
        Assert.Equal("Patient.NotFound", PatientErrors.NotFound.Code);
        Assert.Equal("Appointment.NotFound", AppointmentErrors.NotFound.Code);
        Assert.Equal("Patient.DuplicateDocument", PatientErrors.DuplicateDocument.Code);
    }
}

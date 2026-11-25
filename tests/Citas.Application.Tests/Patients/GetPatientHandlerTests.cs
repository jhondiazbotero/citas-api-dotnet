using Citas.Application.Patients;
using Citas.Application.Tests.Fakes;
using Citas.Domain.Errors;

namespace Citas.Application.Tests.Patients;

public class GetPatientHandlerTests
{
    private readonly FakePatientRepository _patients = new();

    private GetPatientHandler CreateHandler() => new(new GetPatientValidator(), _patients);

    [Fact]
    public async Task HandleAsync_ExistingPatient_ReturnsResponse()
    {
        var patient = TestData.NewPatient();
        _patients.Items.Add(patient);

        var result = await CreateHandler().HandleAsync(new GetPatientQuery(patient.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new PatientResponse(patient.Id, "Ana Gomez", "1234567", null), result.Value);
    }

    [Fact]
    public async Task HandleAsync_UnknownPatient_FailsWithNotFound()
    {
        var result = await CreateHandler().HandleAsync(new GetPatientQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(PatientErrors.NotFound, result.Error);
    }

    [Fact]
    public async Task HandleAsync_EmptyId_FailsWithValidationError()
    {
        var result = await CreateHandler().HandleAsync(new GetPatientQuery(Guid.Empty), CancellationToken.None);

        Assert.Equal("Validation", result.Error.Code);
    }
}

using Citas.Application.Appointments;
using Citas.Application.Tests.Fakes;
using Citas.Domain.Errors;

namespace Citas.Application.Tests.Appointments;

public class GetAppointmentHandlerTests
{
    private readonly FakeAppointmentRepository _appointments = new();

    private GetAppointmentHandler CreateHandler() => new(new GetAppointmentValidator(), _appointments);

    [Fact]
    public async Task HandleAsync_ExistingAppointment_ReturnsResponse()
    {
        var professional = TestData.NewProfessional();
        var patient = TestData.NewPatient();
        var appointment = TestData.Book(professional, patient, TestData.At(2, 10, 0));
        _appointments.Items.Add(appointment);

        var result = await CreateHandler().HandleAsync(new GetAppointmentQuery(appointment.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(appointment.Id, result.Value.Id);
        Assert.Equal(professional.Id, result.Value.ProfessionalId);
        Assert.Equal(patient.Id, result.Value.PatientId);
        Assert.Equal(TestData.At(2, 10, 0), result.Value.Start);
        Assert.Equal("Scheduled", result.Value.Status);
    }

    [Fact]
    public async Task HandleAsync_UnknownAppointment_FailsWithNotFound()
    {
        var result = await CreateHandler().HandleAsync(new GetAppointmentQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(AppointmentErrors.NotFound, result.Error);
    }

    [Fact]
    public async Task HandleAsync_EmptyId_FailsWithValidationError()
    {
        var result = await CreateHandler().HandleAsync(new GetAppointmentQuery(Guid.Empty), CancellationToken.None);

        Assert.Equal("Validation", result.Error.Code);
    }
}

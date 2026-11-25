using Citas.Application.Appointments;
using Citas.Application.Tests.Fakes;
using Citas.Domain.Appointments;
using Citas.Domain.Errors;

namespace Citas.Application.Tests.Appointments;

public class CompleteAppointmentHandlerTests
{
    private readonly FakeAppointmentRepository _appointments = new();
    private readonly FakeClinicClock _clock = new(TestData.At(2, 10, 30));
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly Appointment _appointment;

    public CompleteAppointmentHandlerTests()
    {
        _appointment = TestData.Book(TestData.NewProfessional(), TestData.NewPatient(), TestData.At(2, 10, 0));
        _appointments.Items.Add(_appointment);
    }

    private CompleteAppointmentHandler CreateHandler() =>
        new(new CompleteAppointmentValidator(), _appointments, _clock, _unitOfWork);

    [Fact]
    public async Task HandleAsync_AppointmentAlreadyStarted_ChangesStatusAndSaves()
    {
        var result = await CreateHandler().HandleAsync(new CompleteAppointmentCommand(_appointment.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(AppointmentStatus.Completed, _appointment.Status);
        Assert.Equal(1, _unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task HandleAsync_AppointmentNotStartedYet_PropagatesNotStartedYetWithoutSaving()
    {
        _clock.Now = TestData.At(2, 9, 0);

        var result = await CreateHandler().HandleAsync(new CompleteAppointmentCommand(_appointment.Id), CancellationToken.None);

        Assert.Equal(AppointmentErrors.NotStartedYet, result.Error);
        Assert.Equal(AppointmentStatus.Scheduled, _appointment.Status);
        Assert.Equal(0, _unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task HandleAsync_AppointmentCancelled_PropagatesNotScheduledWithoutSaving()
    {
        _appointment.Cancel("Changed plans", TestData.At(1, 8, 0), byClinic: true);

        var result = await CreateHandler().HandleAsync(new CompleteAppointmentCommand(_appointment.Id), CancellationToken.None);

        Assert.Equal(AppointmentErrors.NotScheduled, result.Error);
        Assert.Equal(0, _unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task HandleAsync_UnknownAppointment_FailsWithNotFound()
    {
        var result = await CreateHandler().HandleAsync(new CompleteAppointmentCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(AppointmentErrors.NotFound, result.Error);
        Assert.Equal(0, _unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task HandleAsync_EmptyId_FailsWithValidationError()
    {
        var result = await CreateHandler().HandleAsync(new CompleteAppointmentCommand(Guid.Empty), CancellationToken.None);

        Assert.Equal("Validation", result.Error.Code);
        Assert.Equal(0, _unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task HandleAsync_SaveFails_PropagatesTheStoreError()
    {
        _unitOfWork.FailWith = AppointmentErrors.NotFound;

        var result = await CreateHandler().HandleAsync(new CompleteAppointmentCommand(_appointment.Id), CancellationToken.None);

        Assert.Equal(AppointmentErrors.NotFound, result.Error);
    }
}

using Citas.Application.Appointments;
using Citas.Application.Tests.Fakes;
using Citas.Domain.Appointments;
using Citas.Domain.Errors;

namespace Citas.Application.Tests.Appointments;

public class CancelAppointmentHandlerTests
{
    private readonly FakeAppointmentRepository _appointments = new();
    private readonly FakeClinicClock _clock = new(TestData.Now);
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly Appointment _appointment;

    public CancelAppointmentHandlerTests()
    {
        _appointment = TestData.Book(TestData.NewProfessional(), TestData.NewPatient(), TestData.At(2, 10, 0));
        _appointments.Items.Add(_appointment);
    }

    private CancelAppointmentHandler CreateHandler() =>
        new(new CancelAppointmentValidator(), _appointments, _clock, _unitOfWork);

    [Fact]
    public async Task HandleAsync_PatientWithMoreThan24HoursNotice_CancelsAndSaves()
    {
        var result = await CreateHandler().HandleAsync(
            new CancelAppointmentCommand(_appointment.Id, "Cannot attend", ByClinic: false), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(AppointmentStatus.Cancelled, _appointment.Status);
        Assert.Equal("Cannot attend", _appointment.CancellationReason);
        Assert.Equal(1, _unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task HandleAsync_PatientWithLessThan24HoursNotice_PropagatesCancellationTooLateWithoutSaving()
    {
        _clock.Now = TestData.At(1, 12, 0);

        var result = await CreateHandler().HandleAsync(
            new CancelAppointmentCommand(_appointment.Id, "Cannot attend", ByClinic: false), CancellationToken.None);

        Assert.Equal(AppointmentErrors.CancellationTooLate, result.Error);
        Assert.Equal(AppointmentStatus.Scheduled, _appointment.Status);
        Assert.Equal(0, _unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task HandleAsync_ClinicWithLessThan24HoursNotice_CancelsAndSaves()
    {
        _clock.Now = TestData.At(1, 12, 0);

        var result = await CreateHandler().HandleAsync(
            new CancelAppointmentCommand(_appointment.Id, "Doctor unavailable", ByClinic: true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(AppointmentStatus.Cancelled, _appointment.Status);
        Assert.Equal(1, _unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task HandleAsync_ClinicAfterTheStart_PropagatesAlreadyStartedWithoutSaving()
    {
        _clock.Now = TestData.At(2, 10, 5);

        var result = await CreateHandler().HandleAsync(
            new CancelAppointmentCommand(_appointment.Id, "Doctor unavailable", ByClinic: true), CancellationToken.None);

        Assert.Equal(AppointmentErrors.AlreadyStarted, result.Error);
        Assert.Equal(0, _unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task HandleAsync_ReasonTooShort_PropagatesInvalidCancellationReasonWithoutSaving()
    {
        var result = await CreateHandler().HandleAsync(
            new CancelAppointmentCommand(_appointment.Id, "ab", ByClinic: false), CancellationToken.None);

        Assert.Equal(AppointmentErrors.InvalidCancellationReason, result.Error);
        Assert.Equal(0, _unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task HandleAsync_UnknownAppointment_FailsWithNotFound()
    {
        var result = await CreateHandler().HandleAsync(
            new CancelAppointmentCommand(Guid.NewGuid(), "Cannot attend", ByClinic: false), CancellationToken.None);

        Assert.Equal(AppointmentErrors.NotFound, result.Error);
        Assert.Equal(0, _unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task HandleAsync_NullReason_FailsWithValidationError()
    {
        var result = await CreateHandler().HandleAsync(
            new CancelAppointmentCommand(_appointment.Id, null!, ByClinic: false), CancellationToken.None);

        Assert.Equal("Validation", result.Error.Code);
        Assert.Equal(0, _unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task HandleAsync_EmptyId_FailsWithValidationError()
    {
        var result = await CreateHandler().HandleAsync(
            new CancelAppointmentCommand(Guid.Empty, "Cannot attend", ByClinic: false), CancellationToken.None);

        Assert.Equal("Validation", result.Error.Code);
    }

    [Fact]
    public async Task HandleAsync_AlreadyCancelled_PropagatesNotScheduledWithoutSaving()
    {
        _appointment.Cancel("Changed plans", TestData.Now, byClinic: true);

        var result = await CreateHandler().HandleAsync(
            new CancelAppointmentCommand(_appointment.Id, "Cannot attend", ByClinic: false), CancellationToken.None);

        Assert.Equal(AppointmentErrors.NotScheduled, result.Error);
        Assert.Equal(0, _unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task HandleAsync_SaveFails_PropagatesTheStoreError()
    {
        _unitOfWork.FailWith = AppointmentErrors.NotFound;

        var result = await CreateHandler().HandleAsync(
            new CancelAppointmentCommand(_appointment.Id, "Cannot attend", ByClinic: false), CancellationToken.None);

        Assert.Equal(AppointmentErrors.NotFound, result.Error);
    }
}

using Citas.Application.Appointments;
using Citas.Application.Tests.Fakes;
using Citas.Domain.Appointments;
using Citas.Domain.Errors;

namespace Citas.Application.Tests.Appointments;

public class RescheduleAppointmentHandlerTests
{
    private readonly FakeProfessionalRepository _professionals = new();
    private readonly FakeAppointmentRepository _appointments = new();
    private readonly FakeClinicClock _clock = new(TestData.Now);
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly Domain.Professionals.Professional _professional = TestData.NewProfessional();
    private readonly Appointment _appointment;

    public RescheduleAppointmentHandlerTests()
    {
        _professionals.Items.Add(_professional);
        _appointment = TestData.Book(_professional, TestData.NewPatient(), TestData.At(2, 10, 0));
        _appointments.Items.Add(_appointment);
    }

    private RescheduleAppointmentHandler CreateHandler() =>
        new(new RescheduleAppointmentValidator(), _professionals, _appointments, _clock, _unitOfWork);

    [Fact]
    public async Task HandleAsync_FreeNewSlot_MovesAppointmentAndSaves()
    {
        var result = await CreateHandler().HandleAsync(
            new RescheduleAppointmentCommand(_appointment.Id, TestData.At(2, 11, 0), 30), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TestData.At(2, 11, 0), result.Value.Start);
        Assert.Equal(TestData.At(2, 11, 30), result.Value.End);
        Assert.Equal(TestData.At(2, 11, 0), _appointment.TimeRange.Start);
        Assert.Equal(1, _unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task HandleAsync_NewSlotOverlapsItsOwnCurrentOne_Succeeds()
    {
        var result = await CreateHandler().HandleAsync(
            new RescheduleAppointmentCommand(_appointment.Id, TestData.At(2, 10, 15), 30), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task HandleAsync_UnknownAppointment_FailsWithNotFound()
    {
        var result = await CreateHandler().HandleAsync(
            new RescheduleAppointmentCommand(Guid.NewGuid(), TestData.At(2, 11, 0), 30), CancellationToken.None);

        Assert.Equal(AppointmentErrors.NotFound, result.Error);
        Assert.Equal(0, _unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task HandleAsync_ProfessionalOfTheAppointmentIsGone_FailsWithNotFound()
    {
        _professionals.Items.Clear();

        var result = await CreateHandler().HandleAsync(
            new RescheduleAppointmentCommand(_appointment.Id, TestData.At(2, 11, 0), 30), CancellationToken.None);

        Assert.Equal(ProfessionalErrors.NotFound, result.Error);
        Assert.Equal(0, _unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task HandleAsync_EmptyId_FailsWithValidationError()
    {
        var result = await CreateHandler().HandleAsync(
            new RescheduleAppointmentCommand(Guid.Empty, TestData.At(2, 11, 0), 30), CancellationToken.None);

        Assert.Equal("Validation", result.Error.Code);
    }

    [Fact]
    public async Task HandleAsync_MissingDuration_FailsWithValidationError()
    {
        var result = await CreateHandler().HandleAsync(
            new RescheduleAppointmentCommand(_appointment.Id, TestData.At(2, 11, 0), 0), CancellationToken.None);

        Assert.Equal("Validation", result.Error.Code);
        Assert.Equal(0, _unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task HandleAsync_DurationNotInSteps_PropagatesInvalidDuration()
    {
        var result = await CreateHandler().HandleAsync(
            new RescheduleAppointmentCommand(_appointment.Id, TestData.At(2, 11, 0), 25), CancellationToken.None);

        Assert.Equal(TimeRangeErrors.InvalidDuration, result.Error);
        Assert.Equal(0, _unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task HandleAsync_LessThan24HoursBeforeOriginalStart_PropagatesRescheduleTooLateWithoutSaving()
    {
        _clock.Now = TestData.At(1, 11, 0);

        var result = await CreateHandler().HandleAsync(
            new RescheduleAppointmentCommand(_appointment.Id, TestData.At(2, 11, 0), 30), CancellationToken.None);

        Assert.Equal(AppointmentErrors.RescheduleTooLate, result.Error);
        Assert.Equal(TestData.At(2, 10, 0), _appointment.TimeRange.Start);
        Assert.Equal(0, _unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task HandleAsync_NewSlotTakenByAnotherAppointment_PropagatesOverlapWithoutSaving()
    {
        _appointments.Items.Add(TestData.Book(_professional, TestData.NewPatient("7654321"), TestData.At(2, 11, 0)));

        var result = await CreateHandler().HandleAsync(
            new RescheduleAppointmentCommand(_appointment.Id, TestData.At(2, 11, 15), 30), CancellationToken.None);

        Assert.Equal(AppointmentErrors.Overlap, result.Error);
        Assert.Equal(TestData.At(2, 10, 0), _appointment.TimeRange.Start);
        Assert.Equal(0, _unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task HandleAsync_NewSlotOutsideWorkingHours_PropagatesDomainErrorWithoutSaving()
    {
        var result = await CreateHandler().HandleAsync(
            new RescheduleAppointmentCommand(_appointment.Id, TestData.At(2, 12, 30), 30), CancellationToken.None);

        Assert.Equal(AppointmentErrors.OutsideWorkingHours, result.Error);
        Assert.Equal(0, _unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task HandleAsync_AppointmentAlreadyCancelled_PropagatesNotScheduled()
    {
        _appointment.Cancel("Changed plans", TestData.Now, byClinic: true);

        var result = await CreateHandler().HandleAsync(
            new RescheduleAppointmentCommand(_appointment.Id, TestData.At(2, 11, 0), 30), CancellationToken.None);

        Assert.Equal(AppointmentErrors.NotScheduled, result.Error);
        Assert.Equal(0, _unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task HandleAsync_StoreRejectsWithOverlap_PropagatesOverlap()
    {
        _unitOfWork.FailWith = AppointmentErrors.Overlap;

        var result = await CreateHandler().HandleAsync(
            new RescheduleAppointmentCommand(_appointment.Id, TestData.At(2, 11, 0), 30), CancellationToken.None);

        Assert.Equal(AppointmentErrors.Overlap, result.Error);
        Assert.Equal(1, _unitOfWork.SaveCalls);
    }
}

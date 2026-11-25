using Citas.Application.Appointments;
using Citas.Application.Tests.Fakes;
using Citas.Domain.Errors;

namespace Citas.Application.Tests.Appointments;

public class ScheduleAppointmentHandlerTests
{
    private readonly FakeProfessionalRepository _professionals = new();
    private readonly FakePatientRepository _patients = new();
    private readonly FakeAppointmentRepository _appointments = new();
    private readonly FakeClinicClock _clock = new(TestData.Now);
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly Domain.Professionals.Professional _professional = TestData.NewProfessional();
    private readonly Domain.Patients.Patient _patient = TestData.NewPatient();

    public ScheduleAppointmentHandlerTests()
    {
        _professionals.Items.Add(_professional);
        _patients.Items.Add(_patient);
    }

    private ScheduleAppointmentHandler CreateHandler() =>
        new(new ScheduleAppointmentValidator(), _professionals, _patients, _appointments, _clock, _unitOfWork);

    private ScheduleAppointmentCommand Command(DateTime? start = null, int minutes = 30, Guid? professionalId = null, Guid? patientId = null) =>
        new(professionalId ?? _professional.Id, patientId ?? _patient.Id, start ?? TestData.At(2, 10, 0), minutes, "Check-up");

    private void AssertNothingSaved()
    {
        Assert.Empty(_appointments.Items);
        Assert.Equal(0, _unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task HandleAsync_FreeSlotInsideWorkingHours_AddsAppointmentAndSaves()
    {
        var result = await CreateHandler().HandleAsync(Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(_professional.Id, result.Value.ProfessionalId);
        Assert.Equal(_patient.Id, result.Value.PatientId);
        Assert.Equal(TestData.At(2, 10, 0), result.Value.Start);
        Assert.Equal(TestData.At(2, 10, 30), result.Value.End);
        Assert.Equal("Scheduled", result.Value.Status);
        Assert.Equal("Check-up", result.Value.Reason);
        Assert.Equal(TestData.Now, result.Value.CreatedAt);
        Assert.Single(_appointments.Items);
        Assert.Equal(1, _unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task HandleAsync_UnknownProfessional_FailsWithNotFound()
    {
        var result = await CreateHandler().HandleAsync(Command(professionalId: Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(ProfessionalErrors.NotFound, result.Error);
        AssertNothingSaved();
    }

    [Fact]
    public async Task HandleAsync_UnknownPatient_FailsWithNotFound()
    {
        var result = await CreateHandler().HandleAsync(Command(patientId: Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(PatientErrors.NotFound, result.Error);
        AssertNothingSaved();
    }

    [Fact]
    public async Task HandleAsync_EmptyProfessionalId_FailsWithValidationError()
    {
        var result = await CreateHandler().HandleAsync(Command(professionalId: Guid.Empty), CancellationToken.None);

        Assert.Equal("Validation", result.Error.Code);
        AssertNothingSaved();
    }

    [Fact]
    public async Task HandleAsync_EmptyPatientId_FailsWithValidationError()
    {
        var result = await CreateHandler().HandleAsync(Command(patientId: Guid.Empty), CancellationToken.None);

        Assert.Equal("Validation", result.Error.Code);
        AssertNothingSaved();
    }

    [Fact]
    public async Task HandleAsync_MissingDuration_FailsWithValidationError()
    {
        var result = await CreateHandler().HandleAsync(Command(minutes: 0), CancellationToken.None);

        Assert.Equal("Validation", result.Error.Code);
        AssertNothingSaved();
    }

    [Fact]
    public async Task HandleAsync_DefaultStart_FailsWithValidationError()
    {
        var result = await CreateHandler().HandleAsync(Command(start: default(DateTime)), CancellationToken.None);

        Assert.Equal("Validation", result.Error.Code);
        AssertNothingSaved();
    }

    [Fact]
    public async Task HandleAsync_DurationNotInSteps_PropagatesInvalidDuration()
    {
        var result = await CreateHandler().HandleAsync(Command(minutes: 20), CancellationToken.None);

        Assert.Equal(TimeRangeErrors.InvalidDuration, result.Error);
        AssertNothingSaved();
    }

    [Fact]
    public async Task HandleAsync_OutsideWorkingHours_PropagatesDomainErrorWithoutSaving()
    {
        var result = await CreateHandler().HandleAsync(Command(start: TestData.At(2, 12, 30)), CancellationToken.None);

        Assert.Equal(AppointmentErrors.OutsideWorkingHours, result.Error);
        AssertNothingSaved();
    }

    [Fact]
    public async Task HandleAsync_LessThanAnHourAhead_PropagatesTooSoonWithoutSaving()
    {
        _clock.Now = TestData.At(2, 9, 30);

        var result = await CreateHandler().HandleAsync(Command(), CancellationToken.None);

        Assert.Equal(AppointmentErrors.TooSoon, result.Error);
        AssertNothingSaved();
    }

    [Fact]
    public async Task HandleAsync_InactiveProfessional_PropagatesInactiveWithoutSaving()
    {
        _professional.Deactivate();

        var result = await CreateHandler().HandleAsync(Command(), CancellationToken.None);

        Assert.Equal(ProfessionalErrors.Inactive, result.Error);
        AssertNothingSaved();
    }

    [Fact]
    public async Task HandleAsync_ProfessionalAlreadyBooked_PropagatesOverlapWithoutSaving()
    {
        _appointments.Items.Add(TestData.Book(_professional, TestData.NewPatient("7654321"), TestData.At(2, 10, 0)));

        var result = await CreateHandler().HandleAsync(Command(start: TestData.At(2, 10, 15)), CancellationToken.None);

        Assert.Equal(AppointmentErrors.Overlap, result.Error);
        Assert.Single(_appointments.Items);
        Assert.Equal(0, _unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task HandleAsync_PatientAlreadyBookedWithAnotherProfessional_PropagatesPatientOverlap()
    {
        var other = TestData.NewProfessional();
        _professionals.Items.Add(other);
        _appointments.Items.Add(TestData.Book(other, _patient, TestData.At(2, 10, 0)));

        var result = await CreateHandler().HandleAsync(Command(), CancellationToken.None);

        Assert.Equal(AppointmentErrors.PatientOverlap, result.Error);
        Assert.Equal(0, _unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task HandleAsync_OverlappingAppointmentIsCancelled_Succeeds()
    {
        var cancelled = TestData.Book(_professional, TestData.NewPatient("7654321"), TestData.At(2, 10, 0));
        cancelled.Cancel("Changed plans", TestData.At(1, 7, 0), byClinic: true);
        _appointments.Items.Add(cancelled);

        var result = await CreateHandler().HandleAsync(Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, _appointments.Items.Count);
    }

    [Fact]
    public async Task HandleAsync_StoreRejectsWithOverlap_PropagatesOverlap()
    {
        _unitOfWork.FailWith = AppointmentErrors.Overlap;

        var result = await CreateHandler().HandleAsync(Command(), CancellationToken.None);

        Assert.Equal(AppointmentErrors.Overlap, result.Error);
        Assert.Equal(1, _unitOfWork.SaveCalls);
    }
}

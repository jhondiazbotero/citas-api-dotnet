using Citas.Domain.Appointments;
using Citas.Domain.Errors;
using Citas.Domain.ValueObjects;

namespace Citas.Domain.Tests.Appointments;

public class AppointmentTests
{
    private static readonly DateTime Start = new(2026, 12, 2, 10, 0, 0, DateTimeKind.Unspecified);
    private static readonly DateTime CreatedAt = Start.AddDays(-10);

    private static Appointment NewAppointment(string? reason = "Check-up")
    {
        var range = TimeRange.Create(Start, TimeSpan.FromMinutes(30)).Value;
        return Appointment.Create(Guid.NewGuid(), Guid.NewGuid(), range, reason, CreatedAt).Value;
    }

    private static Appointment Cancelled()
    {
        var appointment = NewAppointment();
        appointment.Cancel("Changed plans", CreatedAt, byClinic: false);
        return appointment;
    }

    private static Appointment Completed()
    {
        var appointment = NewAppointment();
        appointment.Complete(Start);
        return appointment;
    }

    private static Appointment NoShow()
    {
        var appointment = NewAppointment();
        appointment.MarkNoShow(Start);
        return appointment;
    }

    // ---------- Create ----------

    [Fact]
    public void Create_ValidData_StartsScheduled()
    {
        var professionalId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var range = TimeRange.Create(Start, TimeSpan.FromMinutes(30)).Value;

        var result = Appointment.Create(professionalId, patientId, range, "Check-up", CreatedAt);

        Assert.True(result.IsSuccess);
        var appointment = result.Value;
        Assert.NotEqual(Guid.Empty, appointment.Id);
        Assert.Equal(professionalId, appointment.ProfessionalId);
        Assert.Equal(patientId, appointment.PatientId);
        Assert.Equal(range, appointment.TimeRange);
        Assert.Equal(AppointmentStatus.Scheduled, appointment.Status);
        Assert.Equal("Check-up", appointment.Reason);
        Assert.Equal(CreatedAt, appointment.CreatedAt);
        Assert.Null(appointment.CancellationReason);
        Assert.Null(appointment.CancelledAt);
    }

    [Fact]
    public void Create_WithoutReason_Succeeds()
    {
        Assert.Null(NewAppointment(reason: null).Reason);
    }

    [Fact]
    public void Create_ReasonWith250Characters_Succeeds()
    {
        Assert.True(NewAppointment(new string('a', 250)).Reason!.Length == 250);
    }

    [Fact]
    public void Create_ReasonWith251Characters_FailsWithReasonTooLong()
    {
        var range = TimeRange.Create(Start, TimeSpan.FromMinutes(30)).Value;

        var result = Appointment.Create(Guid.NewGuid(), Guid.NewGuid(), range, new string('a', 251), CreatedAt);

        Assert.Equal(AppointmentErrors.ReasonTooLong, result.Error);
    }

    // ---------- Cancel ----------

    [Fact]
    public void Cancel_ByPatientExactly24HoursBefore_Succeeds()
    {
        var appointment = NewAppointment();
        var now = Start.AddHours(-24);

        var result = appointment.Cancel("Cannot attend", now, byClinic: false);

        Assert.True(result.IsSuccess);
        Assert.Equal(AppointmentStatus.Cancelled, appointment.Status);
        Assert.Equal("Cannot attend", appointment.CancellationReason);
        Assert.Equal(now, appointment.CancelledAt);
    }

    [Fact]
    public void Cancel_ByPatient23Hours59MinutesBefore_FailsWithCancellationTooLate()
    {
        var appointment = NewAppointment();
        var now = Start.AddHours(-24).AddMinutes(1);

        var result = appointment.Cancel("Cannot attend", now, byClinic: false);

        Assert.Equal(AppointmentErrors.CancellationTooLate, result.Error);
        Assert.Equal(AppointmentStatus.Scheduled, appointment.Status);
        Assert.Null(appointment.CancelledAt);
        Assert.Null(appointment.CancellationReason);
    }

    [Fact]
    public void Cancel_ByPatientAfterTheStart_FailsWithCancellationTooLate()
    {
        var appointment = NewAppointment();

        var result = appointment.Cancel("Cannot attend", Start.AddMinutes(5), byClinic: false);

        Assert.Equal(AppointmentErrors.CancellationTooLate, result.Error);
    }

    [Fact]
    public void Cancel_ByClinicOneMinuteBeforeTheStart_Succeeds()
    {
        var appointment = NewAppointment();
        var now = Start.AddMinutes(-1);

        var result = appointment.Cancel("Doctor unavailable", now, byClinic: true);

        Assert.True(result.IsSuccess);
        Assert.Equal(AppointmentStatus.Cancelled, appointment.Status);
        Assert.Equal(now, appointment.CancelledAt);
    }

    [Fact]
    public void Cancel_ByClinicExactlyAtTheStart_FailsWithAlreadyStarted()
    {
        var appointment = NewAppointment();

        var result = appointment.Cancel("Doctor unavailable", Start, byClinic: true);

        Assert.Equal(AppointmentErrors.AlreadyStarted, result.Error);
        Assert.Equal(AppointmentStatus.Scheduled, appointment.Status);
    }

    [Fact]
    public void Cancel_ByClinicAfterTheStart_FailsWithAlreadyStarted()
    {
        var appointment = NewAppointment();

        var result = appointment.Cancel("Doctor unavailable", Start.AddMinutes(10), byClinic: true);

        Assert.Equal(AppointmentErrors.AlreadyStarted, result.Error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ab")]
    public void Cancel_MissingOrShortReason_FailsWithInvalidCancellationReason(string? reason)
    {
        var appointment = NewAppointment();

        var result = appointment.Cancel(reason, CreatedAt, byClinic: false);

        Assert.Equal(AppointmentErrors.InvalidCancellationReason, result.Error);
        Assert.Equal(AppointmentStatus.Scheduled, appointment.Status);
    }

    [Fact]
    public void Cancel_ReasonWith3Characters_Succeeds()
    {
        Assert.True(NewAppointment().Cancel("abc", CreatedAt, byClinic: false).IsSuccess);
    }

    [Fact]
    public void Cancel_ReasonWith250Characters_Succeeds()
    {
        Assert.True(NewAppointment().Cancel(new string('a', 250), CreatedAt, byClinic: false).IsSuccess);
    }

    [Fact]
    public void Cancel_ReasonWith251Characters_FailsWithInvalidCancellationReason()
    {
        var result = NewAppointment().Cancel(new string('a', 251), CreatedAt, byClinic: false);

        Assert.Equal(AppointmentErrors.InvalidCancellationReason, result.Error);
    }

    public static TheoryData<Func<Appointment>> NotScheduledAppointments => new()
    {
        Cancelled,
        Completed,
        NoShow,
    };

    [Theory]
    [MemberData(nameof(NotScheduledAppointments))]
    public void Cancel_WhenNotScheduled_FailsWithNotScheduled(Func<Appointment> build)
    {
        var appointment = build();

        var result = appointment.Cancel("Another reason", CreatedAt, byClinic: true);

        Assert.Equal(AppointmentErrors.NotScheduled, result.Error);
    }

    [Fact]
    public void Cancel_WhenNotScheduledAndReasonInvalid_ReportsNotScheduledFirst()
    {
        var result = Cancelled().Cancel(null, CreatedAt, byClinic: false);

        Assert.Equal(AppointmentErrors.NotScheduled, result.Error);
    }

    // ---------- Complete / MarkNoShow ----------

    [Fact]
    public void Complete_ExactlyAtTheStart_Succeeds()
    {
        var appointment = NewAppointment();

        var result = appointment.Complete(Start);

        Assert.True(result.IsSuccess);
        Assert.Equal(AppointmentStatus.Completed, appointment.Status);
    }

    [Fact]
    public void Complete_AfterTheStart_Succeeds()
    {
        Assert.True(NewAppointment().Complete(Start.AddHours(2)).IsSuccess);
    }

    [Fact]
    public void Complete_OneMinuteBeforeTheStart_FailsWithNotStartedYet()
    {
        var appointment = NewAppointment();

        var result = appointment.Complete(Start.AddMinutes(-1));

        Assert.Equal(AppointmentErrors.NotStartedYet, result.Error);
        Assert.Equal(AppointmentStatus.Scheduled, appointment.Status);
    }

    [Theory]
    [MemberData(nameof(NotScheduledAppointments))]
    public void Complete_WhenNotScheduled_FailsWithNotScheduled(Func<Appointment> build)
    {
        var result = build().Complete(Start.AddHours(1));

        Assert.Equal(AppointmentErrors.NotScheduled, result.Error);
    }

    [Fact]
    public void MarkNoShow_ExactlyAtTheStart_Succeeds()
    {
        var appointment = NewAppointment();

        var result = appointment.MarkNoShow(Start);

        Assert.True(result.IsSuccess);
        Assert.Equal(AppointmentStatus.NoShow, appointment.Status);
    }

    [Fact]
    public void MarkNoShow_OneMinuteBeforeTheStart_FailsWithNotStartedYet()
    {
        var appointment = NewAppointment();

        var result = appointment.MarkNoShow(Start.AddMinutes(-1));

        Assert.Equal(AppointmentErrors.NotStartedYet, result.Error);
        Assert.Equal(AppointmentStatus.Scheduled, appointment.Status);
    }

    [Theory]
    [MemberData(nameof(NotScheduledAppointments))]
    public void MarkNoShow_WhenNotScheduled_FailsWithNotScheduled(Func<Appointment> build)
    {
        var result = build().MarkNoShow(Start.AddHours(1));

        Assert.Equal(AppointmentErrors.NotScheduled, result.Error);
    }
}

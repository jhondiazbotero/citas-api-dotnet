using Citas.Domain.Appointments;
using Citas.Domain.Errors;
using Citas.Domain.Patients;
using Citas.Domain.Professionals;
using Citas.Domain.ValueObjects;

namespace Citas.Domain.Tests.Appointments;

public class AppointmentSchedulerTests
{
    // Tuesday 2026-12-01 08:00; the working day used by most tests is Wednesday 2026-12-02.
    private static readonly DateTime Now = At(1, 8, 0);

    private static DateTime At(int dayOfDecember, int hour, int minute) =>
        new(2026, 12, dayOfDecember, hour, minute, 0, DateTimeKind.Unspecified);

    private static TimeRange Range(DateTime start, int minutes = 30) =>
        TimeRange.Create(start, TimeSpan.FromMinutes(minutes)).Value;

    private static Professional ProfessionalWorking(DayOfWeek day, TimeOnly from, TimeOnly to)
    {
        var professional = Professional.Create("Dr. Luis Mora", "Cardiology").Value;
        professional.SetWorkingHours([WeeklySlot.Create(day, from, to).Value]);
        return professional;
    }

    /// <summary>Available every day of the week all day long, so only the rule under test can fail.</summary>
    private static Professional AlwaysAvailable()
    {
        var professional = Professional.Create("Dr. Luis Mora", "Cardiology").Value;
        professional.SetWorkingHours(Enum.GetValues<DayOfWeek>()
            .Select(d => WeeklySlot.Create(d, TimeOnly.MinValue, TimeOnly.MaxValue).Value));
        return professional;
    }

    private static Patient NewPatient() => Patient.Create("Ana Gomez", "1234567").Value;

    /// <summary>An appointment that already exists, booked far enough in advance.</summary>
    private static Appointment Existing(Professional professional, Patient patient, DateTime start, int minutes = 30) =>
        Appointment.Create(professional.Id, patient.Id, Range(start, minutes), null, At(1, 7, 0)).Value;

    private static Appointment Cancelled(Appointment appointment)
    {
        appointment.Cancel("Changed plans", At(1, 7, 0), byClinic: true);
        return appointment;
    }

    // ---------- Schedule: success ----------

    [Fact]
    public void Schedule_AllRulesMet_ReturnsScheduledAppointment()
    {
        var professional = ProfessionalWorking(DayOfWeek.Wednesday, new TimeOnly(9, 0), new TimeOnly(17, 0));
        var patient = NewPatient();
        var range = Range(At(2, 10, 0));

        var result = AppointmentScheduler.Schedule(professional, patient, range, "Check-up", [], Now);

        Assert.True(result.IsSuccess);
        var appointment = result.Value;
        Assert.Equal(professional.Id, appointment.ProfessionalId);
        Assert.Equal(patient.Id, appointment.PatientId);
        Assert.Equal(range, appointment.TimeRange);
        Assert.Equal("Check-up", appointment.Reason);
        Assert.Equal(AppointmentStatus.Scheduled, appointment.Status);
        Assert.Equal(Now, appointment.CreatedAt);
    }

    [Fact]
    public void Schedule_ReasonOver250Characters_FailsWithReasonTooLong()
    {
        var result = AppointmentScheduler.Schedule(
            AlwaysAvailable(), NewPatient(), Range(At(2, 10, 0)), new string('a', 251), [], Now);

        Assert.Equal(AppointmentErrors.ReasonTooLong, result.Error);
    }

    // ---------- Schedule: rule 1, notice ----------

    [Fact]
    public void Schedule_StartsExactlyOneHourFromNow_Succeeds()
    {
        var result = AppointmentScheduler.Schedule(AlwaysAvailable(), NewPatient(), Range(At(1, 9, 0)), null, [], Now);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Schedule_StartsOneMinuteLessThanAnHourFromNow_FailsWithTooSoon()
    {
        var now = At(1, 8, 1);

        var result = AppointmentScheduler.Schedule(AlwaysAvailable(), NewPatient(), Range(At(1, 9, 0)), null, [], now);

        Assert.Equal(AppointmentErrors.TooSoon, result.Error);
    }

    [Fact]
    public void Schedule_StartsExactlyNow_FailsWithTooSoon()
    {
        var result = AppointmentScheduler.Schedule(AlwaysAvailable(), NewPatient(), Range(At(1, 8, 0)), null, [], Now);

        Assert.Equal(AppointmentErrors.TooSoon, result.Error);
    }

    [Fact]
    public void Schedule_StartedOneMinuteAgo_FailsWithInThePast()
    {
        var now = At(1, 8, 1);

        var result = AppointmentScheduler.Schedule(AlwaysAvailable(), NewPatient(), Range(At(1, 8, 0)), null, [], now);

        Assert.Equal(AppointmentErrors.InThePast, result.Error);
    }

    [Fact]
    public void Schedule_YesterdayStart_FailsWithInThePast()
    {
        var result = AppointmentScheduler.Schedule(AlwaysAvailable(), NewPatient(), Range(At(1, 8, 0).AddDays(-1)), null, [], Now);

        Assert.Equal(AppointmentErrors.InThePast, result.Error);
    }

    // ---------- Schedule: rule 2, professional ----------

    [Fact]
    public void Schedule_InactiveProfessional_FailsWithInactive()
    {
        var professional = AlwaysAvailable();
        professional.Deactivate();

        var result = AppointmentScheduler.Schedule(professional, NewPatient(), Range(At(2, 10, 0)), null, [], Now);

        Assert.Equal(ProfessionalErrors.Inactive, result.Error);
    }

    [Fact]
    public void Schedule_RangeExactlyTheWorkingSlot_Succeeds()
    {
        var professional = ProfessionalWorking(DayOfWeek.Wednesday, new TimeOnly(9, 0), new TimeOnly(10, 0));

        var result = AppointmentScheduler.Schedule(professional, NewPatient(), Range(At(2, 9, 0), 60), null, [], Now);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Schedule_RangeEndsOneMinutePastTheSlot_FailsWithOutsideWorkingHours()
    {
        var professional = ProfessionalWorking(DayOfWeek.Wednesday, new TimeOnly(9, 0), new TimeOnly(10, 59));

        var result = AppointmentScheduler.Schedule(professional, NewPatient(), Range(At(2, 10, 0), 60), null, [], Now);

        Assert.Equal(AppointmentErrors.OutsideWorkingHours, result.Error);
    }

    [Fact]
    public void Schedule_RangeStartsOneMinuteBeforeTheSlot_FailsWithOutsideWorkingHours()
    {
        var professional = ProfessionalWorking(DayOfWeek.Wednesday, new TimeOnly(9, 1), new TimeOnly(12, 0));

        var result = AppointmentScheduler.Schedule(professional, NewPatient(), Range(At(2, 9, 0)), null, [], Now);

        Assert.Equal(AppointmentErrors.OutsideWorkingHours, result.Error);
    }

    [Fact]
    public void Schedule_DayWithoutWorkingHours_FailsWithOutsideWorkingHours()
    {
        var professional = ProfessionalWorking(DayOfWeek.Thursday, new TimeOnly(9, 0), new TimeOnly(17, 0));

        var result = AppointmentScheduler.Schedule(professional, NewPatient(), Range(At(2, 10, 0)), null, [], Now);

        Assert.Equal(AppointmentErrors.OutsideWorkingHours, result.Error);
    }

    // ---------- Schedule: rule 3, overlaps ----------

    [Fact]
    public void Schedule_OverlapsAScheduledAppointmentOfTheProfessional_FailsWithOverlap()
    {
        var professional = AlwaysAvailable();
        var other = Existing(professional, NewPatient(), At(2, 10, 0));

        var result = AppointmentScheduler.Schedule(professional, NewPatient(), Range(At(2, 10, 15)), null, [other], Now);

        Assert.Equal(AppointmentErrors.Overlap, result.Error);
    }

    [Fact]
    public void Schedule_StartsExactlyWhenAnotherEnds_Succeeds()
    {
        var professional = AlwaysAvailable();
        var other = Existing(professional, NewPatient(), At(2, 10, 0));

        var result = AppointmentScheduler.Schedule(professional, NewPatient(), Range(At(2, 10, 30)), null, [other], Now);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Schedule_EndsExactlyWhenAnotherStarts_Succeeds()
    {
        var professional = AlwaysAvailable();
        var other = Existing(professional, NewPatient(), At(2, 10, 0));

        var result = AppointmentScheduler.Schedule(professional, NewPatient(), Range(At(2, 9, 30)), null, [other], Now);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Schedule_OverlapsACancelledAppointment_Succeeds()
    {
        var professional = AlwaysAvailable();
        var cancelled = Cancelled(Existing(professional, NewPatient(), At(2, 10, 0)));

        var result = AppointmentScheduler.Schedule(professional, NewPatient(), Range(At(2, 10, 0)), null, [cancelled], Now);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Schedule_SameTimeAsAnotherProfessionalAndOtherPatient_Succeeds()
    {
        var professional = AlwaysAvailable();
        var otherProfessional = AlwaysAvailable();
        var other = Existing(otherProfessional, NewPatient(), At(2, 10, 0));

        var result = AppointmentScheduler.Schedule(professional, NewPatient(), Range(At(2, 10, 0)), null, [other], Now);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Schedule_PatientAlreadyHasAScheduledAppointmentWithAnotherProfessional_FailsWithPatientOverlap()
    {
        var patient = NewPatient();
        var otherProfessional = AlwaysAvailable();
        var other = Existing(otherProfessional, patient, At(2, 10, 0));

        var result = AppointmentScheduler.Schedule(AlwaysAvailable(), patient, Range(At(2, 10, 15)), null, [other], Now);

        Assert.Equal(AppointmentErrors.PatientOverlap, result.Error);
    }

    [Fact]
    public void Schedule_PatientAppointmentTouchesTheEdge_Succeeds()
    {
        var patient = NewPatient();
        var other = Existing(AlwaysAvailable(), patient, At(2, 10, 0));

        var result = AppointmentScheduler.Schedule(AlwaysAvailable(), patient, Range(At(2, 10, 30)), null, [other], Now);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Schedule_PatientOverlapsACancelledAppointment_Succeeds()
    {
        var patient = NewPatient();
        var cancelled = Cancelled(Existing(AlwaysAvailable(), patient, At(2, 10, 0)));

        var result = AppointmentScheduler.Schedule(AlwaysAvailable(), patient, Range(At(2, 10, 0)), null, [cancelled], Now);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Schedule_BothProfessionalAndPatientOverlap_ReportsProfessionalOverlapFirst()
    {
        var professional = AlwaysAvailable();
        var patient = NewPatient();
        var sameProfessional = Existing(professional, NewPatient(), At(2, 10, 0));
        var samePatient = Existing(AlwaysAvailable(), patient, At(2, 10, 0));

        var result = AppointmentScheduler.Schedule(professional, patient, Range(At(2, 10, 0)), null, [samePatient, sameProfessional], Now);

        Assert.Equal(AppointmentErrors.Overlap, result.Error);
    }

    // ---------- Schedule: order of rules ----------

    [Fact]
    public void Schedule_PastStartAndInactiveProfessional_ReportsInThePastFirst()
    {
        var professional = AlwaysAvailable();
        professional.Deactivate();

        var result = AppointmentScheduler.Schedule(professional, NewPatient(), Range(At(1, 7, 0)), null, [], Now);

        Assert.Equal(AppointmentErrors.InThePast, result.Error);
    }

    [Fact]
    public void Schedule_InactiveAndOutsideHours_ReportsInactiveFirst()
    {
        var professional = ProfessionalWorking(DayOfWeek.Thursday, new TimeOnly(9, 0), new TimeOnly(17, 0));
        professional.Deactivate();

        var result = AppointmentScheduler.Schedule(professional, NewPatient(), Range(At(2, 10, 0)), null, [], Now);

        Assert.Equal(ProfessionalErrors.Inactive, result.Error);
    }

    // ---------- Reschedule ----------

    // The appointment under test is on Thursday 2026-12-03 at 10:00; "now" is Tuesday 08:00 (50 hours before).
    private static (Professional Professional, Patient Patient, Appointment Appointment) Booked()
    {
        var professional = AlwaysAvailable();
        var patient = NewPatient();
        var appointment = Existing(professional, patient, At(3, 10, 0));
        return (professional, patient, appointment);
    }

    [Fact]
    public void Reschedule_ToAFreeRange_MovesTheAppointment()
    {
        var (professional, _, appointment) = Booked();
        var newRange = Range(At(4, 15, 0));

        var result = AppointmentScheduler.Reschedule(appointment, professional, newRange, [appointment], Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(newRange, appointment.TimeRange);
        Assert.Equal(AppointmentStatus.Scheduled, appointment.Status);
    }

    [Fact]
    public void Reschedule_OntoItsOwnTime_Succeeds()
    {
        var (professional, _, appointment) = Booked();

        var result = AppointmentScheduler.Reschedule(appointment, professional, appointment.TimeRange, [appointment], Now);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Reschedule_PartiallyOverOwnTime_Succeeds()
    {
        var (professional, _, appointment) = Booked();
        var newRange = Range(At(3, 10, 15));

        var result = AppointmentScheduler.Reschedule(appointment, professional, newRange, [appointment], Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(newRange, appointment.TimeRange);
    }

    [Fact]
    public void Reschedule_ExistingListDoesNotIncludeItself_Succeeds()
    {
        var (professional, _, appointment) = Booked();

        var result = AppointmentScheduler.Reschedule(appointment, professional, appointment.TimeRange, [], Now);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Reschedule_ExactlyAt24HoursBeforeTheOriginalStart_Succeeds()
    {
        var (professional, _, appointment) = Booked();
        var now = appointment.TimeRange.Start.AddHours(-24);

        var result = AppointmentScheduler.Reschedule(appointment, professional, Range(At(4, 15, 0)), [appointment], now);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Reschedule_23Hours59MinutesBeforeTheOriginalStart_FailsWithRescheduleTooLate()
    {
        var (professional, _, appointment) = Booked();
        var originalRange = appointment.TimeRange;
        var now = appointment.TimeRange.Start.AddHours(-24).AddMinutes(1);

        var result = AppointmentScheduler.Reschedule(appointment, professional, Range(At(4, 15, 0)), [appointment], now);

        Assert.Equal(AppointmentErrors.RescheduleTooLate, result.Error);
        Assert.Equal(originalRange, appointment.TimeRange);
    }

    [Fact]
    public void Reschedule_AfterTheOriginalStart_FailsWithRescheduleTooLate()
    {
        var (professional, _, appointment) = Booked();
        var now = appointment.TimeRange.Start.AddMinutes(5);

        var result = AppointmentScheduler.Reschedule(appointment, professional, Range(At(4, 15, 0)), [appointment], now);

        Assert.Equal(AppointmentErrors.RescheduleTooLate, result.Error);
    }

    [Fact]
    public void Reschedule_CancelledAppointment_FailsWithNotScheduled()
    {
        var (professional, _, appointment) = Booked();
        appointment.Cancel("Changed plans", Now, byClinic: false);

        var result = AppointmentScheduler.Reschedule(appointment, professional, Range(At(4, 15, 0)), [appointment], Now);

        Assert.Equal(AppointmentErrors.NotScheduled, result.Error);
    }

    [Fact]
    public void Reschedule_CompletedAppointment_FailsWithNotScheduled()
    {
        var (professional, _, appointment) = Booked();
        appointment.Complete(appointment.TimeRange.Start);

        var result = AppointmentScheduler.Reschedule(appointment, professional, Range(At(4, 15, 0)), [appointment], Now);

        Assert.Equal(AppointmentErrors.NotScheduled, result.Error);
    }

    [Fact]
    public void Reschedule_NoShowAppointment_FailsWithNotScheduled()
    {
        var (professional, _, appointment) = Booked();
        appointment.MarkNoShow(appointment.TimeRange.Start);

        var result = AppointmentScheduler.Reschedule(appointment, professional, Range(At(4, 15, 0)), [appointment], Now);

        Assert.Equal(AppointmentErrors.NotScheduled, result.Error);
    }

    [Fact]
    public void Reschedule_OverAnotherScheduledAppointmentOfTheProfessional_FailsWithOverlap()
    {
        var (professional, _, appointment) = Booked();
        var other = Existing(professional, NewPatient(), At(4, 15, 0));
        var originalRange = appointment.TimeRange;

        var result = AppointmentScheduler.Reschedule(appointment, professional, Range(At(4, 15, 15)), [appointment, other], Now);

        Assert.Equal(AppointmentErrors.Overlap, result.Error);
        Assert.Equal(originalRange, appointment.TimeRange);
    }

    [Fact]
    public void Reschedule_RightAfterAnotherAppointment_Succeeds()
    {
        var (professional, _, appointment) = Booked();
        var other = Existing(professional, NewPatient(), At(4, 15, 0));

        var result = AppointmentScheduler.Reschedule(appointment, professional, Range(At(4, 15, 30)), [appointment, other], Now);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Reschedule_OverACancelledAppointment_Succeeds()
    {
        var (professional, _, appointment) = Booked();
        var cancelled = Cancelled(Existing(professional, NewPatient(), At(4, 15, 0)));

        var result = AppointmentScheduler.Reschedule(appointment, professional, Range(At(4, 15, 0)), [appointment, cancelled], Now);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Reschedule_OverAnotherAppointmentOfThePatient_FailsWithPatientOverlap()
    {
        var (professional, patient, appointment) = Booked();
        var other = Existing(AlwaysAvailable(), patient, At(4, 15, 0));

        var result = AppointmentScheduler.Reschedule(appointment, professional, Range(At(4, 15, 0)), [appointment, other], Now);

        Assert.Equal(AppointmentErrors.PatientOverlap, result.Error);
    }

    [Fact]
    public void Reschedule_OutsideWorkingHours_FailsWithOutsideWorkingHours()
    {
        var professional = ProfessionalWorking(DayOfWeek.Thursday, new TimeOnly(9, 0), new TimeOnly(17, 0));
        var appointment = Existing(professional, NewPatient(), At(3, 10, 0));

        var result = AppointmentScheduler.Reschedule(appointment, professional, Range(At(3, 16, 45)), [appointment], Now);

        Assert.Equal(AppointmentErrors.OutsideWorkingHours, result.Error);
    }

    [Fact]
    public void Reschedule_NewRangeInThePast_FailsWithInThePast()
    {
        var (professional, _, appointment) = Booked();

        var result = AppointmentScheduler.Reschedule(appointment, professional, Range(At(1, 7, 0)), [appointment], Now);

        Assert.Equal(AppointmentErrors.InThePast, result.Error);
    }

    [Fact]
    public void Reschedule_NewRangeLessThanAnHourAway_FailsWithTooSoon()
    {
        var (professional, _, appointment) = Booked();

        var result = AppointmentScheduler.Reschedule(appointment, professional, Range(At(1, 8, 45)), [appointment], Now);

        Assert.Equal(AppointmentErrors.TooSoon, result.Error);
    }

    [Fact]
    public void Reschedule_InactiveProfessional_FailsWithInactive()
    {
        var (professional, _, appointment) = Booked();
        professional.Deactivate();

        var result = AppointmentScheduler.Reschedule(appointment, professional, Range(At(4, 15, 0)), [appointment], Now);

        Assert.Equal(ProfessionalErrors.Inactive, result.Error);
    }

    [Fact]
    public void Reschedule_WithAProfessionalThatDoesNotOwnTheAppointment_FailsWithProfessionalMismatch()
    {
        var (_, _, appointment) = Booked();

        var result = AppointmentScheduler.Reschedule(appointment, AlwaysAvailable(), Range(At(4, 15, 0)), [appointment], Now);

        Assert.Equal(AppointmentErrors.ProfessionalMismatch, result.Error);
    }

    // ---------- CheckSlot: same rules as Schedule, no patient needed ----------

    [Fact]
    public void CheckSlot_FreeSlotInsideWorkingHours_Succeeds()
    {
        var professional = ProfessionalWorking(DayOfWeek.Wednesday, new TimeOnly(9, 0), new TimeOnly(17, 0));

        var result = AppointmentScheduler.CheckSlot(professional, Range(At(2, 10, 0)), [], Now);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void CheckSlot_LessThanAnHourAhead_FailsWithTooSoon()
    {
        var result = AppointmentScheduler.CheckSlot(AlwaysAvailable(), Range(At(1, 8, 45)), [], Now);

        Assert.Equal(AppointmentErrors.TooSoon, result.Error);
    }

    [Fact]
    public void CheckSlot_InThePast_FailsWithInThePast()
    {
        var result = AppointmentScheduler.CheckSlot(AlwaysAvailable(), Range(At(1, 7, 0)), [], Now);

        Assert.Equal(AppointmentErrors.InThePast, result.Error);
    }

    [Fact]
    public void CheckSlot_OutsideWorkingHours_FailsWithOutsideWorkingHours()
    {
        var professional = ProfessionalWorking(DayOfWeek.Wednesday, new TimeOnly(9, 0), new TimeOnly(12, 0));

        var result = AppointmentScheduler.CheckSlot(professional, Range(At(2, 11, 45)), [], Now);

        Assert.Equal(AppointmentErrors.OutsideWorkingHours, result.Error);
    }

    [Fact]
    public void CheckSlot_InactiveProfessional_FailsWithInactive()
    {
        var professional = AlwaysAvailable();
        professional.Deactivate();

        var result = AppointmentScheduler.CheckSlot(professional, Range(At(2, 10, 0)), [], Now);

        Assert.Equal(ProfessionalErrors.Inactive, result.Error);
    }

    [Fact]
    public void CheckSlot_OverlapsScheduledAppointment_FailsWithOverlap()
    {
        var professional = AlwaysAvailable();
        var other = Existing(professional, NewPatient(), At(2, 10, 0));

        var result = AppointmentScheduler.CheckSlot(professional, Range(At(2, 10, 15)), [other], Now);

        Assert.Equal(AppointmentErrors.Overlap, result.Error);
    }

    [Fact]
    public void CheckSlot_OverlapsCancelledAppointment_Succeeds()
    {
        var professional = AlwaysAvailable();
        var other = Cancelled(Existing(professional, NewPatient(), At(2, 10, 0)));

        var result = AppointmentScheduler.CheckSlot(professional, Range(At(2, 10, 15)), [other], Now);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void CheckSlot_OverlapsAnotherProfessionalsAppointment_Succeeds()
    {
        var professional = AlwaysAvailable();
        var someoneElse = AlwaysAvailable();
        var other = Existing(someoneElse, NewPatient(), At(2, 10, 0));

        var result = AppointmentScheduler.CheckSlot(professional, Range(At(2, 10, 0)), [other], Now);

        Assert.True(result.IsSuccess);
    }
}

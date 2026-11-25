using Citas.Application.Common;
using Citas.Domain.Appointments;
using Citas.Domain.Patients;
using Citas.Domain.Professionals;
using Citas.Domain.ValueObjects;

namespace Citas.Application.Tests.Fakes;

/// <summary>Builders shared by the handler tests. Clinic "now" is Tuesday 2026-12-01 08:00.</summary>
public static class TestData
{
    public static readonly DateTime Now = At(1, 8, 0);

    public static DateTime At(int dayOfDecember, int hour, int minute) =>
        new(2026, 12, dayOfDecember, hour, minute, 0, DateTimeKind.Unspecified);

    public static readonly WorkingSlotDto WednesdayMorning = new(DayOfWeek.Wednesday, new TimeOnly(9, 0), new TimeOnly(12, 0));

    public static TimeRange Range(DateTime start, int minutes = 30) =>
        TimeRange.Create(start, TimeSpan.FromMinutes(minutes)).Value;

    /// <summary>Active professional working Wednesdays 09:00-12:00 and 14:00-17:00.</summary>
    public static Professional NewProfessional()
    {
        var professional = Professional.Create("Dr. Luis Mora", "Cardiology").Value;
        professional.SetWorkingHours(
        [
            WeeklySlot.Create(DayOfWeek.Wednesday, new TimeOnly(9, 0), new TimeOnly(12, 0)).Value,
            WeeklySlot.Create(DayOfWeek.Wednesday, new TimeOnly(14, 0), new TimeOnly(17, 0)).Value,
        ]);
        return professional;
    }

    public static Patient NewPatient(string document = "1234567") =>
        Patient.Create("Ana Gomez", document).Value;

    /// <summary>An already booked appointment, created the day before "now" so any lead-time rule is met.</summary>
    public static Appointment Book(Professional professional, Patient patient, DateTime start, int minutes = 30)
    {
        var result = AppointmentScheduler.Schedule(professional, patient, Range(start, minutes), null, [], At(1, 7, 0));
        return result.Value;
    }
}

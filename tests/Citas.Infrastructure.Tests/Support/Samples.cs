using Citas.Domain.Appointments;
using Citas.Domain.Patients;
using Citas.Domain.Professionals;
using Citas.Domain.ValueObjects;
using Citas.Infrastructure.Persistence;

namespace Citas.Infrastructure.Tests.Support;

/// <summary>Builders for the integration tests. Clinic "now" is Tuesday 2026-12-01 08:00; everything is booked after it.</summary>
public static class Samples
{
    public static readonly DateTime Now = At(1, 8, 0);

    public static DateTime At(int dayOfDecember, int hour, int minute) =>
        new(2026, 12, dayOfDecember, hour, minute, 0, DateTimeKind.Unspecified);

    public static TimeRange Range(DateTime start, int minutes = 30) =>
        TimeRange.Create(start, TimeSpan.FromMinutes(minutes)).Value;

    /// <summary>Active professional who works every day of the week from 00:00 to 23:59.</summary>
    public static Professional NewProfessional(string name = "Dra. Ana Prueba", string specialty = "Medicina general")
    {
        Professional professional = Professional.Create(name, specialty).Value;
        professional.SetWorkingHours(Enum.GetValues<DayOfWeek>()
            .Select(day => WeeklySlot.Create(day, new TimeOnly(0, 0), new TimeOnly(23, 59)).Value));
        return professional;
    }

    public static Patient NewPatient(string document = "1234567", string name = "Luis Prueba", string? phone = null) =>
        Patient.Create(name, document, phone).Value;

    /// <summary>
    /// Builds an appointment through the scheduler WITHOUT looking at other appointments, so the tests can create the
    /// collisions that only the database is able to stop.
    /// </summary>
    public static Appointment NewAppointment(Professional professional, Patient patient, DateTime start, int minutes = 30, string? reason = null) =>
        AppointmentScheduler.Schedule(professional, patient, Range(start, minutes), reason, [], Now).Value;

    /// <summary>Saves a professional and a patient so appointments can point at them.</summary>
    public static async Task<(Professional Professional, Patient Patient)> SaveParticipantsAsync(CitasDbContext db)
    {
        Professional professional = NewProfessional();
        Patient patient = NewPatient();
        db.Professionals.Add(professional);
        db.Patients.Add(patient);
        await db.SaveChangesAsync();
        return (professional, patient);
    }
}

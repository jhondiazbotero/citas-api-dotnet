using Citas.Domain.Appointments;
using Citas.Domain.Patients;
using Citas.Domain.Professionals;
using Citas.Domain.ValueObjects;
using Citas.Infrastructure.Persistence;
using Citas.Infrastructure.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace Citas.Infrastructure.Tests;

public class PersistenceRoundTripTests(PostgresFixture postgres) : DatabaseTestBase(postgres)
{
    [Fact]
    public async Task Patient_WithPhone_ComesBackEqual()
    {
        Patient patient = Samples.NewPatient("9876543", "María Prueba", "3001234567");
        await using (CitasDbContext write = NewContext())
        {
            write.Patients.Add(patient);
            await write.SaveChangesAsync();
        }

        Patient loaded = await NewContext().Patients.SingleAsync(p => p.Id == patient.Id);

        Assert.Equal("María Prueba", loaded.FullName);
        Assert.Equal("9876543", loaded.DocumentNumber);
        Assert.Equal("3001234567", loaded.Phone);
    }

    [Fact]
    public async Task Patient_WithoutPhone_ComesBackWithNullPhone()
    {
        Patient patient = Samples.NewPatient();
        CitasDbContext write = NewContext();
        write.Patients.Add(patient);
        await write.SaveChangesAsync();

        Patient loaded = await NewContext().Patients.SingleAsync(p => p.Id == patient.Id);

        Assert.Null(loaded.Phone);
    }

    [Fact]
    public async Task Professional_WithWorkingHours_ComesBackWithTheSameSlots()
    {
        Professional professional = Professional.Create("Dr. Beto Prueba", "Odontología").Value;
        WeeklySlot[] slots =
        [
            WeeklySlot.Create(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(12, 0)).Value,
            WeeklySlot.Create(DayOfWeek.Monday, new TimeOnly(14, 0), new TimeOnly(18, 30)).Value,
            WeeklySlot.Create(DayOfWeek.Saturday, new TimeOnly(8, 0), new TimeOnly(12, 0)).Value,
        ];
        professional.SetWorkingHours(slots);
        CitasDbContext write = NewContext();
        write.Professionals.Add(professional);
        await write.SaveChangesAsync();

        Professional loaded = await NewContext().Professionals.SingleAsync(p => p.Id == professional.Id);

        Assert.Equal("Dr. Beto Prueba", loaded.FullName);
        Assert.Equal("Odontología", loaded.Specialty);
        Assert.True(loaded.IsActive);
        Assert.Equal(slots.OrderBy(s => s.Day).ThenBy(s => s.From), loaded.WorkingHours.OrderBy(s => s.Day).ThenBy(s => s.From));
    }

    [Fact]
    public async Task Professional_ReplacingTheSchedule_StoresOnlyTheNewSlots()
    {
        Professional professional = Samples.NewProfessional();
        CitasDbContext first = NewContext();
        first.Professionals.Add(professional);
        await first.SaveChangesAsync();

        CitasDbContext second = NewContext();
        Professional tracked = await second.Professionals.SingleAsync(p => p.Id == professional.Id);
        WeeklySlot only = WeeklySlot.Create(DayOfWeek.Friday, new TimeOnly(9, 0), new TimeOnly(10, 0)).Value;
        Assert.True(tracked.SetWorkingHours([only]).IsSuccess);
        tracked.Deactivate();
        await second.SaveChangesAsync();

        Professional loaded = await NewContext().Professionals.SingleAsync(p => p.Id == professional.Id);

        Assert.Equal([only], loaded.WorkingHours);
        Assert.False(loaded.IsActive);
        Assert.Equal(1, await NewContext().Database.SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM professional_working_hours").SingleAsync());
    }

    [Fact]
    public async Task Appointment_ComesBackWithItsTimeRangeAndStatus()
    {
        CitasDbContext write = NewContext();
        (Professional professional, Patient patient) = await Samples.SaveParticipantsAsync(write);
        Appointment appointment = Samples.NewAppointment(professional, patient, Samples.At(2, 9, 15), 45, "Control");
        write.Appointments.Add(appointment);
        await write.SaveChangesAsync();

        Appointment loaded = await NewContext().Appointments.SingleAsync(a => a.Id == appointment.Id);

        Assert.Equal(professional.Id, loaded.ProfessionalId);
        Assert.Equal(patient.Id, loaded.PatientId);
        Assert.Equal(Samples.At(2, 9, 15), loaded.TimeRange.Start);
        Assert.Equal(Samples.At(2, 10, 0), loaded.TimeRange.End);
        Assert.Equal(TimeSpan.FromMinutes(45), loaded.TimeRange.Duration);
        Assert.Equal(DateTimeKind.Unspecified, loaded.TimeRange.Start.Kind);
        Assert.Equal(AppointmentStatus.Scheduled, loaded.Status);
        Assert.Equal("Control", loaded.Reason);
        Assert.Equal(Samples.Now, loaded.CreatedAt);
        Assert.Null(loaded.CancelledAt);
        Assert.Null(loaded.CancellationReason);
    }

    [Fact]
    public async Task Appointment_Cancelled_KeepsCancellationDataAndStoresStatusAsText()
    {
        CitasDbContext write = NewContext();
        (Professional professional, Patient patient) = await Samples.SaveParticipantsAsync(write);
        Appointment appointment = Samples.NewAppointment(professional, patient, Samples.At(5, 10, 0));
        write.Appointments.Add(appointment);
        await write.SaveChangesAsync();
        Assert.True(appointment.Cancel("El paciente viaja", Samples.Now, byClinic: false).IsSuccess);
        await write.SaveChangesAsync();

        Appointment loaded = await NewContext().Appointments.SingleAsync(a => a.Id == appointment.Id);
        string rawStatus = await NewContext().Database
            .SqlQuery<string>($"SELECT status AS \"Value\" FROM appointments")
            .SingleAsync();

        Assert.Equal(AppointmentStatus.Cancelled, loaded.Status);
        Assert.Equal("El paciente viaja", loaded.CancellationReason);
        Assert.Equal(Samples.Now, loaded.CancelledAt);
        Assert.Equal("Cancelled", rawStatus);
    }

    [Fact]
    public async Task Appointment_Rescheduled_StoresTheNewRange()
    {
        CitasDbContext write = NewContext();
        (Professional professional, Patient patient) = await Samples.SaveParticipantsAsync(write);
        Appointment appointment = Samples.NewAppointment(professional, patient, Samples.At(5, 10, 0));
        write.Appointments.Add(appointment);
        await write.SaveChangesAsync();

        TimeRange moved = Samples.Range(Samples.At(7, 16, 0), 60);
        Assert.True(AppointmentScheduler.Reschedule(appointment, professional, moved, [appointment], Samples.Now).IsSuccess);
        await write.SaveChangesAsync();

        Appointment loaded = await NewContext().Appointments.SingleAsync(a => a.Id == appointment.Id);

        Assert.Equal(moved, loaded.TimeRange);
    }
}

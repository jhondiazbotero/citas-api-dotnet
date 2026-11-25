using Citas.Domain.Appointments;
using Citas.Domain.Patients;
using Citas.Domain.Professionals;
using Citas.Infrastructure.Persistence;
using Citas.Infrastructure.Repositories;
using Citas.Infrastructure.Tests.Support;

namespace Citas.Infrastructure.Tests;

public class AppointmentRepositoryTests(PostgresFixture postgres) : DatabaseTestBase(postgres)
{
    private Professional _professional = null!;
    private Patient _patient = null!;

    private async Task<Appointment> SaveAsync(Professional professional, Patient patient, DateTime start, int minutes = 30, bool cancel = false)
    {
        CitasDbContext db = NewContext();
        Appointment appointment = Samples.NewAppointment(professional, patient, start, minutes);
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();
        if (cancel)
        {
            Assert.True(appointment.Cancel("Cancelada por prueba", Samples.Now, byClinic: true).IsSuccess);
            await db.SaveChangesAsync();
        }

        return appointment;
    }

    private async Task PrepareAsync()
    {
        CitasDbContext db = NewContext();
        (_professional, _patient) = await Samples.SaveParticipantsAsync(db);
    }

    private async Task<Patient> SaveOtherPatientAsync(string document)
    {
        CitasDbContext db = NewContext();
        Patient other = Samples.NewPatient(document, "Otro Paciente");
        db.Patients.Add(other);
        await db.SaveChangesAsync();
        return other;
    }

    private async Task<Professional> SaveOtherProfessionalAsync()
    {
        CitasDbContext db = NewContext();
        Professional other = Samples.NewProfessional("Dr. Otro Prueba", "Psicología");
        db.Professionals.Add(other);
        await db.SaveChangesAsync();
        return other;
    }

    private async Task<IReadOnlyList<Appointment>> OverlappingAsync(Guid professionalId, Guid patientId, DateTime start, int minutes)
    {
        var repository = new AppointmentRepository(NewContext());
        return await repository.GetScheduledOverlappingAsync(professionalId, patientId, Samples.Range(start, minutes), CancellationToken.None);
    }

    [Fact]
    public async Task GetById_ExistingAndUnknown()
    {
        await PrepareAsync();
        Appointment saved = await SaveAsync(_professional, _patient, Samples.At(2, 9, 0));
        var repository = new AppointmentRepository(NewContext());

        Appointment? found = await repository.GetByIdAsync(saved.Id, CancellationToken.None);
        Appointment? missing = await repository.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(saved.Id, found?.Id);
        Assert.Null(missing);
    }

    [Fact]
    public async Task Overlapping_SameProfessionalAndCrossingRange_IsReturned()
    {
        await PrepareAsync();
        Appointment saved = await SaveAsync(_professional, _patient, Samples.At(2, 9, 0), 60);
        Patient someoneElse = await SaveOtherPatientAsync("7654321");

        var found = await OverlappingAsync(_professional.Id, someoneElse.Id, Samples.At(2, 9, 30), 30);

        Assert.Equal([saved.Id], found.Select(a => a.Id));
    }

    [Fact]
    public async Task Overlapping_SamePatientWithAnotherProfessional_IsReturned()
    {
        await PrepareAsync();
        Professional other = await SaveOtherProfessionalAsync();
        Appointment saved = await SaveAsync(other, _patient, Samples.At(2, 9, 0), 60);

        var found = await OverlappingAsync(_professional.Id, _patient.Id, Samples.At(2, 9, 30), 30);

        Assert.Equal([saved.Id], found.Select(a => a.Id));
    }

    [Theory]
    [InlineData(8, 30, 30)] // ends exactly when the saved one starts
    [InlineData(10, 0, 30)] // starts exactly when the saved one ends
    public async Task Overlapping_TouchingEdges_IsNotReturned(int hour, int minute, int minutes)
    {
        await PrepareAsync();
        await SaveAsync(_professional, _patient, Samples.At(2, 9, 0), 60);

        var found = await OverlappingAsync(_professional.Id, _patient.Id, Samples.At(2, hour, minute), minutes);

        Assert.Empty(found);
    }

    [Theory]
    [InlineData(8, 45, 30)] // one quarter hour of overlap at the start
    [InlineData(9, 45, 30)] // one quarter hour of overlap at the end
    [InlineData(9, 15, 15)] // inside
    [InlineData(8, 0, 120)] // wraps around it
    public async Task Overlapping_EdgeOverlaps_AreReturned(int hour, int minute, int minutes)
    {
        await PrepareAsync();
        Appointment saved = await SaveAsync(_professional, _patient, Samples.At(2, 9, 0), 60);

        var found = await OverlappingAsync(_professional.Id, _patient.Id, Samples.At(2, hour, minute), minutes);

        Assert.Equal([saved.Id], found.Select(a => a.Id));
    }

    [Fact]
    public async Task Overlapping_CancelledAppointments_AreLeftOut()
    {
        await PrepareAsync();
        await SaveAsync(_professional, _patient, Samples.At(2, 9, 0), 60, cancel: true);

        var found = await OverlappingAsync(_professional.Id, _patient.Id, Samples.At(2, 9, 0), 60);

        Assert.Empty(found);
    }

    [Fact]
    public async Task Overlapping_OtherProfessionalAndOtherPatient_IsLeftOut()
    {
        await PrepareAsync();
        Professional otherProfessional = await SaveOtherProfessionalAsync();
        Patient otherPatient = await SaveOtherPatientAsync("7654321");
        await SaveAsync(otherProfessional, otherPatient, Samples.At(2, 9, 0), 60);

        var found = await OverlappingAsync(_professional.Id, _patient.Id, Samples.At(2, 9, 0), 60);

        Assert.Empty(found);
    }

    [Fact]
    public async Task ByProfessional_FiltersByStartDateWithBothEndsIncluded()
    {
        await PrepareAsync();
        Patient other = await SaveOtherPatientAsync("7654321");
        Professional otherProfessional = await SaveOtherProfessionalAsync();
        Patient third = await SaveOtherPatientAsync("7654322");

        await SaveAsync(_professional, _patient, Samples.At(1, 23, 30), 15); // the day before: out
        Appointment firstMoment = await SaveAsync(_professional, _patient, Samples.At(2, 0, 0)); // 00:00 of "from": in
        Appointment middle = await SaveAsync(_professional, other, Samples.At(3, 12, 0)); // in
        Appointment lastMoment = await SaveAsync(_professional, _patient, Samples.At(4, 23, 30), 15); // last quarter hour that fits in "to": in
        await SaveAsync(_professional, _patient, Samples.At(5, 0, 0)); // 00:00 of the day after: out
        await SaveAsync(otherProfessional, third, Samples.At(3, 12, 0)); // someone else's: out
        Appointment cancelled = await SaveAsync(_professional, third, Samples.At(3, 15, 0), cancel: true); // any status: in

        var repository = new AppointmentRepository(NewContext());
        var found = await repository.GetByProfessionalAsync(
            _professional.Id, new DateOnly(2026, 12, 2), new DateOnly(2026, 12, 4), CancellationToken.None);

        Assert.Equal(
            [firstMoment.Id, middle.Id, cancelled.Id, lastMoment.Id],
            found.Select(a => a.Id));
    }

    [Fact]
    public async Task ByProfessional_SingleDay_AndEmptyWhenFromIsAfterTo()
    {
        await PrepareAsync();
        Appointment onlyOne = await SaveAsync(_professional, _patient, Samples.At(3, 10, 0));
        var repository = new AppointmentRepository(NewContext());

        var oneDay = await repository.GetByProfessionalAsync(
            _professional.Id, new DateOnly(2026, 12, 3), new DateOnly(2026, 12, 3), CancellationToken.None);
        var reversed = await repository.GetByProfessionalAsync(
            _professional.Id, new DateOnly(2026, 12, 4), new DateOnly(2026, 12, 3), CancellationToken.None);
        var openEnded = await repository.GetByProfessionalAsync(
            _professional.Id, new DateOnly(2026, 12, 3), DateOnly.MaxValue, CancellationToken.None);

        Assert.Equal([onlyOne.Id], oneDay.Select(a => a.Id));
        Assert.Empty(reversed);
        Assert.Equal([onlyOne.Id], openEnded.Select(a => a.Id));
    }

    [Fact]
    public async Task PatientRepository_ExistsByDocument_AndGetById()
    {
        await PrepareAsync();
        var repository = new PatientRepository(NewContext());

        Assert.True(await repository.ExistsByDocumentAsync(_patient.DocumentNumber, CancellationToken.None));
        Assert.False(await repository.ExistsByDocumentAsync("55555555", CancellationToken.None));
        Assert.Equal(_patient.Id, (await repository.GetByIdAsync(_patient.Id, CancellationToken.None))?.Id);
        Assert.Null(await repository.GetByIdAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task ProfessionalRepository_GetById_LoadsTheSchedule()
    {
        await PrepareAsync();
        var repository = new ProfessionalRepository(NewContext());

        Professional? found = await repository.GetByIdAsync(_professional.Id, CancellationToken.None);

        Assert.NotNull(found);
        Assert.Equal(7, found.WorkingHours.Count);
        Assert.Null(await repository.GetByIdAsync(Guid.NewGuid(), CancellationToken.None));
    }
}

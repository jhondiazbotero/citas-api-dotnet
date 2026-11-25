using Citas.Domain.Appointments;
using Citas.Domain.Common;
using Citas.Domain.Errors;
using Citas.Domain.Patients;
using Citas.Domain.Professionals;
using Citas.Domain.ValueObjects;
using Citas.Infrastructure.Persistence;
using Citas.Infrastructure.Repositories;
using Citas.Infrastructure.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace Citas.Infrastructure.Tests;

public class UnitOfWorkTests(PostgresFixture postgres) : DatabaseTestBase(postgres)
{
    private Professional _professional = null!;
    private Patient _patient = null!;

    private async Task PrepareAsync()
    {
        (_professional, _patient) = await Samples.SaveParticipantsAsync(NewContext());
    }

    private async Task<Patient> SavePatientAsync(string document)
    {
        CitasDbContext db = NewContext();
        Patient patient = Samples.NewPatient(document, "Paciente " + document);
        db.Patients.Add(patient);
        await db.SaveChangesAsync();
        return patient;
    }

    private async Task<Professional> SaveProfessionalAsync(string name)
    {
        CitasDbContext db = NewContext();
        Professional professional = Samples.NewProfessional(name);
        db.Professionals.Add(professional);
        await db.SaveChangesAsync();
        return professional;
    }

    /// <summary>Books through its OWN context, like a separate request, and returns what the unit of work answered.</summary>
    private async Task<Result> BookAsync(Professional professional, Patient patient, DateTime start, int minutes = 30)
    {
        CitasDbContext db = NewContext();
        new AppointmentRepository(db).Add(Samples.NewAppointment(professional, patient, start, minutes));
        return await new UnitOfWork(db).SaveChangesAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Save_NewAppointment_Succeeds()
    {
        await PrepareAsync();

        Result result = await BookAsync(_professional, _patient, Samples.At(2, 9, 0));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Save_DuplicateDocument_FailsWithDuplicateDocument()
    {
        CitasDbContext first = NewContext();
        new PatientRepository(first).Add(Samples.NewPatient("1234567", "Primera Persona"));
        Assert.True((await new UnitOfWork(first).SaveChangesAsync(CancellationToken.None)).IsSuccess);

        CitasDbContext second = NewContext();
        new PatientRepository(second).Add(Samples.NewPatient("1234567", "Segunda Persona"));
        Result result = await new UnitOfWork(second).SaveChangesAsync(CancellationToken.None);

        Assert.Equal(PatientErrors.DuplicateDocument, result.Error);
    }

    [Fact]
    public async Task Save_SameProfessionalCrossingRange_FailsWithOverlap()
    {
        await PrepareAsync();
        Patient other = await SavePatientAsync("7654321");
        Assert.True((await BookAsync(_professional, _patient, Samples.At(2, 9, 0), 60)).IsSuccess);

        // Two contexts that never saw each other's data: the in-memory rules cannot catch this, the database does.
        Result second = await BookAsync(_professional, other, Samples.At(2, 9, 30), 30);

        Assert.Equal(AppointmentErrors.Overlap, second.Error);
    }

    [Fact]
    public async Task Save_SamePatientCrossingRangeWithAnotherProfessional_FailsWithPatientOverlap()
    {
        await PrepareAsync();
        Professional other = await SaveProfessionalAsync("Dr. Otro Prueba");
        Assert.True((await BookAsync(_professional, _patient, Samples.At(2, 9, 0), 60)).IsSuccess);

        Result second = await BookAsync(other, _patient, Samples.At(2, 9, 30), 30);

        Assert.Equal(AppointmentErrors.PatientOverlap, second.Error);
    }

    [Fact]
    public async Task Save_TwoRequestsAtTheSameTime_OnlyOneWinsAndTheOtherGetsOverlap()
    {
        await PrepareAsync();
        Patient other = await SavePatientAsync("7654321");
        CitasDbContext first = NewContext();
        CitasDbContext second = NewContext();
        new AppointmentRepository(first).Add(Samples.NewAppointment(_professional, _patient, Samples.At(2, 10, 0)));
        new AppointmentRepository(second).Add(Samples.NewAppointment(_professional, other, Samples.At(2, 10, 0)));

        Result[] results = await Task.WhenAll(
            Task.Run(() => new UnitOfWork(first).SaveChangesAsync(CancellationToken.None)),
            Task.Run(() => new UnitOfWork(second).SaveChangesAsync(CancellationToken.None)));

        Assert.Single(results, r => r.IsSuccess);
        Assert.Single(results, r => r.IsFailure && r.Error == AppointmentErrors.Overlap);
        Assert.Equal(1, await NewContext().Appointments.CountAsync());
    }

    [Fact]
    public async Task Save_CancelledAppointmentDoesNotBlockTheSameSlot()
    {
        await PrepareAsync();
        CitasDbContext db = NewContext();
        Appointment first = Samples.NewAppointment(_professional, _patient, Samples.At(2, 9, 0));
        db.Appointments.Add(first);
        await db.SaveChangesAsync();
        Assert.True(first.Cancel("Cancelada por prueba", Samples.Now, byClinic: true).IsSuccess);
        Assert.True((await new UnitOfWork(db).SaveChangesAsync(CancellationToken.None)).IsSuccess);

        Result again = await BookAsync(_professional, _patient, Samples.At(2, 9, 0));

        Assert.True(again.IsSuccess);
    }

    [Fact]
    public async Task Save_AppointmentThatEndsWhenAnotherStarts_IsAccepted()
    {
        await PrepareAsync();
        Assert.True((await BookAsync(_professional, _patient, Samples.At(2, 9, 0), 30)).IsSuccess);

        Result next = await BookAsync(_professional, _patient, Samples.At(2, 9, 30), 30);
        Result previous = await BookAsync(_professional, _patient, Samples.At(2, 8, 30), 30);

        Assert.True(next.IsSuccess);
        Assert.True(previous.IsSuccess);
    }

    [Fact]
    public async Task Save_MovingAnAppointmentOntoAnotherOne_FailsWithOverlap()
    {
        await PrepareAsync();
        Patient other = await SavePatientAsync("7654321");
        Assert.True((await BookAsync(_professional, other, Samples.At(2, 9, 0), 60)).IsSuccess);

        CitasDbContext db = NewContext();
        Appointment mine = Samples.NewAppointment(_professional, _patient, Samples.At(3, 9, 0), 60);
        db.Appointments.Add(mine);
        await db.SaveChangesAsync();
        // The domain accepts the move because it is told nothing else is booked; only the database knows better.
        Assert.True(AppointmentScheduler.Reschedule(mine, _professional, Samples.Range(Samples.At(2, 9, 30), 30), [], Samples.Now).IsSuccess);

        Result result = await new UnitOfWork(db).SaveChangesAsync(CancellationToken.None);

        Assert.Equal(AppointmentErrors.Overlap, result.Error);
    }

    [Fact]
    public async Task Save_TwoPeopleChangeTheSameAppointment_SecondGetsConcurrencyConflict()
    {
        await PrepareAsync();
        CitasDbContext setup = NewContext();
        Appointment saved = Samples.NewAppointment(_professional, _patient, Samples.At(5, 10, 0));
        setup.Appointments.Add(saved);
        await setup.SaveChangesAsync();

        CitasDbContext contextA = NewContext();
        CitasDbContext contextB = NewContext();
        Appointment copyA = await contextA.Appointments.SingleAsync(a => a.Id == saved.Id);
        Appointment copyB = await contextB.Appointments.SingleAsync(a => a.Id == saved.Id);

        Assert.True(copyA.Cancel("Cancela recepción", Samples.Now, byClinic: true).IsSuccess);
        Assert.True((await new UnitOfWork(contextA).SaveChangesAsync(CancellationToken.None)).IsSuccess);

        Assert.True(copyB.Cancel("Cancela el paciente", Samples.Now, byClinic: true).IsSuccess);
        Result result = await new UnitOfWork(contextB).SaveChangesAsync(CancellationToken.None);

        Assert.Equal(AppointmentErrors.ConcurrencyConflict, result.Error);
        Appointment stored = await NewContext().Appointments.SingleAsync(a => a.Id == saved.Id);
        Assert.Equal("Cancela recepción", stored.CancellationReason);
    }

    [Fact]
    public async Task Save_UnrelatedDatabaseError_IsRethrown()
    {
        CitasDbContext db = NewContext();
        // The professional and the patient do not exist: foreign key violation (23503), not a business rule.
        Professional ghostProfessional = Samples.NewProfessional();
        Patient ghostPatient = Samples.NewPatient();
        db.Appointments.Add(Samples.NewAppointment(ghostProfessional, ghostPatient, Samples.At(2, 9, 0)));

        await Assert.ThrowsAsync<DbUpdateException>(() => new UnitOfWork(db).SaveChangesAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Database_RejectsOverlapEvenWithoutTheUnitOfWork()
    {
        // Proves the protection lives in PostgreSQL (the exclusion constraint), not in application code.
        await PrepareAsync();
        Patient other = await SavePatientAsync("7654321");
        Assert.True((await BookAsync(_professional, _patient, Samples.At(2, 9, 0))).IsSuccess);

        CitasDbContext db = NewContext();
        db.Appointments.Add(Samples.NewAppointment(_professional, other, Samples.At(2, 9, 0)));
        DbUpdateException error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        var postgresError = Assert.IsType<Npgsql.PostgresException>(error.InnerException);
        Assert.Equal("23P01", postgresError.SqlState);
        Assert.Equal(ConstraintNames.ProfessionalOverlap, postgresError.ConstraintName);
    }
}

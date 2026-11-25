using Citas.Infrastructure.Persistence;
using Citas.Infrastructure.Tests.Support;
using Citas.Testing;
using Microsoft.EntityFrameworkCore;

namespace Citas.Infrastructure.Tests;

/// <summary>The schema that the real migrations leave behind, seen from PostgreSQL itself.</summary>
public class SchemaTests(PostgresFixture postgres) : DatabaseTestBase(postgres)
{
    private async Task<List<string>> IndexesOfAsync(string table) =>
        await NewContext().Database
            .SqlQuery<string>($"SELECT indexname AS \"Value\" FROM pg_indexes WHERE schemaname = 'public' AND tablename = {table} ORDER BY indexname")
            .ToListAsync();

    [Fact]
    public async Task Appointments_HaveTheAgendaIndex_ThatLeadsWithTheProfessional()
    {
        // EF Core insists on an index for the foreign key professional_id (ix_appointments_professional_id), and cannot index the
        // columns of a complex type, so the agenda index is created with SQL. Removing the EF one was tried and EF brings it back.
        Assert.Contains("ix_appointments_professional_id_start_at", await IndexesOfAsync("appointments"));
    }

    [Fact]
    public async Task Appointments_KeepTheirOtherIndexesAndTheTwoExclusionConstraints()
    {
        List<string> indexes = await IndexesOfAsync("appointments");

        Assert.Contains("ix_appointments_patient_id", indexes);
        Assert.Contains(ConstraintNames.ProfessionalOverlap, indexes);
        Assert.Contains(ConstraintNames.PatientOverlap, indexes);
        Assert.Contains("pk_appointments", indexes);
    }

    [Fact]
    public async Task TheModelAndTheMigrations_AreInStep()
    {
        CitasDbContext db = NewContext();

        Assert.False(db.Database.HasPendingModelChanges());
        await Task.CompletedTask;
    }
}

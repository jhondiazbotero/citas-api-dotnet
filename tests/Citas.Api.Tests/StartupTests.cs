using Citas.Api.Tests.Support;
using Citas.Infrastructure.Persistence;
using Citas.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Citas.Api.Tests;

[Collection(ApiCollection.Name)]
public class StartupTests(PostgresFixture postgres)
{
    private async Task<(int Professionals, int Patients, int Appointments)> CountsAfterStartingAsync(string connectionString, bool migrate, bool seed)
    {
        await using var factory = new CitasApiFactory(connectionString, new Dictionary<string, string>
        {
            ["Database:MigrateOnStartup"] = migrate.ToString(),
            ["Demo:SeedData"] = seed.ToString(),
        });
        using HttpClient client = factory.CreateClient(); // starts the host: migrations and seeding run here

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CitasDbContext>();
        return (
            await db.Professionals.CountAsync(TestContext.Current.CancellationToken),
            await db.Patients.CountAsync(TestContext.Current.CancellationToken),
            await db.Appointments.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MigrateAndSeed_BuildAnEmptyDatabaseAndLoadTheDemoData()
    {
        string connectionString = await postgres.CreateEmptyDatabaseAsync();
        try
        {
            var (professionals, patients, appointments) = await CountsAfterStartingAsync(connectionString, migrate: true, seed: true);

            Assert.Equal(3, professionals);
            Assert.Equal(5, patients);
            Assert.True(appointments > 0);
        }
        finally
        {
            await postgres.DropDatabaseAsync(connectionString);
        }
    }

    [Fact]
    public async Task StartingTwice_DoesNotDuplicateTheDemoData()
    {
        string connectionString = await postgres.CreateEmptyDatabaseAsync();
        try
        {
            var first = await CountsAfterStartingAsync(connectionString, migrate: true, seed: true);
            var second = await CountsAfterStartingAsync(connectionString, migrate: true, seed: true);

            Assert.Equal(first, second);
        }
        finally
        {
            await postgres.DropDatabaseAsync(connectionString);
        }
    }

    [Fact]
    public async Task MigrateOnly_LeavesTheTablesEmpty()
    {
        string connectionString = await postgres.CreateEmptyDatabaseAsync();
        try
        {
            var counts = await CountsAfterStartingAsync(connectionString, migrate: true, seed: false);

            Assert.Equal((0, 0, 0), counts);
        }
        finally
        {
            await postgres.DropDatabaseAsync(connectionString);
        }
    }
}

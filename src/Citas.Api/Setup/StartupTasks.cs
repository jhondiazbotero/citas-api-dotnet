using Citas.Infrastructure.Persistence;
using Citas.Infrastructure.Seeding;
using Microsoft.EntityFrameworkCore;

namespace Citas.Api.Setup;

internal static class StartupTasks
{
    public const string MigrateKey = "Database:MigrateOnStartup";

    /// <summary>
    /// Before the first request: applies the pending migrations when <c>Database:MigrateOnStartup</c> is true and loads the
    /// fictional demo data when <c>Demo:SeedData</c> is true. Both are on in Development and off elsewhere.
    /// </summary>
    public static async Task RunStartupTasksAsync(this WebApplication app)
    {
        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        IServiceProvider services = scope.ServiceProvider;
        ILogger logger = app.Logger;

        if (app.Configuration.GetValue<bool>(MigrateKey))
        {
            logger.LogInformation("Applying database migrations");
            await services.GetRequiredService<CitasDbContext>().Database.MigrateAsync();
        }

        if (await services.GetRequiredService<DemoDataSeeder>().SeedIfEnabledAsync(CancellationToken.None))
        {
            logger.LogInformation("Demo data is in place");
        }
    }
}

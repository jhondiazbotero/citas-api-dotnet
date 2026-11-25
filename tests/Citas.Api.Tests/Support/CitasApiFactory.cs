using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace Citas.Api.Tests.Support;

/// <summary>
/// The real API on a test server, pointed at a throwaway PostgreSQL database and a controllable clock.
/// Runs as Production (what a deployment looks like); tests that need other settings pass them in.
/// </summary>
public sealed class CitasApiFactory(
    string connectionString,
    IReadOnlyDictionary<string, string>? settings = null,
    Action<IServiceCollection>? configureServices = null) : WebApplicationFactory<Program>
{
    /// <summary>Tuesday 2026-12-01 08:00 in the clinic (America/Bogota, UTC-5).</summary>
    public static readonly DateTimeOffset StartOfTime = new(2026, 12, 1, 13, 0, 0, TimeSpan.Zero);

    public FakeTimeProvider Time { get; } = new(StartOfTime);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.UseSetting("ConnectionStrings:Citas", connectionString);
        builder.UseSetting("Database:MigrateOnStartup", "false");
        builder.UseSetting("Demo:SeedData", "false");

        foreach ((string key, string value) in settings ?? new Dictionary<string, string>())
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Time);
            configureServices?.Invoke(services);
        });
    }
}

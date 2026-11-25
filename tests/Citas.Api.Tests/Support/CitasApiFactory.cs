using System.Security.Cryptography;
using Citas.Application.Abstractions;
using Citas.Domain.Staff;
using Citas.Infrastructure.Persistence;
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

    /// <summary>Generated for each factory: no key is written anywhere.</summary>
    public string SigningKey { get; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

    /// <summary>The issuer of the API itself, so the tests sign tokens exactly like the real login does (and with the fake clock).</summary>
    public string TokenFor(StaffRole role, Guid? professionalId = null)
    {
        var user = StaffUser.Create($"{role}@tests.local", "hash", role, professionalId).Value;
        return Services.GetRequiredService<ITokenIssuer>().Issue(user).Value;
    }

    /// <summary>Inserts a user straight into the database, with a real hash of the password.</summary>
    public async Task<StaffUser> AddUserAsync(
        string email,
        string password,
        StaffRole role = StaffRole.Reception,
        Guid? professionalId = null,
        bool active = true)
    {
        await using AsyncServiceScope scope = Services.CreateAsyncScope();
        var user = StaffUser.Create(email, scope.ServiceProvider.GetRequiredService<IPasswordHasher>().Hash(password), role, professionalId).Value;
        if (!active)
        {
            user.Deactivate();
        }

        var db = scope.ServiceProvider.GetRequiredService<CitasDbContext>();
        db.StaffUsers.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.UseSetting("ConnectionStrings:Citas", connectionString);
        builder.UseSetting("Database:MigrateOnStartup", "false");
        builder.UseSetting("Demo:SeedData", "false");
        builder.UseSetting("Jwt:SigningKey", SigningKey);

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

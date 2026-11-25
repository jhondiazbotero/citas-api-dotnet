using Citas.Testing;
using Citas.Application.Abstractions;
using Citas.Domain.Appointments;
using Citas.Domain.Staff;
using Citas.Infrastructure.Security;
using Citas.Infrastructure.Persistence;
using Citas.Infrastructure.Seeding;
using Citas.Infrastructure.Tests.Support;
using Citas.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace Citas.Infrastructure.Tests;

public class DemoDataSeederTests(PostgresFixture postgres) : DatabaseTestBase(postgres)
{
    // Tuesday 2026-12-01 08:00 in Bogota.
    private static readonly IClinicClock Clock = new ClinicClock(
        new FakeTimeProvider(new DateTimeOffset(2026, 12, 1, 13, 0, 0, TimeSpan.Zero)),
        TimeZoneInfo.FindSystemTimeZoneById("America/Bogota"));

    private const string DemoPassword = "Clave-de-demo-1";

    private static readonly IdentityPasswordHasher Hasher = new();

    private readonly CapturingLogger _log = new();

    private DemoDataSeeder NewSeeder(bool? enabled = null, string? usersPassword = null)
    {
        var values = new Dictionary<string, string?>();
        if (enabled is not null)
        {
            values[DemoDataSeeder.ConfigurationKey] = enabled.Value.ToString();
        }

        if (usersPassword is not null)
        {
            values[DemoDataSeeder.UsersPasswordKey] = usersPassword;
        }

        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        return new DemoDataSeeder(NewContext(), Clock, configuration, Hasher, _log);
    }

    private sealed class CapturingLogger : ILogger<DemoDataSeeder>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }

    private async Task<(int Professionals, int Patients, int Appointments, int Slots)> CountsAsync()
    {
        CitasDbContext db = NewContext();
        int slots = await db.Database.SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM professional_working_hours").SingleAsync();
        return (await db.Professionals.CountAsync(), await db.Patients.CountAsync(), await db.Appointments.CountAsync(), slots);
    }

    [Fact]
    public async Task Seed_CreatesThreeProfessionalsFivePatientsAndFutureAppointments()
    {
        await NewSeeder().SeedAsync(CancellationToken.None);

        var counts = await CountsAsync();
        Assert.Equal(3, counts.Professionals);
        Assert.Equal(5, counts.Patients);
        Assert.True(counts.Appointments > 0);

        CitasDbContext db = NewContext();
        var appointments = await db.Appointments.ToListAsync();
        Assert.All(appointments, a =>
        {
            Assert.True(a.TimeRange.Start > Clock.Now);
            Assert.Equal(AppointmentStatus.Scheduled, a.Status);
        });
    }

    [Fact]
    public async Task Seed_GivesWeekdayMorningsAndAfternoonsAndOneSaturday()
    {
        await NewSeeder().SeedAsync(CancellationToken.None);

        CitasDbContext db = NewContext();
        var professionals = await db.Professionals.ToListAsync();

        Assert.Equal(["Medicina general", "Odontología", "Psicología"], professionals.Select(p => p.Specialty).Order());
        Assert.All(professionals, p => Assert.Equal(10, p.WorkingHours.Count(s => s.Day != DayOfWeek.Saturday)));
        Assert.Equal(1, professionals.Count(p => p.WorkingHours.Any(s => s.Day == DayOfWeek.Saturday)));
        Assert.All(
            professionals.SelectMany(p => p.WorkingHours).Where(s => s.Day != DayOfWeek.Saturday),
            s => Assert.Contains((s.From, s.To), new[] { (new TimeOnly(8, 0), new TimeOnly(12, 0)), (new TimeOnly(14, 0), new TimeOnly(18, 0)) }));
    }

    [Fact]
    public async Task Seed_RunTwice_DoesNotDuplicateAnything()
    {
        await NewSeeder().SeedAsync(CancellationToken.None);
        var afterFirst = await CountsAsync();

        await NewSeeder().SeedAsync(CancellationToken.None);
        await NewSeeder().SeedAsync(CancellationToken.None);

        Assert.Equal(afterFirst, await CountsAsync());
    }

    [Fact]
    public async Task SeedIfEnabled_WithoutTheSetting_DoesNothing()
    {
        bool ran = await NewSeeder().SeedIfEnabledAsync(CancellationToken.None);

        Assert.False(ran);
        Assert.Equal((0, 0, 0, 0), await CountsAsync());
    }

    [Fact]
    public async Task SeedIfEnabled_SettingFalse_DoesNothing()
    {
        bool ran = await NewSeeder(enabled: false).SeedIfEnabledAsync(CancellationToken.None);

        Assert.False(ran);
        Assert.Equal((0, 0, 0, 0), await CountsAsync());
    }

    [Fact]
    public async Task SeedIfEnabled_SettingTrue_Seeds()
    {
        bool ran = await NewSeeder(enabled: true).SeedIfEnabledAsync(CancellationToken.None);

        Assert.True(ran);
        Assert.Equal(3, (await CountsAsync()).Professionals);
    }

    [Fact]
    public async Task Seed_WithoutAPassword_CreatesNoUsersAndWarns()
    {
        await NewSeeder().SeedAsync(CancellationToken.None);

        Assert.Empty(await NewContext().StaffUsers.ToListAsync());
        Assert.Equal(3, (await CountsAsync()).Professionals);
        var warning = Assert.Single(_log.Entries, e => e.Level == LogLevel.Warning);
        Assert.Contains(DemoDataSeeder.UsersPasswordKey, warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(DemoPassword, warning.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Seed_WithABlankPassword_CreatesNoUsers(string password)
    {
        await NewSeeder(usersPassword: password).SeedAsync(CancellationToken.None);

        Assert.Empty(await NewContext().StaffUsers.ToListAsync());
    }

    [Fact]
    public async Task Seed_WithAPassword_CreatesAdminReceptionAndOneUserPerProfessional()
    {
        await NewSeeder(usersPassword: DemoPassword).SeedAsync(CancellationToken.None);

        CitasDbContext db = NewContext();
        var users = await db.StaffUsers.ToListAsync();
        Assert.Equal(
            ["admin@demo.local", "alderete@demo.local", "marquina@demo.local", "recepcion@demo.local", "valcarcel@demo.local"],
            users.Select(u => u.Email).Order());
        Assert.Equal(StaffRole.Admin, users.Single(u => u.Email == "admin@demo.local").Role);
        Assert.Equal(StaffRole.Reception, users.Single(u => u.Email == "recepcion@demo.local").Role);
        Assert.All(users, u => Assert.True(u.IsActive));

        var professionals = await db.Professionals.ToListAsync();
        StaffUser marquina = users.Single(u => u.Email == "marquina@demo.local");
        Assert.Equal(StaffRole.Professional, marquina.Role);
        Assert.Equal("Dra. Elena Marquina", professionals.Single(p => p.Id == marquina.ProfessionalId).FullName);
        Assert.Equal(3, users.Count(u => u.ProfessionalId is not null));
        Assert.DoesNotContain(_log.Entries, e => e.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task Seed_UserPasswordsAreHashedAndEachHasItsOwnSalt()
    {
        await NewSeeder(usersPassword: DemoPassword).SeedAsync(CancellationToken.None);

        var users = await NewContext().StaffUsers.ToListAsync();

        Assert.All(users, u =>
        {
            Assert.DoesNotContain(DemoPassword, u.PasswordHash, StringComparison.Ordinal);
            Assert.True(Hasher.Verify(u.PasswordHash, DemoPassword));
        });
        Assert.Equal(users.Count, users.Select(u => u.PasswordHash).Distinct().Count());
    }

    [Fact]
    public async Task Seed_UsersRunTwice_DoNotDuplicateAndKeepTheirHash()
    {
        await NewSeeder(usersPassword: DemoPassword).SeedAsync(CancellationToken.None);
        var first = await NewContext().StaffUsers.OrderBy(u => u.Email).Select(u => new { u.Id, u.PasswordHash }).ToListAsync();

        await NewSeeder(usersPassword: DemoPassword).SeedAsync(CancellationToken.None);
        var second = await NewContext().StaffUsers.OrderBy(u => u.Email).Select(u => new { u.Id, u.PasswordHash }).ToListAsync();

        Assert.Equal(first, second);
    }
}

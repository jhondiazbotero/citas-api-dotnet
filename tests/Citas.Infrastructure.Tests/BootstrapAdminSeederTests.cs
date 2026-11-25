using Citas.Domain.Staff;
using Citas.Infrastructure.Persistence;
using Citas.Infrastructure.Security;
using Citas.Infrastructure.Seeding;
using Citas.Infrastructure.Tests.Support;
using Citas.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Citas.Infrastructure.Tests;

public class BootstrapAdminSeederTests(PostgresFixture postgres) : DatabaseTestBase(postgres)
{
    private const string Email = "Dueno@Clinica.Test";
    private const string Password = "Una-clave-larga-1";

    private static readonly IdentityPasswordHasher Hasher = new();

    private readonly CapturingLogger _log = new();

    private BootstrapAdminSeeder NewSeeder(string? email = Email, string? password = Password)
    {
        var values = new Dictionary<string, string?>
        {
            [BootstrapAdminSeeder.EmailKey] = email,
            [BootstrapAdminSeeder.PasswordKey] = password,
        };
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        return new BootstrapAdminSeeder(NewContext(), configuration, Hasher, _log);
    }

    private async Task AddUserAsync(string email, StaffRole role)
    {
        CitasDbContext db = NewContext();
        db.StaffUsers.Add(StaffUser.Create(email, "hash-cualquiera", role).Value);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task WithoutAnyAdmin_CreatesTheConfiguredOne_WithAHashThatChecksOut()
    {
        bool created = await NewSeeder().EnsureAdminAsync(CancellationToken.None);

        Assert.True(created);
        StaffUser admin = await NewContext().StaffUsers.SingleAsync();
        Assert.Equal("dueno@clinica.test", admin.Email);
        Assert.Equal(StaffRole.Admin, admin.Role);
        Assert.True(admin.IsActive);
        Assert.Null(admin.ProfessionalId);
        Assert.NotEqual(Password, admin.PasswordHash);
        Assert.True(Hasher.Verify(admin.PasswordHash, Password));
    }

    [Fact]
    public async Task RunningItTwice_CreatesOnlyOneAdmin()
    {
        await NewSeeder().EnsureAdminAsync(CancellationToken.None);

        bool second = await NewSeeder().EnsureAdminAsync(CancellationToken.None);

        Assert.False(second);
        Assert.Equal(1, await NewContext().StaffUsers.CountAsync());
    }

    [Fact]
    public async Task WhenAnAdminAlreadyExists_NothingIsCreated_AndTheSettingsAreNotEvenChecked()
    {
        await AddUserAsync("otro@clinica.test", StaffRole.Admin);

        bool created = await NewSeeder(password: "corta").EnsureAdminAsync(CancellationToken.None);

        Assert.False(created);
        Assert.Equal("otro@clinica.test", (await NewContext().StaffUsers.SingleAsync()).Email);
    }

    [Fact]
    public async Task AReceptionUser_DoesNotCountAsAnAdmin()
    {
        await AddUserAsync("recepcion@clinica.test", StaffRole.Reception);

        bool created = await NewSeeder().EnsureAdminAsync(CancellationToken.None);

        Assert.True(created);
        Assert.Equal(2, await NewContext().StaffUsers.CountAsync());
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("   ", "   ")]
    public async Task WithoutSettings_DoesNothing(string? email, string? password)
    {
        bool created = await NewSeeder(email, password).EnsureAdminAsync(CancellationToken.None);

        Assert.False(created);
        Assert.Empty(await NewContext().StaffUsers.ToListAsync());
        Assert.DoesNotContain(_log.Entries, e => e.Level >= LogLevel.Warning);
    }

    [Theory]
    [InlineData(Email, null)]
    [InlineData(null, Password)]
    public async Task WithOnlyOneOfTheTwoSettings_DoesNothing_AndWarnsWithoutTheValues(string? email, string? password)
    {
        bool created = await NewSeeder(email, password).EnsureAdminAsync(CancellationToken.None);

        Assert.False(created);
        Assert.Empty(await NewContext().StaffUsers.ToListAsync());
        var warning = Assert.Single(_log.Entries, e => e.Level == LogLevel.Warning);
        Assert.Contains(BootstrapAdminSeeder.PasswordKey, warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Password, warning.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("corta-11-ch")]
    [InlineData("xyz-corta")]
    public async Task APasswordThatIsTooShort_StopsTheStartup_AndNothingIsCreated(string password)
    {
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => NewSeeder(password: password).EnsureAdminAsync(CancellationToken.None));

        Assert.Contains(BootstrapAdminSeeder.PasswordKey, failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(password, failure.Message, StringComparison.Ordinal);
        Assert.Empty(await NewContext().StaffUsers.ToListAsync());
    }

    [Fact]
    public async Task APasswordOf257Characters_StopsTheStartup_AndOf256IsAccepted()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => NewSeeder(password: new string('a', 257)).EnsureAdminAsync(CancellationToken.None));
        Assert.Empty(await NewContext().StaffUsers.ToListAsync());

        Assert.True(await NewSeeder(password: new string('a', 256)).EnsureAdminAsync(CancellationToken.None));
    }

    [Fact]
    public async Task AnInvalidEmail_StopsTheStartup()
    {
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => NewSeeder(email: "sin-arroba").EnsureAdminAsync(CancellationToken.None));

        Assert.Contains(BootstrapAdminSeeder.EmailKey, failure.Message, StringComparison.Ordinal);
        Assert.Empty(await NewContext().StaffUsers.ToListAsync());
    }

    [Fact]
    public async Task NothingItLogs_EverContainsThePassword()
    {
        await NewSeeder().EnsureAdminAsync(CancellationToken.None);
        await NewSeeder(password: null).EnsureAdminAsync(CancellationToken.None);

        Assert.NotEmpty(_log.Entries);
        Assert.All(_log.Entries, e => Assert.DoesNotContain(Password, e.Message, StringComparison.Ordinal));
    }

    [Fact]
    public async Task WhenTheEmailBelongsToANonAdminUser_StopsTheStartup_WithoutCreatingAnything()
    {
        await AddUserAsync("dueno@clinica.test", StaffRole.Reception);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => NewSeeder().EnsureAdminAsync(CancellationToken.None));

        Assert.Contains(BootstrapAdminSeeder.EmailKey, failure.Message, StringComparison.Ordinal);
        Assert.Equal(StaffRole.Reception, (await NewContext().StaffUsers.SingleAsync()).Role);
    }

    private sealed class CapturingLogger : ILogger<BootstrapAdminSeeder>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }
}

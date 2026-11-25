using Citas.Domain.Errors;
using Citas.Domain.Professionals;
using Citas.Domain.Staff;
using Citas.Infrastructure.Persistence;
using Citas.Infrastructure.Repositories;
using Citas.Infrastructure.Tests.Support;
using Citas.Testing;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Citas.Infrastructure.Tests;

public class StaffUserRepositoryTests(PostgresFixture postgres) : DatabaseTestBase(postgres)
{
    private const string Hash = "AQAAAAIAAYagAAAAEExampleHash";

    private static StaffUser NewUser(string email = "admin@demo.local", StaffRole role = StaffRole.Admin, Guid? professionalId = null) =>
        StaffUser.Create(email, Hash, role, professionalId).Value;

    private async Task SaveAsync(params StaffUser[] users)
    {
        CitasDbContext db = NewContext();
        db.StaffUsers.AddRange(users);
        await db.SaveChangesAsync();
    }

    private async Task<Guid> SaveProfessionalAsync()
    {
        CitasDbContext db = NewContext();
        Professional professional = Samples.NewProfessional();
        db.Professionals.Add(professional);
        await db.SaveChangesAsync();
        return professional.Id;
    }

    [Fact]
    public async Task RoundTrip_KeepsEveryField()
    {
        Guid professionalId = await SaveProfessionalAsync();
        StaffUser saved = NewUser("marquina@demo.local", StaffRole.Professional, professionalId);
        saved.Deactivate();
        await SaveAsync(saved);

        StaffUser? loaded = await new StaffUserRepository(NewContext()).GetByEmailAsync("marquina@demo.local", CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal(saved.Id, loaded.Id);
        Assert.Equal(Hash, loaded.PasswordHash);
        Assert.Equal(StaffRole.Professional, loaded.Role);
        Assert.Equal(professionalId, loaded.ProfessionalId);
        Assert.False(loaded.IsActive);
    }

    [Fact]
    public async Task GetByEmail_UnknownEmail_IsNull()
    {
        await SaveAsync(NewUser());

        Assert.Null(await new StaffUserRepository(NewContext()).GetByEmailAsync("nadie@demo.local", CancellationToken.None));
    }

    [Fact]
    public async Task Add_ThroughTheRepositoryAndTheUnitOfWork_Persists()
    {
        CitasDbContext db = NewContext();
        new StaffUserRepository(db).Add(NewUser("recepcion@demo.local", StaffRole.Reception));
        var result = await new UnitOfWork(db).SaveChangesAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, await NewContext().StaffUsers.CountAsync());
    }

    [Fact]
    public async Task TheRoleIsStoredAsText()
    {
        await SaveAsync(NewUser(role: StaffRole.Reception));

        string role = await NewContext().Database.SqlQuery<string>($"SELECT role AS \"Value\" FROM staff_users").SingleAsync();

        Assert.Equal("Reception", role);
    }

    [Fact]
    public async Task DuplicateEmail_IsRefusedByTheUnitOfWorkWithADomainError()
    {
        await SaveAsync(NewUser());
        CitasDbContext db = NewContext();
        db.StaffUsers.Add(NewUser());

        var result = await new UnitOfWork(db).SaveChangesAsync(CancellationToken.None);

        Assert.Equal(StaffUserErrors.DuplicateEmail, result.Error);
    }

    [Fact]
    public async Task ProfessionalLink_MustPointAtAnExistingProfessional()
    {
        CitasDbContext db = NewContext();
        db.StaffUsers.Add(NewUser("fantasma@demo.local", StaffRole.Professional, Guid.NewGuid()));

        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        Assert.Equal("fk_staff_users_professionals_professional_id", Assert.IsType<PostgresException>(error.InnerException).ConstraintName);
    }

    [Theory]
    [InlineData("Admin", true)]
    [InlineData("Professional", false)]
    [InlineData("Reception", true)]
    [InlineData("Doctor", false)]
    public async Task TheDatabaseItselfRefusesARoleThatBreaksTheRules(string role, bool withProfessional)
    {
        Guid? professionalId = withProfessional ? await SaveProfessionalAsync() : null;
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "INSERT INTO staff_users (id, email, password_hash, role, professional_id, is_active) VALUES (@id, @email, 'h', @role, @professional, true)",
            connection);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("email", "x@demo.local");
        command.Parameters.AddWithValue("role", role);
        command.Parameters.AddWithValue("professional", (object?)professionalId ?? DBNull.Value);

        // Admin/Reception with a professional, Professional without one, and an unknown role: all three are rejected.
        var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());

        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
    }
}

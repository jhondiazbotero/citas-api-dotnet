using Citas.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Citas.Testing;

/// <summary>
/// One real PostgreSQL container shared by every test of the collection. The REAL migrations run once on a template
/// database; each test then gets its own copy (CREATE DATABASE ... TEMPLATE), so tests never see each other's rows.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private const string TemplateName = "citas_template";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        await ExecuteAsync($"CREATE DATABASE {TemplateName}");
        await using var context = CreateContext(ConnectionStringFor(TemplateName));
        await context.Database.MigrateAsync();
        NpgsqlConnection.ClearAllPools();
    }

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();

    public async Task<string> CreateDatabaseAsync()
    {
        string name = $"citas_{Guid.NewGuid():N}";
        await ExecuteAsync($"CREATE DATABASE {name} TEMPLATE {TemplateName}");
        return ConnectionStringFor(name);
    }

    public async Task DropDatabaseAsync(string connectionString)
    {
        string name = new NpgsqlConnectionStringBuilder(connectionString).Database!;
        NpgsqlConnection.ClearAllPools();
        await ExecuteAsync($"DROP DATABASE IF EXISTS {name} WITH (FORCE)");
    }

    public static CitasDbContext CreateContext(string connectionString) =>
        new(new DbContextOptionsBuilder<CitasDbContext>().UseNpgsql(connectionString).Options);

    private string ConnectionStringFor(string database) =>
        new NpgsqlConnectionStringBuilder(_container.GetConnectionString()) { Database = database }.ConnectionString;

    private async Task ExecuteAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(_container.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}

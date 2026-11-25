using Citas.Infrastructure.Persistence;

namespace Citas.Infrastructure.Tests.Support;

/// <summary>Gives every test its own empty database and cleans it up afterwards.</summary>
[Collection(PostgresCollection.Name)]
public abstract class DatabaseTestBase(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly List<CitasDbContext> _contexts = [];

    protected string ConnectionString { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync() => ConnectionString = await postgres.CreateDatabaseAsync();

    public async ValueTask DisposeAsync()
    {
        foreach (CitasDbContext context in _contexts)
        {
            await context.DisposeAsync();
        }

        await postgres.DropDatabaseAsync(ConnectionString);
    }

    /// <summary>A new context (new connection, empty identity map), as if it came from another request.</summary>
    protected CitasDbContext NewContext()
    {
        CitasDbContext context = PostgresFixture.CreateContext(ConnectionString);
        _contexts.Add(context);
        return context;
    }
}

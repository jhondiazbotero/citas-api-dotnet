using Citas.Testing;

namespace Citas.Infrastructure.Tests.Support;

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}

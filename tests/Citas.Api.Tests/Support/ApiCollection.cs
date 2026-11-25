using Citas.Testing;

namespace Citas.Api.Tests.Support;

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "api";
}

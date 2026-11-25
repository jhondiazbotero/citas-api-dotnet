using System.Net;
using System.Text.Json;
using Citas.Api.Tests.Support;
using Citas.Testing;

namespace Citas.Api.Tests;

public class HealthTests(PostgresFixture postgres) : ApiTestBase(postgres)
{
    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health")]
    public async Task Live_AndTheLegacyAlias_Return200(string url)
    {
        using var response = await GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Ready_Returns200WithTheDatabaseUp()
    {
        using var response = await GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement body = await response.ReadAsync();
        Assert.Equal("Healthy", body.GetProperty("status").GetString());
        Assert.Contains(body.GetProperty("checks").EnumerateArray(), c => c.GetProperty("name").GetString() == "postgres");
    }
}

public class HealthWithoutDatabaseTests(PostgresFixture postgres) : ApiTestBase(postgres)
{
    private const string Unreachable = "Host=127.0.0.1;Port=1;Database=citas;Username=citas;Password=not-a-real-secret;Timeout=2";

    protected override CitasApiFactory CreateFactory(string connectionString) => new(Unreachable);

    [Fact]
    public async Task Ready_Returns503_ButLiveStaysUp()
    {
        using var live = await GetAsync("/health/live");
        using var ready = await GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        string text = await ready.Content.ReadAsStringAsync(Cancellation);
        Assert.Contains("Unhealthy", text, StringComparison.Ordinal);
        Assert.DoesNotContain("not-a-real-secret", text, StringComparison.Ordinal);
        Assert.DoesNotContain("127.0.0.1", text, StringComparison.Ordinal);
    }
}

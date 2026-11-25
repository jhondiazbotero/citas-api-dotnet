using System.Net;
using Citas.Api.Tests.Support;
using Citas.Testing;

namespace Citas.Api.Tests;

public class VersioningTests(PostgresFixture postgres) : ApiTestBase(postgres)
{
    [Fact]
    public async Task V1_ReportsTheSupportedVersions()
    {
        using var response = await GetAsync($"{V1}/patients/{Guid.NewGuid()}");

        Assert.Equal("1.0", Assert.Single(response.Headers.GetValues("api-supported-versions")));
    }

    [Fact]
    public async Task AnUnknownVersion_IsNotFoundAsProblemDetails()
    {
        using var response = await GetAsync($"/api/v2/patients/{Guid.NewGuid()}");

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "Http.NotFound");
    }
}

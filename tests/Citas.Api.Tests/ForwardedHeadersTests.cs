using System.Net;
using System.Net.Http.Json;
using Citas.Api.Tests.Support;
using Citas.Testing;

namespace Citas.Api.Tests;

/// <summary>
/// Behind a proxy every request reaches the API from the proxy's address, so the login limit would share one quota among all
/// clients. With <c>ForwardedHeaders:Enabled</c> the quota follows the client address that the proxy puts in X-Forwarded-For.
/// </summary>
public abstract class ForwardedHeadersTestBase(PostgresFixture postgres, bool enabled) : ApiTestBase(postgres)
{
    protected override CitasApiFactory CreateFactory(string connectionString) =>
        new(connectionString, new Dictionary<string, string> { ["ForwardedHeaders:Enabled"] = enabled.ToString() });

    /// <summary>One wrong login attempt that says it was forwarded for <paramref name="forwardedFor"/>.</summary>
    protected async Task<HttpStatusCode> AttemptAsync(string forwardedFor)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{V1}/auth/token")
        {
            Content = JsonContent.Create(new { email = "nadie@tests.local", password = "mala" }, options: Json.Options),
        };
        request.Headers.Add("X-Forwarded-For", forwardedFor);
        using var response = await Anonymous.SendAsync(request, Cancellation);
        return response.StatusCode;
    }

    protected async Task<HttpStatusCode> ExhaustAsync(string forwardedFor)
    {
        for (int attempt = 1; attempt <= 5; attempt++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, await AttemptAsync(forwardedFor));
        }

        return await AttemptAsync(forwardedFor);
    }
}

public class ForwardedHeadersEnabledTests(PostgresFixture postgres) : ForwardedHeadersTestBase(postgres, enabled: true)
{
    [Fact]
    public async Task TwoClientAddresses_HaveTheirOwnQuota()
    {
        Assert.Equal(HttpStatusCode.TooManyRequests, await ExhaustAsync("203.0.113.10"));

        Assert.Equal(HttpStatusCode.Unauthorized, await AttemptAsync("203.0.113.11"));
        Assert.Equal(HttpStatusCode.TooManyRequests, await AttemptAsync("203.0.113.10"));
    }

    [Fact]
    public async Task OnlyTheEntryOfTheNearestProxyIsTrusted_SoAClientCannotInventAddresses()
    {
        // The proxy appends the real client address at the end; whatever the client wrote before it is ignored.
        Assert.Equal(HttpStatusCode.TooManyRequests, await ExhaustAsync("1.1.1.1, 203.0.113.10"));

        Assert.Equal(HttpStatusCode.TooManyRequests, await AttemptAsync("2.2.2.2, 203.0.113.10"));
        Assert.Equal(HttpStatusCode.Unauthorized, await AttemptAsync("2.2.2.2, 203.0.113.12"));
    }
}

public class ForwardedHeadersDisabledTests(PostgresFixture postgres) : ForwardedHeadersTestBase(postgres, enabled: false)
{
    [Fact]
    public async Task TheHeaderIsIgnored_AndAllClientsShareTheQuotaOfTheConnection()
    {
        Assert.Equal(HttpStatusCode.TooManyRequests, await ExhaustAsync("203.0.113.10"));

        Assert.Equal(HttpStatusCode.TooManyRequests, await AttemptAsync("203.0.113.11"));
    }
}

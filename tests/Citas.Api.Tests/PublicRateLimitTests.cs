using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Citas.Api.Tests.Support;
using Citas.Domain.Staff;
using Citas.Testing;

namespace Citas.Api.Tests;

/// <summary>The two public reads (free times and the list of professionals) share a quota of 60 requests per minute per address.</summary>
public class PublicRateLimitTests(PostgresFixture postgres) : ApiTestBase(postgres)
{
    private const int Limit = 60;

    private Task<HttpResponseMessage> SlotsAsync(Guid professionalId) =>
        Anonymous.GetAsync($"{V1}/professionals/{professionalId}/available-slots?date=2026-12-03", Cancellation);

    private Task<HttpResponseMessage> ListAsync() => Anonymous.GetAsync($"{V1}/professionals", Cancellation);

    private async Task SpendAsync(Func<Task<HttpResponseMessage>> call, int times)
    {
        for (int i = 1; i <= times; i++)
        {
            using HttpResponseMessage response = await call();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task TheSixtyFirstFreeTimesRequest_Is429WithRetryAfter_AndTheSixtiethStillWorks()
    {
        Guid professional = await RegisterProfessionalAsync();
        await SpendAsync(() => SlotsAsync(professional), Limit);

        using var rejected = await SlotsAsync(professional);

        JsonElement problem = await AssertProblemAsync(rejected, HttpStatusCode.TooManyRequests, "Request.TooManyRequests");
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("detail").GetString()));
        string retryAfter = Assert.Single(rejected.Headers.GetValues("Retry-After"));
        Assert.InRange(int.Parse(retryAfter, CultureInfo.InvariantCulture), 1, 60);
    }

    [Fact]
    public async Task TheListOfProfessionals_HasTheSameLimit()
    {
        await SpendAsync(ListAsync, Limit);

        using var rejected = await ListAsync();

        await AssertProblemAsync(rejected, HttpStatusCode.TooManyRequests, "Request.TooManyRequests");
        Assert.True(rejected.Headers.Contains("Retry-After"));
    }

    [Fact]
    public async Task BothReadsShareOneQuota()
    {
        Guid professional = await RegisterProfessionalAsync();
        await SpendAsync(() => SlotsAsync(professional), Limit / 2);
        await SpendAsync(ListAsync, Limit / 2);

        using var bySlots = await SlotsAsync(professional);
        using var byList = await ListAsync();

        Assert.Equal(HttpStatusCode.TooManyRequests, bySlots.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, byList.StatusCode);
    }

    [Fact]
    public async Task ARequestThatIsRefused_IsNotAnswered_WithTheLoginCode()
    {
        await SpendAsync(ListAsync, Limit);

        using var rejected = await ListAsync();

        JsonElement problem = await rejected.ReadAsync();
        Assert.NotEqual("Auth.TooManyAttempts", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task ExhaustingThePublicQuota_DoesNotBlockSigningIn_NorTheStaffEndpoints()
    {
        await Factory.AddUserAsync("recepcion@tests.local", "Una-clave-larga-1", StaffRole.Reception);
        Guid professional = await RegisterProfessionalAsync();
        await SpendAsync(ListAsync, Limit);

        using var signIn = await Anonymous.PostAsJsonAsync($"{V1}/auth/token", new { email = "recepcion@tests.local", password = "Una-clave-larga-1" }, Json.Options, Cancellation);
        using var staff = await GetAsync($"{V1}/professionals/{professional}");

        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        Assert.Equal(HttpStatusCode.OK, staff.StatusCode);
    }

    [Fact]
    public async Task ExhaustingTheLoginQuota_DoesNotBlockTheFreeTimes()
    {
        Guid professional = await RegisterProfessionalAsync();
        for (int attempt = 1; attempt <= 6; attempt++)
        {
            using var _ = await Anonymous.PostAsJsonAsync($"{V1}/auth/token", new { email = "nadie@tests.local", password = "mala" }, Json.Options, Cancellation);
        }

        using var slots = await SlotsAsync(professional);

        Assert.Equal(HttpStatusCode.OK, slots.StatusCode);
    }

    [Fact]
    public async Task TheStaffEndpoints_AreNotLimitedByThePublicRule()
    {
        Guid professional = await RegisterProfessionalAsync();

        await SpendAsync(() => GetAsync($"{V1}/professionals/{professional}"), Limit + 5);
    }
}

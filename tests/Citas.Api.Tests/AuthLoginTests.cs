using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Citas.Api.Tests.Support;
using Citas.Domain.Staff;
using Citas.Testing;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Citas.Api.Tests;

public class AuthLoginTests(PostgresFixture postgres) : ApiTestBase(postgres)
{
    private const string Password = "Clave-de-pruebas-1";
    private const string Login = $"{V1}/auth/token";

    private Task<HttpResponseMessage> LoginAsync(string email, string password) =>
        Anonymous.PostAsJsonAsync(Login, new { email, password }, Json.Options, Cancellation);

    private static JsonWebToken Read(string token) => new JsonWebTokenHandler().ReadJsonWebToken(token);

    [Fact]
    public async Task Login_ReturnsATokenWithTheRightClaimsAndExpiry()
    {
        StaffUser user = await Factory.AddUserAsync("recepcion@tests.local", Password, StaffRole.Reception);

        using var response = await LoginAsync("recepcion@tests.local", Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement body = await response.ReadAsync();
        Assert.Equal("Reception", body.GetProperty("role").GetString());
        JsonWebToken token = Read(body.GetProperty("accessToken").GetString()!);
        Assert.Equal("HS256", token.Alg);
        Assert.Equal("citas-api", token.Issuer);
        Assert.Contains("citas-clients", token.Audiences);
        Assert.Equal(user.Id.ToString(), token.GetClaim("sub").Value);
        Assert.Equal("recepcion@tests.local", token.GetClaim("email").Value);
        Assert.Equal("Reception", token.GetClaim("role").Value);
        Assert.False(token.TryGetClaim("professional_id", out _));
        Assert.Equal(CitasApiFactory.StartOfTime.AddMinutes(60), new DateTimeOffset(token.ValidTo, TimeSpan.Zero));
        Assert.Equal(CitasApiFactory.StartOfTime, new DateTimeOffset(token.ValidFrom, TimeSpan.Zero));
        Assert.Equal(CitasApiFactory.StartOfTime.AddMinutes(60), body.GetProperty("expiresAt").GetDateTimeOffset());
    }

    [Fact]
    public async Task Login_AsAProfessional_PutsTheProfessionalIdInTheToken()
    {
        Guid professionalId = await RegisterProfessionalAsync();
        await Factory.AddUserAsync("ana@tests.local", Password, StaffRole.Professional, professionalId);

        using var response = await LoginAsync("ana@tests.local", Password);

        JsonElement body = await response.ReadAsync();
        Assert.Equal("Professional", body.GetProperty("role").GetString());
        Assert.Equal(professionalId.ToString(), Read(body.GetProperty("accessToken").GetString()!).GetClaim("professional_id").Value);
    }

    [Fact]
    public async Task TheTokenOfALogin_OpensTheDoorsOfItsRole_AndOnlyThose()
    {
        Guid professionalId = await RegisterProfessionalAsync();
        await Factory.AddUserAsync("recepcion@tests.local", Password, StaffRole.Reception);
        using var login = await LoginAsync("recepcion@tests.local", Password);
        string token = (await login.ReadAsync()).GetProperty("accessToken").GetString()!;
        using var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        using var allowed = await client.GetAsync($"{V1}/patients/{Guid.NewGuid()}", Cancellation);
        using var denied = await client.PostAsJsonAsync($"{V1}/professionals/{professionalId}/deactivate", new { }, Json.Options, Cancellation);

        Assert.Equal(HttpStatusCode.NotFound, allowed.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [Theory]
    [InlineData("RECEPCION@Tests.Local")]
    [InlineData("  recepcion@tests.local  ")]
    public async Task Login_IgnoresCaseAndSurroundingSpacesInTheEmail(string typed)
    {
        await Factory.AddUserAsync("recepcion@tests.local", Password, StaffRole.Reception);

        using var response = await LoginAsync(typed, Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task EveryWayOfFailing_GivesTheVeryBodyAnd401()
    {
        await Factory.AddUserAsync("recepcion@tests.local", Password, StaffRole.Reception);
        await Factory.AddUserAsync("baja@tests.local", Password, StaffRole.Reception, active: false);

        using var wrongPassword = await LoginAsync("recepcion@tests.local", "otra-clave");
        using var unknown = await LoginAsync("nadie@tests.local", Password);
        using var inactive = await LoginAsync("baja@tests.local", Password);

        JsonElement a = await AssertProblemAsync(wrongPassword, HttpStatusCode.Unauthorized, "Auth.InvalidCredentials");
        JsonElement b = await AssertProblemAsync(unknown, HttpStatusCode.Unauthorized, "Auth.InvalidCredentials");
        JsonElement c = await AssertProblemAsync(inactive, HttpStatusCode.Unauthorized, "Auth.InvalidCredentials");
        foreach (string field in new[] { "type", "title", "status", "detail", "code" })
        {
            Assert.Equal(a.GetProperty(field).ToString(), b.GetProperty(field).ToString());
            Assert.Equal(a.GetProperty(field).ToString(), c.GetProperty(field).ToString());
        }

        string detail = a.GetProperty("detail").GetString()!;
        Assert.DoesNotContain("recepcion", detail, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("exist", detail, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("inactive", detail, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("", Password)]
    [InlineData("recepcion@tests.local", "")]
    public async Task Login_WithAMissingField_Is400(string email, string password)
    {
        using var response = await LoginAsync(email, password);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "Validation");
    }

    [Fact]
    public async Task Login_WithABrokenBody_Is400()
    {
        using var response = await Anonymous.PostAsync(Login, new StringContent("{ no es json", System.Text.Encoding.UTF8, "application/json"), Cancellation);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "Request.InvalidBody");
    }

    [Fact]
    public async Task Login_DoesNotLeakThePasswordOrTheHashInAnyAnswer()
    {
        StaffUser user = await Factory.AddUserAsync("recepcion@tests.local", Password, StaffRole.Reception);

        using var ok = await LoginAsync("recepcion@tests.local", Password);
        using var bad = await LoginAsync("recepcion@tests.local", "mala");

        foreach (HttpResponseMessage response in new[] { ok, bad })
        {
            string text = await response.Content.ReadAsStringAsync(Cancellation);
            Assert.DoesNotContain(Password, text, StringComparison.Ordinal);
            Assert.DoesNotContain(user.PasswordHash, text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task TheSixthAttemptInAMinute_Is429WithRetryAfter()
    {
        await Factory.AddUserAsync("recepcion@tests.local", Password, StaffRole.Reception);

        for (int attempt = 1; attempt <= 5; attempt++)
        {
            using var wrong = await LoginAsync("recepcion@tests.local", "mala-" + attempt);
            Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        }

        using var sixth = await LoginAsync("recepcion@tests.local", Password); // even the right password: the door is shut

        JsonElement problem = await AssertProblemAsync(sixth, HttpStatusCode.TooManyRequests, "Auth.TooManyAttempts");
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("detail").GetString()));
        string retryAfter = Assert.Single(sixth.Headers.GetValues("Retry-After"));
        Assert.InRange(int.Parse(retryAfter, System.Globalization.CultureInfo.InvariantCulture), 1, 60);
    }

    [Fact]
    public async Task TheFifthAttempt_StillGetsItsRealAnswer()
    {
        await Factory.AddUserAsync("recepcion@tests.local", Password, StaffRole.Reception);

        for (int attempt = 1; attempt <= 4; attempt++)
        {
            using var wrong = await LoginAsync("recepcion@tests.local", "mala-" + attempt);
            Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        }

        using var fifth = await LoginAsync("recepcion@tests.local", Password);

        Assert.Equal(HttpStatusCode.OK, fifth.StatusCode);
    }

    [Fact]
    public async Task TheLimit_OnlyTouchesTheLogin()
    {
        for (int attempt = 1; attempt <= 6; attempt++)
        {
            using var wrong = await LoginAsync("nadie@tests.local", "mala");
            _ = wrong.StatusCode;
        }

        using var other = await Client.GetAsync($"{V1}/patients/{Guid.NewGuid()}", Cancellation);
        using var health = await Anonymous.GetAsync("/health", Cancellation);

        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }

    // ---- what the API does with tokens ----

    private async Task<string> LoginTokenAsync()
    {
        await Factory.AddUserAsync("recepcion@tests.local", Password, StaffRole.Reception);
        using var response = await LoginAsync("recepcion@tests.local", Password);
        return (await response.ReadAsync()).GetProperty("accessToken").GetString()!;
    }

    private async Task<HttpStatusCode> CallWithAsync(string token)
    {
        using var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        using var response = await client.GetAsync($"{V1}/patients/{Guid.NewGuid()}", Cancellation);
        return response.StatusCode;
    }

    [Fact]
    public async Task AnExpiredToken_Is401_WithTheFakeClockMovedPastTheExpiry()
    {
        string token = await LoginTokenAsync();
        Assert.Equal(HttpStatusCode.NotFound, await CallWithAsync(token)); // valid: passes authorization, the patient is unknown

        Factory.Time.Advance(TimeSpan.FromMinutes(60) + TimeSpan.FromSeconds(29)); // 29 s past the expiry, inside the 30 s of skew
        Assert.Equal(HttpStatusCode.NotFound, await CallWithAsync(token));

        Factory.Time.Advance(TimeSpan.FromSeconds(2)); // 31 s past
        using var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        using var response = await client.GetAsync($"{V1}/patients/{Guid.NewGuid()}", Cancellation);
        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "Auth.Unauthorized");
        Assert.Contains(response.Headers.WwwAuthenticate, h => h.Scheme == "Bearer");
    }

    [Fact]
    public async Task ATokenFromTheFuture_IsNotAcceptedYet()
    {
        string token = TestTokens.Mint(Factory.SigningKey, Factory.Time.GetUtcNow().AddMinutes(10), role: "Admin");

        Assert.Equal(HttpStatusCode.Unauthorized, await CallWithAsync(token));

        Factory.Time.Advance(TimeSpan.FromMinutes(10));
        Assert.Equal(HttpStatusCode.NotFound, await CallWithAsync(token));
    }

    [Fact]
    public async Task ATokenSignedWithAnotherKey_Is401()
    {
        string forged = TestTokens.Mint(Convert.ToBase64String(new byte[48]), Factory.Time.GetUtcNow());

        Assert.Equal(HttpStatusCode.Unauthorized, await CallWithAsync(forged));
        Assert.Equal(HttpStatusCode.NotFound, await CallWithAsync(TestTokens.Mint(Factory.SigningKey, Factory.Time.GetUtcNow())));
    }

    [Fact]
    public async Task ATokenForAnotherIssuerOrAudience_Is401()
    {
        DateTimeOffset now = Factory.Time.GetUtcNow();

        Assert.Equal(HttpStatusCode.Unauthorized, await CallWithAsync(TestTokens.Mint(Factory.SigningKey, now, issuer: "otro-emisor")));
        Assert.Equal(HttpStatusCode.Unauthorized, await CallWithAsync(TestTokens.Mint(Factory.SigningKey, now, audience: "otra-audiencia")));
    }

    [Fact]
    public async Task AnUnsignedToken_Is401()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, await CallWithAsync(TestTokens.Unsigned(Factory.Time.GetUtcNow())));
    }

    [Fact]
    public async Task ATokenSignedWithAnotherAlgorithm_Is401()
    {
        string hs512 = TestTokens.Mint(Factory.SigningKey, Factory.Time.GetUtcNow(), algorithm: Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha512);

        Assert.Equal(HttpStatusCode.Unauthorized, await CallWithAsync(hs512));
    }

    [Fact]
    public async Task ATamperedToken_Is401()
    {
        string token = TestTokens.Mint(Factory.SigningKey, Factory.Time.GetUtcNow(), role: "Reception");
        string[] parts = token.Split('.');
        string payload = System.Text.Encoding.UTF8.GetString(Microsoft.IdentityModel.Tokens.Base64UrlEncoder.DecodeBytes(parts[1]));
        string forged = Microsoft.IdentityModel.Tokens.Base64UrlEncoder.Encode(payload.Replace("Reception", "Admin", StringComparison.Ordinal));

        Assert.Equal(HttpStatusCode.Unauthorized, await CallWithAsync($"{parts[0]}.{forged}.{parts[2]}"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("esto-no-es-un-token")]
    [InlineData("a.b.c")]
    public async Task GarbageInTheBearerHeader_Is401(string token)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, await CallWithAsync(token));
    }

    [Fact]
    public async Task AnotherAuthorizationScheme_Is401()
    {
        using var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Basic", Convert.ToBase64String("admin:admin"u8.ToArray()));

        using var response = await client.GetAsync($"{V1}/patients/{Guid.NewGuid()}", Cancellation);

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "Auth.Unauthorized");
    }

    [Fact]
    public async Task Health_AndDocs_NeedNoToken()
    {
        foreach (string url in new[] { "/health", "/health/live", "/docs", "/openapi/v1.json" })
        {
            using var response = await Anonymous.GetAsync(url, Cancellation);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}

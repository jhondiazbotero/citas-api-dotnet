using System.Net;
using System.Text.Json;
using Citas.Api.Tests.Support;
using Citas.Testing;

namespace Citas.Api.Tests;

public class DocsTests(PostgresFixture postgres) : ApiTestBase(postgres)
{
    private async Task<JsonElement> OpenApiAsync()
    {
        using var response = await Anonymous.GetAsync("/openapi/v1.json", Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadAsync();
    }

    [Fact]
    public async Task Docs_ServesTheScalarPage()
    {
        using var response = await Anonymous.GetAsync("/docs", Cancellation);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Root_RedirectsToDocs()
    {
        using var noRedirects = Factory.CreateClient(new() { AllowAutoRedirect = false });

        using var response = await noRedirects.GetAsync("/", Cancellation);

        Assert.True(response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.TemporaryRedirect or HttpStatusCode.MovedPermanently);
        Assert.Equal("/docs", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task OpenApi_DescribesTheApiAndEveryPath()
    {
        JsonElement document = await OpenApiAsync();

        JsonElement info = document.GetProperty("info");
        Assert.Equal("Citas API", info.GetProperty("title").GetString());
        Assert.False(string.IsNullOrWhiteSpace(info.GetProperty("description").GetString()));

        string[] expected =
        [
            "/api/v1/auth/token",
            "/api/v1/patients",
            "/api/v1/patients/{id}",
            "/api/v1/professionals",
            "/api/v1/professionals/{id}",
            "/api/v1/professionals/{id}/working-hours",
            "/api/v1/professionals/{id}/deactivate",
            "/api/v1/professionals/{id}/agenda",
            "/api/v1/professionals/{id}/available-slots",
            "/api/v1/appointments",
            "/api/v1/appointments/{id}",
            "/api/v1/appointments/{id}/reschedule",
            "/api/v1/appointments/{id}/cancel",
            "/api/v1/appointments/{id}/complete",
            "/api/v1/appointments/{id}/no-show",
        ];
        string[] actual = [.. document.GetProperty("paths").EnumerateObject().Select(p => p.Name)];

        Assert.Equal(expected.Order(), actual.Order());
    }

    [Fact]
    public async Task OpenApi_EveryOperationHasSummaryDescriptionAndProblemResponses()
    {
        JsonElement document = await OpenApiAsync();

        foreach (JsonProperty path in document.GetProperty("paths").EnumerateObject())
        {
            foreach (JsonProperty operation in path.Value.EnumerateObject())
            {
                string where = $"{operation.Name.ToUpperInvariant()} {path.Name}";
                Assert.False(string.IsNullOrWhiteSpace(operation.Value.GetProperty("summary").GetString()), where);
                Assert.False(string.IsNullOrWhiteSpace(operation.Value.GetProperty("description").GetString()), where);
                JsonElement responses = operation.Value.GetProperty("responses");
                Assert.True(responses.TryGetProperty("404", out _) || responses.TryGetProperty("400", out _), where);
            }
        }
    }

    [Fact]
    public async Task OpenApi_BookingDocumentsItsPossibleResponses()
    {
        JsonElement document = await OpenApiAsync();

        JsonElement post = document.GetProperty("paths").GetProperty("/api/v1/appointments").GetProperty("post");
        string[] codes = [.. post.GetProperty("responses").EnumerateObject().Select(r => r.Name)];

        Assert.Equal(["201", "400", "401", "403", "404", "409", "422"], codes.Order());
        Assert.True(post.GetProperty("responses").GetProperty("409").GetProperty("content").TryGetProperty("application/problem+json", out _));
    }

    [Fact]
    public async Task OpenApi_ExplainsThatDatesAreLocalWithoutOffset()
    {
        JsonElement document = await OpenApiAsync();

        string text = document.GetRawText();

        Assert.Contains("without offset", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\"date-time\"", document.GetProperty("paths").GetRawText(), StringComparison.Ordinal);

        // The only instant in the API is the token expiry, an absolute moment (UTC with offset), not clinic time.
        JsonElement schemas = document.GetProperty("components").GetProperty("schemas");
        Assert.Equal("date-time", schemas.GetProperty("LoginResponse").GetProperty("properties").GetProperty("expiresAt").GetProperty("format").GetString());
        string others = string.Concat(schemas.EnumerateObject().Where(s => s.Name != "LoginResponse").Select(s => s.Value.GetRawText()));
        Assert.DoesNotContain("\"date-time\"", others, StringComparison.Ordinal);
        Assert.DoesNotContain("{version}", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenApi_DocumentsTheErrorCodeExtension()
    {
        JsonElement document = await OpenApiAsync();

        JsonElement problem = document.GetProperty("components").GetProperty("schemas").GetProperty("ProblemDetails");

        Assert.True(problem.GetProperty("properties").TryGetProperty("code", out _));
    }

    [Fact]
    public async Task OpenApi_MarksOptionalFieldsAsOptional()
    {
        JsonElement document = await OpenApiAsync();
        JsonElement schemas = document.GetProperty("components").GetProperty("schemas");

        string[] schedule = [.. schemas.GetProperty("ScheduleAppointmentRequest").GetProperty("required").EnumerateArray().Select(e => e.GetString()!)];
        string[] patient = [.. schemas.GetProperty("RegisterPatientRequest").GetProperty("required").EnumerateArray().Select(e => e.GetString()!)];

        Assert.DoesNotContain("reason", schedule);
        Assert.Contains("start", schedule);
        Assert.DoesNotContain("phone", patient);
    }

    [Fact]
    public async Task OpenApi_ShipsExamplesForTheMainRequests()
    {
        JsonElement document = await OpenApiAsync();

        string text = document.GetProperty("components").GetProperty("schemas").GetRawText();

        Assert.Contains("\"examples\"", text, StringComparison.Ordinal);
    }
}

public class DocsSecurityTests(PostgresFixture postgres) : ApiTestBase(postgres)
{
    private async Task<JsonElement> OpenApiAsync()
    {
        using var response = await Anonymous.GetAsync("/openapi/v1.json", Cancellation);
        return await response.ReadAsync();
    }

    [Fact]
    public async Task OpenApi_DeclaresTheBearerScheme_SoTheDocsCanTakeAToken()
    {
        JsonElement document = await OpenApiAsync();

        JsonElement bearer = document.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer");

        Assert.Equal("http", bearer.GetProperty("type").GetString());
        Assert.Equal("bearer", bearer.GetProperty("scheme").GetString());
        Assert.Equal("JWT", bearer.GetProperty("bearerFormat").GetString());
    }

    [Fact]
    public async Task OpenApi_ProtectedOperationsRequireTheScheme_AndPublicOnesDoNot()
    {
        JsonElement document = await OpenApiAsync();
        string[] publicOperations = ["post /api/v1/auth/token", "get /api/v1/professionals/{id}/available-slots"];

        foreach (JsonProperty path in document.GetProperty("paths").EnumerateObject())
        {
            foreach (JsonProperty operation in path.Value.EnumerateObject())
            {
                string where = $"{operation.Name} {path.Name}";
                bool hasSecurity = operation.Value.TryGetProperty("security", out JsonElement security) && security.GetArrayLength() > 0;
                Assert.Equal(!publicOperations.Contains(where), hasSecurity);
            }
        }
    }

    [Fact]
    public async Task OpenApi_EveryOperationSaysWhoMayCallIt()
    {
        JsonElement document = await OpenApiAsync();
        var access = new Dictionary<string, string>();

        foreach (JsonProperty path in document.GetProperty("paths").EnumerateObject())
        {
            foreach (JsonProperty operation in path.Value.EnumerateObject())
            {
                string description = operation.Value.GetProperty("description").GetString()!;
                Assert.Contains("**Access:**", description, StringComparison.Ordinal);
                access[$"{operation.Name} {path.Name}"] = description[description.IndexOf("**Access:**", StringComparison.Ordinal)..];
            }
        }

        Assert.Contains("Public", access["post /api/v1/auth/token"], StringComparison.Ordinal);
        Assert.Contains("Public", access["get /api/v1/professionals/{id}/available-slots"], StringComparison.Ordinal);
        Assert.EndsWith("Admin.", access["post /api/v1/professionals"], StringComparison.Ordinal);
        Assert.EndsWith("Admin or Reception.", access["post /api/v1/patients"], StringComparison.Ordinal);
        Assert.Contains("any other professional gets 403", access["get /api/v1/professionals/{id}/agenda"], StringComparison.Ordinal);
        Assert.Contains("any other professional gets 403", access["get /api/v1/professionals/{id}"], StringComparison.Ordinal);
        Assert.Contains("Professional who owns the appointment", access["post /api/v1/appointments/{id}/complete"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenApi_ProtectedOperationsDocument401And403_AndLoginDocuments401And429()
    {
        JsonElement document = await OpenApiAsync();
        JsonElement paths = document.GetProperty("paths");

        JsonElement cancel = paths.GetProperty("/api/v1/appointments/{id}/cancel").GetProperty("post").GetProperty("responses");
        JsonElement login = paths.GetProperty("/api/v1/auth/token").GetProperty("post").GetProperty("responses");

        Assert.True(cancel.TryGetProperty("401", out _));
        Assert.True(cancel.TryGetProperty("403", out _));
        Assert.True(login.TryGetProperty("200", out _));
        Assert.True(login.TryGetProperty("401", out _));
        Assert.True(login.TryGetProperty("429", out _));
        Assert.False(login.TryGetProperty("403", out _));
    }

    [Fact]
    public async Task OpenApi_LoginHasAnExample_AndNoSecretIsInIt()
    {
        JsonElement document = await OpenApiAsync();
        JsonElement schemas = document.GetProperty("components").GetProperty("schemas");

        string request = schemas.GetProperty("LoginRequest").GetRawText();

        Assert.Contains("\"examples\"", request, StringComparison.Ordinal);
        Assert.Contains("recepcion@demo.local", request, StringComparison.Ordinal);
        Assert.True(schemas.GetProperty("LoginResponse").GetProperty("properties").TryGetProperty("accessToken", out _));
    }

    [Fact]
    public async Task OpenApi_CancelUsesOnBehalfOfPatient_NotByClinic()
    {
        JsonElement document = await OpenApiAsync();

        string cancel = document.GetProperty("components").GetProperty("schemas").GetProperty("CancelAppointmentRequest").GetRawText();

        Assert.Contains("onBehalfOfPatient", cancel, StringComparison.Ordinal);
        Assert.DoesNotContain("byClinic", cancel, StringComparison.Ordinal);
    }
}

public class DocsDisabledTests(PostgresFixture postgres) : ApiTestBase(postgres)
{
    protected override CitasApiFactory CreateFactory(string connectionString) =>
        new(connectionString, new Dictionary<string, string> { ["Docs:Enabled"] = "false" });

    [Theory]
    [InlineData("/docs")]
    [InlineData("/openapi/v1.json")]
    [InlineData("/")]
    public async Task WhenDisabled_NothingIsPublished(string url)
    {
        using var noRedirects = Factory.CreateClient(new() { AllowAutoRedirect = false });

        using var response = await noRedirects.GetAsync(url, Cancellation);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

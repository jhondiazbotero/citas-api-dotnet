using System.Net;
using System.Text.Json;
using Citas.Api.Tests.Support;
using Citas.Testing;

namespace Citas.Api.Tests;

public class DocsTests(PostgresFixture postgres) : ApiTestBase(postgres)
{
    private async Task<JsonElement> OpenApiAsync()
    {
        using var response = await GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadAsync();
    }

    [Fact]
    public async Task Docs_ServesTheScalarPage()
    {
        using var response = await GetAsync("/docs");

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

        Assert.Equal(["201", "400", "404", "409", "422"], codes.Order());
        Assert.True(post.GetProperty("responses").GetProperty("409").GetProperty("content").TryGetProperty("application/problem+json", out _));
    }

    [Fact]
    public async Task OpenApi_ExplainsThatDatesAreLocalWithoutOffset()
    {
        JsonElement document = await OpenApiAsync();

        string text = document.GetRawText();

        Assert.Contains("without offset", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\"date-time\"", text, StringComparison.Ordinal);
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

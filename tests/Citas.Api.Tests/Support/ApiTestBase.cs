using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Citas.Testing;

namespace Citas.Api.Tests.Support;

/// <summary>One database, one API instance and one client per test; builds the data the scenarios need through the API itself.</summary>
[Collection(ApiCollection.Name)]
public abstract class ApiTestBase(PostgresFixture postgres) : IAsyncLifetime
{
    protected const string V1 = "/api/v1";

    private string _connectionString = string.Empty;

    protected CitasApiFactory Factory { get; private set; } = null!;

    protected HttpClient Client { get; private set; } = null!;

    protected CancellationToken Cancellation => TestContext.Current.CancellationToken;

    /// <summary>Override to start the API with other settings or services.</summary>
    protected virtual CitasApiFactory CreateFactory(string connectionString) => new(connectionString);

    public async ValueTask InitializeAsync()
    {
        _connectionString = await postgres.CreateDatabaseAsync();
        Factory = CreateFactory(_connectionString);
        Client = Factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await Factory.DisposeAsync();
        await postgres.DropDatabaseAsync(_connectionString);
    }

    protected static string Local(int day, int hour, int minute = 0) => $"2026-12-{day:00}T{hour:00}:{minute:00}:00";

    protected Task<HttpResponseMessage> PostAsync(string url, object body) =>
        Client.PostAsJsonAsync(url, body, Json.Options, Cancellation);

    protected Task<HttpResponseMessage> PostRawAsync(string url, string json, string mediaType = "application/json") =>
        Client.PostAsync(url, new StringContent(json, Encoding.UTF8, mediaType), Cancellation);

    protected Task<HttpResponseMessage> GetAsync(string url) => Client.GetAsync(url, Cancellation);

    protected static object[] WeekdayHours() =>
    [
        .. new[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday" }.SelectMany(day => new object[]
        {
            new { day, from = "08:00", to = "12:00" },
            new { day, from = "14:00", to = "18:00" },
        }),
    ];

    protected async Task<Guid> RegisterProfessionalAsync(string name = "Dra. Ana Prueba")
    {
        using var response = await PostAsync($"{V1}/professionals", new { fullName = name, specialty = "Medicina general", workingHours = WeekdayHours() });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadIdAsync();
    }

    protected async Task<Guid> RegisterPatientAsync(string document = "1234567", string name = "Luis Prueba")
    {
        using var response = await PostAsync($"{V1}/patients", new { fullName = name, documentNumber = document, phone = "3001234567" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadIdAsync();
    }

    protected Task<HttpResponseMessage> ScheduleAsync(Guid professionalId, Guid patientId, string start, int minutes = 30) =>
        PostAsync($"{V1}/appointments", new { professionalId, patientId, start, durationMinutes = minutes, reason = "Control" });

    protected async Task<Guid> BookAsync(Guid professionalId, Guid patientId, string start, int minutes = 30)
    {
        using var response = await ScheduleAsync(professionalId, patientId, start, minutes);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadIdAsync();
    }

    protected async Task<string[]> FreeStartsAsync(Guid professionalId, int day, int minutes = 30)
    {
        using var response = await GetAsync($"{V1}/professionals/{professionalId}/available-slots?date=2026-12-{day:00}&durationMinutes={minutes}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement body = await response.ReadAsync();
        return [.. body.EnumerateArray().Select(slot => slot.GetProperty("start").GetString()!)];
    }

    /// <summary>The response is RFC 9457 problem details with the expected status and domain code.</summary>
    protected static async Task<JsonElement> AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        JsonElement problem = await response.ReadAsync();
        Assert.Equal((int)status, problem.GetProperty("status").GetInt32());
        Assert.Equal(code, problem.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("type").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("title").GetString()));
        return problem;
    }
}

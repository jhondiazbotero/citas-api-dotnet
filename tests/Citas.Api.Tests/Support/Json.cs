using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Citas.Api.Http;

namespace Citas.Api.Tests.Support;

/// <summary>Same JSON rules as the API, so the tests send and read what a real client would.</summary>
internal static class Json
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new ClinicDateTimeConverter(), new JsonStringEnumConverter() },
    };

    public static async Task<JsonElement> ReadAsync(this HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Options, TestContext.Current.CancellationToken));

    public static async Task<Guid> ReadIdAsync(this HttpResponseMessage response) =>
        (await response.ReadAsync()).GetProperty("id").GetGuid();
}

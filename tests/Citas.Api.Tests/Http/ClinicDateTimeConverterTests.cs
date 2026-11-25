using System.Text.Json;
using Citas.Api.Http;

namespace Citas.Api.Tests.Http;

public class ClinicDateTimeConverterTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { Converters = { new ClinicDateTimeConverter() } };

    private sealed record Body(DateTime Start, DateTime? End = null);

    private static Body Read(string value) =>
        JsonSerializer.Deserialize<Body>($$"""{"start":"{{value}}"}""", Options)!;

    [Theory]
    [InlineData("2026-12-01T09:30", 9, 30, 0)]
    [InlineData("2026-12-01T09:30:45", 9, 30, 45)]
    [InlineData("2026-12-01T00:00", 0, 0, 0)]
    public void Read_AcceptsLocalTimeWithoutZone(string text, int hour, int minute, int second)
    {
        DateTime value = Read(text).Start;

        Assert.Equal(new DateTime(2026, 12, 1, hour, minute, second), value);
        Assert.Equal(DateTimeKind.Unspecified, value.Kind);
    }

    [Theory]
    [InlineData("2026-12-01T09:30:00Z")]
    [InlineData("2026-12-01T09:30Z")]
    [InlineData("2026-12-01T09:30:00+00:00")]
    [InlineData("2026-12-01T09:30:00-05:00")]
    [InlineData("2026-12-01T09:30-0500")]
    [InlineData("2026-12-01T09:30:00z")]
    public void Read_RejectsZoneOrOffset_WithAClearMessage(string text)
    {
        var error = Assert.Throws<JsonException>(() => Read(text));

        Assert.Contains("Send local clinic time without offset", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("2026-12-01")]
    [InlineData("2026-12-01 09:30")]
    [InlineData("2026-12-01T09")]
    [InlineData("2026-12-01T09:30:00.123")]
    [InlineData("01/12/2026 09:30")]
    [InlineData("not a date")]
    [InlineData("")]
    public void Read_RejectsOtherFormats_NamingTheExpectedOne(string text)
    {
        var error = Assert.Throws<JsonException>(() => Read(text));

        Assert.Contains("yyyy-MM-ddTHH:mm", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_RejectsNonStringTokens()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Body>("""{"start":20261201}""", Options));
    }

    [Fact]
    public void Write_UsesLocalTimeWithoutZone()
    {
        var body = new Body(new DateTime(2026, 12, 1, 9, 30, 0, DateTimeKind.Utc), new DateTime(2026, 12, 1, 10, 0, 0));

        string json = JsonSerializer.Serialize(body, Options);

        Assert.Contains("\"start\":\"2026-12-01T09:30:00\"", json, StringComparison.Ordinal);
        Assert.Contains("\"end\":\"2026-12-01T10:00:00\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Nullable_NullRoundTrips()
    {
        string json = JsonSerializer.Serialize(new Body(new DateTime(2026, 12, 1, 9, 30, 0)), Options);

        Assert.Contains("\"end\":null", json, StringComparison.Ordinal);
        Assert.Null(JsonSerializer.Deserialize<Body>(json, Options)!.End);
    }
}

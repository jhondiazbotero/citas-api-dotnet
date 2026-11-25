using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Citas.Api.Http;

/// <summary>
/// The API speaks the clinic's local time. It reads and writes <c>yyyy-MM-ddTHH:mm[:ss]</c> and refuses any value that
/// carries a zone (<c>Z</c> or an offset), because silently converting it would move the appointment to another hour.
/// </summary>
internal sealed partial class ClinicDateTimeConverter : JsonConverter<DateTime>
{
    public const string Format = "yyyy-MM-ddTHH:mm[:ss]";

    public const string OffsetMessage =
        "Send local clinic time without offset (yyyy-MM-ddTHH:mm[:ss]); values ending in Z or +hh:mm are not accepted.";

    public const string FormatMessage = "Dates must look like yyyy-MM-ddTHH:mm[:ss] in local clinic time.";

    private static readonly string[] Formats = ["yyyy-MM-dd'T'HH:mm", "yyyy-MM-dd'T'HH:mm:ss"];

    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException(FormatMessage);
        }

        string text = reader.GetString() ?? string.Empty;

        if (DateTime.TryParseExact(text, Formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime value))
        {
            return DateTime.SpecifyKind(value, DateTimeKind.Unspecified);
        }

        throw new JsonException(HasZone().IsMatch(text) ? OffsetMessage : FormatMessage);
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture));

    // A date-time with a time part that ends in Z or in an offset such as +00:00, -05:00 or -0500.
    [GeneratedRegex(@"T\d{2}:\d{2}(:\d{2}(\.\d+)?)?\s*([zZ]|[+-]\d{2}(:?\d{2})?)$")]
    private static partial Regex HasZone();
}

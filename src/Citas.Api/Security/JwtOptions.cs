using System.Text;

namespace Citas.Api.Security;

/// <summary>
/// Settings of the access tokens, from the <c>Jwt</c> section. The signing key has no default and never lives in a versioned
/// file: in Development it comes from user-secrets, elsewhere from the environment variable <c>Jwt__SigningKey</c>.
/// </summary>
internal sealed record JwtOptions(string Issuer, string Audience, string SigningKey, int ExpiresMinutes)
{
    public const string SectionName = "Jwt";
    public const string SigningKeyKey = "Jwt:SigningKey";
    public const int MinKeyBytes = 32;
    public const int DefaultExpiresMinutes = 60;
    public const int MaxExpiresMinutes = 24 * 60;

    public byte[] KeyBytes => Encoding.UTF8.GetBytes(SigningKey);

    /// <summary>The text of a record lists every property; the signing key must not be one of them (it would reach a log or an error page).</summary>
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"{nameof(Issuer)} = {Issuer}, {nameof(Audience)} = {Audience}, {nameof(ExpiresMinutes)} = {ExpiresMinutes}");
        return true;
    }

    /// <summary>Reads and checks the section. Throws <see cref="InvalidOperationException"/> with an actionable message, so the app does not start half configured.</summary>
    public static JwtOptions From(IConfiguration configuration)
    {
        string? key = configuration[SigningKeyKey];
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException(
                $"Missing configuration '{SigningKeyKey}'. Set a random secret of at least {MinKeyBytes} bytes: " +
                "in Development with 'dotnet user-secrets set \"Jwt:SigningKey\" \"<secret>\"', elsewhere with the environment variable Jwt__SigningKey.");
        }

        int bytes = Encoding.UTF8.GetByteCount(key);
        if (bytes < MinKeyBytes)
        {
            throw new InvalidOperationException(
                $"'{SigningKeyKey}' is too short: it has {bytes} bytes and at least {MinKeyBytes} are required.");
        }

        string issuer = Required(configuration, "Jwt:Issuer");
        string audience = Required(configuration, "Jwt:Audience");

        int minutes = configuration.GetValue<int?>("Jwt:ExpiresMinutes") ?? DefaultExpiresMinutes;
        if (minutes is < 1 or > MaxExpiresMinutes)
        {
            throw new InvalidOperationException($"'Jwt:ExpiresMinutes' must be between 1 and {MaxExpiresMinutes}, not {minutes}.");
        }

        return new JwtOptions(issuer, audience, key, minutes);
    }

    private static string Required(IConfiguration configuration, string name) =>
        configuration[name] is { Length: > 0 } value && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidOperationException($"Missing configuration '{name}'.");
}

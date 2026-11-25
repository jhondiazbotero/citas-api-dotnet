using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Citas.Api.Tests.Support;

/// <summary>Builds tokens by hand, to check what the API does with tokens it did NOT issue (other key, other issuer, odd claims).</summary>
internal static class TestTokens
{
    public static string Mint(
        string signingKey,
        DateTimeOffset now,
        string role = "Admin",
        Guid? professionalId = null,
        string issuer = "citas-api",
        string audience = "citas-clients",
        TimeSpan? lifetime = null,
        string algorithm = SecurityAlgorithms.HmacSha256)
    {
        var claims = new Dictionary<string, object>
        {
            ["sub"] = Guid.NewGuid().ToString(),
            ["email"] = "hecho-a-mano@tests.local",
            ["role"] = role,
        };
        if (professionalId is { } id)
        {
            claims["professional_id"] = id.ToString();
        }

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = now.Add(lifetime ?? TimeSpan.FromMinutes(30)).UtcDateTime,
            Claims = claims,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(signingKey)), algorithm),
        });
    }

    /// <summary>A token with no signature at all (<c>alg: none</c>).</summary>
    public static string Unsigned(DateTimeOffset now, string role = "Admin") =>
        new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "citas-api",
            Audience = "citas-clients",
            Expires = now.AddMinutes(30).UtcDateTime,
            Claims = new Dictionary<string, object> { ["sub"] = Guid.NewGuid().ToString(), ["role"] = role },
        });
}

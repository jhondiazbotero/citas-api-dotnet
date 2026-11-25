using Citas.Application.Abstractions;
using Citas.Domain.Staff;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Citas.Api.Security;

/// <summary>Signs access tokens (HS256). The clock is the <see cref="TimeProvider"/>, so tests can move time.</summary>
internal sealed class JwtTokenIssuer : ITokenIssuer
{
    public const string RoleClaim = "role";
    public const string ProfessionalClaim = "professional_id";

    private readonly JsonWebTokenHandler _handler = new();
    private readonly JwtOptions _options;
    private readonly TimeProvider _time;
    private readonly SigningCredentials _credentials;

    public JwtTokenIssuer(JwtOptions options, TimeProvider time)
    {
        _options = options;
        _time = time;
        _credentials = new SigningCredentials(new SymmetricSecurityKey(options.KeyBytes), SecurityAlgorithms.HmacSha256);
    }

    public AccessToken Issue(StaffUser user)
    {
        DateTimeOffset now = _time.GetUtcNow();
        DateTimeOffset expires = now.AddMinutes(_options.ExpiresMinutes);

        var claims = new Dictionary<string, object>
        {
            [JwtRegisteredClaimNames.Sub] = user.Id.ToString(),
            [JwtRegisteredClaimNames.Email] = user.Email,
            [RoleClaim] = user.Role.ToString(),
            [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString("N"),
        };
        if (user.ProfessionalId is { } professionalId)
        {
            claims[ProfessionalClaim] = professionalId.ToString();
        }

        string token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            Claims = claims,
            SigningCredentials = _credentials,
        });

        return new AccessToken(token, expires);
    }
}

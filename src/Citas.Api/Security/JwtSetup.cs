using Citas.Application.Abstractions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Citas.Api.Security;

internal static class JwtSetup
{
    public static readonly TimeSpan ClockSkew = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Bearer authentication, the issuer of tokens, the policies of authorization and the rate limits. Reads the <c>Jwt</c> section right away:
    /// a missing or short signing key stops the app from starting.
    /// </summary>
    public static IServiceCollection AddCitasSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        JwtOptions jwt = JwtOptions.From(configuration);

        services.AddSingleton(jwt);
        services.AddSingleton<ITokenIssuer, JwtTokenIssuer>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<TimeProvider>((options, time) =>
            {
                options.MapInboundClaims = false; // keep "sub", "email" and "role" as they are in the token
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(jwt.KeyBytes),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    RequireSignedTokens = true,
                    RequireExpirationTime = true,
                    ValidateLifetime = true,
                    ClockSkew = ClockSkew,
                    LifetimeValidator = (notBefore, expires, _, parameters) => IsInsideLifetime(time.GetUtcNow(), notBefore, expires, parameters.ClockSkew),
                    NameClaimType = JwtRegisteredClaimNames.Sub,
                    RoleClaimType = JwtTokenIssuer.RoleClaim,
                };
            });

        services.AddCitasAuthorization();
        services.AddCitasRateLimiting();

        return services;
    }

    private static bool IsInsideLifetime(DateTimeOffset now, DateTime? notBefore, DateTime? expires, TimeSpan skew)
    {
        if (expires is null)
        {
            return false;
        }

        DateTimeOffset end = new(DateTime.SpecifyKind(expires.Value, DateTimeKind.Utc));
        if (now > end + skew)
        {
            return false;
        }

        if (notBefore is { } start)
        {
            DateTimeOffset from = new(DateTime.SpecifyKind(start, DateTimeKind.Utc));
            return now >= from - skew;
        }

        return true;
    }
}

using System.Threading.RateLimiting;
using Citas.Api.Http;
using Citas.Application.Abstractions;
using Citas.Domain.Errors;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Citas.Api.Security;

internal static class JwtSetup
{
    public const string LoginRateLimit = "login";
    public const int LoginAttemptsPerWindow = 5;
    public static readonly TimeSpan LoginWindow = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan ClockSkew = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Bearer authentication, the issuer of tokens and the login rate limit. Reads the <c>Jwt</c> section right away:
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
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(LoginRateLimit, httpContext => RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = LoginAttemptsPerWindow,
                    Window = LoginWindow,
                    QueueLimit = 0,
                }));
            options.OnRejected = WriteTooManyAttemptsAsync;
        });

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

    private static async ValueTask WriteTooManyAttemptsAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        HttpContext http = context.HttpContext;
        int retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan wait)
            ? Math.Max(1, (int)Math.Ceiling(wait.TotalSeconds))
            : (int)LoginWindow.TotalSeconds;

        http.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        http.Response.Headers.RetryAfter = retryAfter.ToString(System.Globalization.CultureInfo.InvariantCulture);

        await http.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,

                // The framework has no default for 429, so the two standard members are filled in here.
                Type = "https://tools.ietf.org/html/rfc6585#section-4",
                Title = "Too Many Requests",
                Detail = AuthErrors.TooManyAttempts.Message,
                Extensions = { [ProblemResult.CodeExtension] = AuthErrors.TooManyAttempts.Code },
            },
        });
    }
}

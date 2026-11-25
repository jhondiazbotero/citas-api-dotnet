using System.Globalization;
using System.Threading.RateLimiting;
using Citas.Api.Http;
using Citas.Domain.Common;
using Citas.Domain.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Citas.Api.Security;

/// <summary>
/// Two fixed-window limits per client address, kept in the memory of each instance. The login allows 5 attempts a minute: it is
/// the door for guessing passwords. The two public reads (free times and the list of professionals) share 60 requests a minute:
/// anyone can call them, so nothing else stops a script from hammering the database.
/// </summary>
internal static class RateLimitingSetup
{
    public const string LoginRateLimit = "login";
    public const string PublicReadRateLimit = "public-read";
    public const int LoginAttemptsPerWindow = 5;
    public const int PublicReadRequestsPerWindow = 60;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    /// <summary>Code of the 429 of the public reads. The login keeps <c>Auth.TooManyAttempts</c>.</summary>
    public static readonly Error TooManyRequests =
        new("Request.TooManyRequests", "Too many requests. Wait before trying again.");

    public static IServiceCollection AddCitasRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(LoginRateLimit, http => PerAddress(http, LoginRateLimit, LoginAttemptsPerWindow));
            options.AddPolicy(PublicReadRateLimit, http => PerAddress(http, PublicReadRateLimit, PublicReadRequestsPerWindow));
            options.OnRejected = WriteTooManyRequestsAsync;
        });

        return services;
    }

    // The policy name goes into the key: each policy has its own counter for the same address.
    private static RateLimitPartition<string> PerAddress(HttpContext http, string policy, int permits) =>
        RateLimitPartition.GetFixedWindowLimiter(
            $"{policy}:{http.Connection.RemoteIpAddress?.ToString() ?? "unknown"}",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = permits, Window = Window, QueueLimit = 0 });

    private static async ValueTask WriteTooManyRequestsAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        HttpContext http = context.HttpContext;
        int retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan wait)
            ? Math.Max(1, (int)Math.Ceiling(wait.TotalSeconds))
            : (int)Window.TotalSeconds;

        string? policy = http.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName;
        Error error = policy == LoginRateLimit ? AuthErrors.TooManyAttempts : TooManyRequests;

        http.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        http.Response.Headers.RetryAfter = retryAfter.ToString(CultureInfo.InvariantCulture);

        await http.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,

                // The framework has no default for 429, so the two standard members are filled in here.
                Type = "https://tools.ietf.org/html/rfc6585#section-4",
                Title = "Too Many Requests",
                Detail = error.Message,
                Extensions = { [ProblemResult.CodeExtension] = error.Code },
            },
        });
    }
}

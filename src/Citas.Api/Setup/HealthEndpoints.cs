using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Citas.Api.Setup;

internal static class HealthEndpoints
{
    /// <summary>
    /// <c>/health/live</c>: the process answers. <c>/health/ready</c>: it can also reach PostgreSQL (503 otherwise).
    /// <c>/health</c> stays as an alias of live. The body lists check names and states only, never exception text or connection details.
    /// </summary>
    public static void MapHealth(this WebApplication app)
    {
        var live = new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains(ApiServices.LiveTag),
            ResponseWriter = WriteAsync,
        };
        var ready = new HealthCheckOptions { ResponseWriter = WriteAsync };

        app.MapHealthChecks("/health/live", live);
        app.MapHealthChecks("/health/ready", ready);
        app.MapHealthChecks("/health", live);
    }

    private static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        var body = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(entry => new { name = entry.Key, status = entry.Value.Status.ToString() }),
        };
        return context.Response.WriteAsync(JsonSerializer.Serialize(body, JsonSerializerOptions.Web));
    }
}

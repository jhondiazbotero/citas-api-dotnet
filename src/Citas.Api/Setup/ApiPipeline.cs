using Asp.Versioning;
using Asp.Versioning.Builder;
using Citas.Api.Endpoints;
using Citas.Api.OpenApi;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;

namespace Citas.Api.Setup;

internal static class ApiPipeline
{
    public const string DocsEnabledKey = "Docs:Enabled";
    public const string DocsPath = "/docs";

    /// <summary>
    /// Forwarded headers before anything else (when enabled), so the logs and the login limit see the client address.
    /// Request log next (so it sees the final status), then the exception handler and the status pages that write problem details
    /// (also for the empty 401 and 403 answers of authentication), the rate limiter, and who-you-are before what-you-may-do.
    /// </summary>
    public static WebApplication UseApi(this WebApplication app)
    {
        if (ForwardedHeadersSetup.IsEnabled(app.Configuration))
        {
            app.UseForwardedHeaders();
        }

        app.UseSerilogRequestLogging(options => options.GetLevel = RequestLogLevel);
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        return app;
    }

    public static WebApplication MapApi(this WebApplication app)
    {
        app.MapHealth();
        app.MapVersionedEndpoints();

        // Docs:Enabled defaults to true: it is a portfolio project, so the interface stays public in production too.
        if (app.Configuration.GetValue(DocsEnabledKey, defaultValue: true))
        {
            app.MapDocs();
        }

        return app;
    }

    private static void MapVersionedEndpoints(this WebApplication app)
    {
        IVersionedEndpointRouteBuilder versioned = app.NewVersionedApi("Citas");

        RouteGroupBuilder v1 = versioned
            .MapGroup("/api/v{version:apiVersion}")
            .HasApiVersion(new ApiVersion(1, 0));

        v1.MapAuthEndpoints();
        v1.MapPatientEndpoints();
        v1.MapProfessionalEndpoints();
        v1.MapAppointmentEndpoints();
        v1.MapStaffUserEndpoints();
    }

    private static void MapDocs(this WebApplication app)
    {
        app.MapOpenApi().WithDocumentPerVersion().AllowAnonymous();
        app.MapScalarApiReference(DocsPath, options =>
        {
            options.Title = "Citas API";
            // Nothing leaves the page: no telemetry, no AI chat, no MCP, no developer toolbar.
            options.Telemetry = false;
            options.Agent = new ScalarAgentOptions { Disabled = true };
            options.Mcp = new ScalarMcpOptions { Disabled = true };
            options.ShowDeveloperTools = DeveloperToolsVisibility.Never;
            options.AddPreferredSecuritySchemes(OpenApiSetup.BearerScheme);
        }).AllowAnonymous();

        app.MapGet("/", () => Results.Redirect(DocsPath)).AllowAnonymous().ExcludeFromDescription();
    }

    // Health probes run every few seconds; keep them out of the log unless something goes wrong.
    private static LogEventLevel RequestLogLevel(HttpContext context, double elapsedMs, Exception? exception) =>
        exception is not null || context.Response.StatusCode >= StatusCodes.Status500InternalServerError
            ? LogEventLevel.Error
            : context.Request.Path.StartsWithSegments("/health", StringComparison.Ordinal)
                ? LogEventLevel.Verbose
                : LogEventLevel.Information;
}

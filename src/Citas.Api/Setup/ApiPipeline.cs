using Asp.Versioning;
using Asp.Versioning.Builder;
using Citas.Api.Endpoints;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;

namespace Citas.Api.Setup;

internal static class ApiPipeline
{
    public const string DocsEnabledKey = "Docs:Enabled";
    public const string DocsPath = "/docs";

    /// <summary>Request log first (so it sees the final status), then the exception handler and the status pages that write problem details.</summary>
    public static WebApplication UseApi(this WebApplication app)
    {
        app.UseSerilogRequestLogging(options => options.GetLevel = RequestLogLevel);
        app.UseExceptionHandler();
        app.UseStatusCodePages();
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

        v1.MapPatientEndpoints();
        v1.MapProfessionalEndpoints();
        v1.MapAppointmentEndpoints();
    }

    private static void MapDocs(this WebApplication app)
    {
        app.MapOpenApi().WithDocumentPerVersion();
        app.MapScalarApiReference(DocsPath, options =>
        {
            options.Title = "Citas API";
            options.Telemetry = false;
        });

        app.MapGet("/", () => Results.Redirect(DocsPath)).ExcludeFromDescription();
    }

    // Health probes run every few seconds; keep them out of the log unless something goes wrong.
    private static LogEventLevel RequestLogLevel(HttpContext context, double elapsedMs, Exception? exception) =>
        exception is not null || context.Response.StatusCode >= StatusCodes.Status500InternalServerError
            ? LogEventLevel.Error
            : context.Request.Path.StartsWithSegments("/health", StringComparison.Ordinal)
                ? LogEventLevel.Verbose
                : LogEventLevel.Information;
}

using System.Text.Json.Serialization;
using Asp.Versioning;
using Citas.Api.Http;
using Citas.Api.OpenApi;
using Citas.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Citas.Api.Setup;

internal static class ApiServices
{
    public const string LiveTag = "live";
    public const string ReadyTag = "ready";

    public static IServiceCollection AddApi(this IServiceCollection services)
    {
        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(new ClinicDateTimeConverter());
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
        });

        // Always raise BadHttpRequestException (not only in Development) so BadRequestExceptionHandler answers every malformed request alike.
        services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);

        services.AddProblemDetails(options => options.CustomizeProblemDetails = ProblemDetailsSetup.Customize);
        services.AddExceptionHandler<BadRequestExceptionHandler>();

        services
            .AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1, 0);
                options.ReportApiVersions = true;
                options.ApiVersionReader = new UrlSegmentApiVersionReader();
            })
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'V";
                options.SubstituteApiVersionInUrl = true;
            })
            .AddOpenApi(options => options.AddCitasTransformers());

        services
            .AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), tags: [LiveTag])
            .AddDbContextCheck<CitasDbContext>("postgres", failureStatus: HealthStatus.Unhealthy, tags: [ReadyTag]);

        return services;
    }
}

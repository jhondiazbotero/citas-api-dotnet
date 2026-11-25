using Serilog;
using Serilog.Formatting.Compact;

namespace Citas.Api.Setup;

internal static class LoggingSetup
{
    private const string DevelopmentTemplate =
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}";

    /// <summary>
    /// Serilog as the logger. Levels come from the <c>Serilog</c> configuration section. Development writes readable text;
    /// every other environment writes one compact JSON object per line (what log collectors expect).
    /// Request and response bodies are never logged: they carry patient data.
    /// </summary>
    public static WebApplicationBuilder AddApiLogging(this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((context, services, logger) =>
        {
            logger
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext();

            if (context.HostingEnvironment.IsDevelopment())
            {
                logger.WriteTo.Console(outputTemplate: DevelopmentTemplate);
            }
            else
            {
                logger.WriteTo.Console(new RenderedCompactJsonFormatter());
            }
        });

        return builder;
    }
}

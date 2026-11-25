using Microsoft.AspNetCore.HttpOverrides;

namespace Citas.Api.Setup;

/// <summary>
/// Behind a reverse proxy or a load balancer the connection comes from the proxy, so the login limit and the logs would see
/// one address for everybody. With <c>ForwardedHeaders:Enabled</c> the API reads <c>X-Forwarded-For</c> and <c>X-Forwarded-Proto</c>.
/// Off by default: with no proxy in front, any client could write those headers and pick its own address.
/// </summary>
internal static class ForwardedHeadersSetup
{
    public const string EnabledKey = "ForwardedHeaders:Enabled";

    public static bool IsEnabled(IConfiguration configuration) => configuration.GetValue<bool>(EnabledKey);

    /// <summary>
    /// The proxy of a container deployment has no fixed address, so the usual allow-list (loopback only) is lifted. The limit of
    /// one forwarded hop stays: only the last entry of <c>X-Forwarded-For</c>, the one the nearest proxy appends, is believed, so a
    /// client cannot invent an address by writing the header itself. The API must not be reachable except through that proxy.
    /// </summary>
    public static IServiceCollection AddForwardedHeadersIfEnabled(this IServiceCollection services, IConfiguration configuration)
    {
        if (IsEnabled(configuration))
        {
            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                options.ForwardLimit = 1;
                options.KnownIPNetworks.Clear();
                options.KnownProxies.Clear();
            });
        }

        return services;
    }
}

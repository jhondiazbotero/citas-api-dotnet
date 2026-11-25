using Citas.Domain.Staff;
using Microsoft.AspNetCore.Authorization;

namespace Citas.Api.Security;

/// <summary>Named authorization policies: endpoints say which policy they need, never a loose list of roles.</summary>
internal static class Policies
{
    /// <summary>Administrators only.</summary>
    public const string Admin = "Admin";

    /// <summary>Administrators and reception: the front desk.</summary>
    public const string FrontDesk = "FrontDesk";

    /// <summary>Front desk, or the professional themselves (the route <c>id</c> is a professional): their agenda and their record.</summary>
    public const string OwnProfessional = "OwnProfessional";

    /// <summary>Front desk, or the professional who owns the appointment (the route <c>id</c> is an appointment).</summary>
    public const string OwnAppointment = "OwnAppointment";

    private static readonly string AdminRole = nameof(StaffRole.Admin);
    private static readonly string ReceptionRole = nameof(StaffRole.Reception);

    public static IServiceCollection AddCitasAuthorization(this IServiceCollection services)
    {
        services.AddScoped<IAuthorizationHandler, OwnResourceHandler>();
        services.AddAuthorizationBuilder()
            // No fallback policy on purpose: a route that does not exist must stay a plain 404. The test "EveryApiEndpoint..."
            // fails if an endpoint under /api says nothing about who may call it.
            .AddPolicy(Admin, policy => policy.RequireRole(AdminRole))
            .AddPolicy(FrontDesk, policy => policy.RequireRole(AdminRole, ReceptionRole))
            .AddPolicy(OwnProfessional, policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(new OwnResourceRequirement(OwnedResource.Professional)))
            .AddPolicy(OwnAppointment, policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(new OwnResourceRequirement(OwnedResource.Appointment)));
        return services;
    }

    /// <summary>What the docs say about who may call an endpoint, by policy.</summary>
    public static string Describe(string? policy) => policy switch
    {
        Admin => "Admin.",
        FrontDesk => "Admin or Reception.",
        OwnProfessional => "Admin or Reception, or the Professional the resource belongs to (any other professional gets 403).",
        OwnAppointment => "Admin or Reception, or the Professional who owns the appointment (any other professional gets 403).",
        _ => "any signed-in user.",
    };
}

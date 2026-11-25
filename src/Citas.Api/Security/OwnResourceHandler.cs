using System.Security.Claims;
using Citas.Application.Abstractions;
using Citas.Domain.Staff;
using Microsoft.AspNetCore.Authorization;

namespace Citas.Api.Security;

internal enum OwnedResource
{
    /// <summary>The route <c>id</c> is the id of a professional.</summary>
    Professional,

    /// <summary>The route <c>id</c> is the id of an appointment.</summary>
    Appointment,
}

internal sealed class OwnResourceRequirement(OwnedResource resource) : IAuthorizationRequirement
{
    public OwnedResource Resource { get; } = resource;
}

/// <summary>
/// The one place that decides "only their own": the front desk passes; a professional passes only when the resource
/// (their agenda, or an appointment of theirs) belongs to the professional named in their token. Anything else, including a
/// resource that does not exist, is refused the same way, so a professional cannot use it to discover other people's ids.
/// </summary>
internal sealed class OwnResourceHandler(IAppointmentRepository appointments) : AuthorizationHandler<OwnResourceRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, OwnResourceRequirement requirement)
    {
        ClaimsPrincipal user = context.User;

        if (user.IsInRole(nameof(StaffRole.Admin)) || user.IsInRole(nameof(StaffRole.Reception)))
        {
            context.Succeed(requirement);
            return;
        }

        if (!user.IsInRole(nameof(StaffRole.Professional))
            || context.Resource is not HttpContext http
            || !Guid.TryParse(user.FindFirstValue(JwtTokenIssuer.ProfessionalClaim), out Guid tokenProfessionalId)
            || !Guid.TryParse(http.GetRouteValue("id")?.ToString(), out Guid resourceId))
        {
            return;
        }

        Guid? ownerId = requirement.Resource switch
        {
            OwnedResource.Professional => resourceId,
            OwnedResource.Appointment => (await appointments.GetByIdAsync(resourceId, http.RequestAborted))?.ProfessionalId,
            _ => null,
        };

        if (ownerId == tokenProfessionalId)
        {
            context.Succeed(requirement);
        }
    }
}

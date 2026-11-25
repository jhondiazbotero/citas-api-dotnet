namespace Citas.Api.Security;

internal static class EndpointAccess
{
    /// <summary>Requires a policy and documents the 401 and 403 answers that come with it.</summary>
    public static RouteHandlerBuilder RequireAccess(this RouteHandlerBuilder builder, string policy) =>
        builder
            .RequireAuthorization(policy)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);
}

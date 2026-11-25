using Citas.Api.Contracts;
using Citas.Api.Http;
using Citas.Api.Security;
using Citas.Application.Auth;

namespace Citas.Api.Endpoints;

internal static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder api)
    {
        RouteGroupBuilder group = api.MapGroup("/auth").WithTags("Auth");

        group.MapPost("/token", LoginAsync)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitingSetup.LoginRateLimit)
            .WithName("Login")
            .WithSummary("Sign in")
            .WithDescription("Exchanges the email and password of a staff user for a bearer access token. Any wrong email, wrong password or deactivated user gets the same 401, so the answer does not reveal which one failed. At most 5 attempts per minute from one address: the sixth gets 429 with a `Retry-After` header. Patients do not sign in.")
            .Produces<LoginResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        return api;
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        LoginHandler handler,
        CancellationToken cancellationToken) =>
        (await handler.HandleAsync(new LoginCommand(request.Email, request.Password), cancellationToken)).ToOk();
}

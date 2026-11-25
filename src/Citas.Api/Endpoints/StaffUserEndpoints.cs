using Citas.Api.Contracts;
using Citas.Api.Http;
using Citas.Api.Security;
using Citas.Application.StaffUsers;

namespace Citas.Api.Endpoints;

internal static class StaffUserEndpoints
{
    public static RouteGroupBuilder MapStaffUserEndpoints(this RouteGroupBuilder api)
    {
        RouteGroupBuilder group = api.MapGroup("/staff-users").WithTags("Staff users");

        group.MapPost("/", CreateAsync)
            .RequireAccess(Policies.Admin)
            .WithName("CreateStaffUser")
            .WithSummary("Create a staff user")
            .WithDescription("Creates a Reception user or a Professional user (linked to an existing, active professional) with an email and a password of 12 to 256 characters. Administrators cannot be created here. A repeated email is rejected with 409. The response never includes the password or its hash.")
            .Produces<StaffUserResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return api;
    }

    private static async Task<IResult> CreateAsync(
        CreateStaffUserRequest request,
        CreateStaffUserHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new CreateStaffUserCommand(request.Email, request.Password, request.Role, request.ProfessionalId), cancellationToken);

        // There is no endpoint to read one staff user, so there is no Location to point at.
        return result.IsSuccess ? TypedResults.Json(result.Value, statusCode: StatusCodes.Status201Created) : new ProblemResult(result.Error);
    }
}

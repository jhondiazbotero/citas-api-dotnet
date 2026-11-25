using Citas.Api.Contracts;
using Citas.Api.Http;
using Citas.Application.Patients;
using Microsoft.AspNetCore.Mvc;

namespace Citas.Api.Endpoints;

internal static class PatientEndpoints
{
    private const string GetByIdRoute = "GetPatient";

    public static RouteGroupBuilder MapPatientEndpoints(this RouteGroupBuilder api)
    {
        RouteGroupBuilder group = api.MapGroup("/patients").WithTags("Patients");

        group.MapPost("/", RegisterAsync)
            .WithName("RegisterPatient")
            .WithSummary("Register a patient")
            .WithDescription("Creates a patient. The document number must be unique: a second patient with the same document is rejected with 409.")
            .Produces<PatientResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/{id}", GetAsync)
            .WithName(GetByIdRoute)
            .WithSummary("Get a patient")
            .WithDescription("Returns one patient by id.")
            .Produces<PatientResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return api;
    }

    private static async Task<IResult> RegisterAsync(
        RegisterPatientRequest request,
        RegisterPatientHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new RegisterPatientCommand(request.FullName, request.DocumentNumber, request.Phone), cancellationToken);
        return result.ToCreated(GetByIdRoute, patient => new { id = patient.Id });
    }

    private static async Task<IResult> GetAsync(
        [FromRoute] Guid id,
        GetPatientHandler handler,
        CancellationToken cancellationToken) =>
        (await handler.HandleAsync(new GetPatientQuery(id), cancellationToken)).ToOk();
}

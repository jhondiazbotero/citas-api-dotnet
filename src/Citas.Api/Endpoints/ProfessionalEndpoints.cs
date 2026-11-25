using System.ComponentModel;
using Citas.Api.Contracts;
using Citas.Api.Http;
using Citas.Api.Security;
using Citas.Application.Appointments;
using Citas.Application.Professionals;
using Microsoft.AspNetCore.Mvc;

namespace Citas.Api.Endpoints;

internal static class ProfessionalEndpoints
{
    private const string GetByIdRoute = "GetProfessional";
    private const int DefaultSlotMinutes = 30;

    public static RouteGroupBuilder MapProfessionalEndpoints(this RouteGroupBuilder api)
    {
        RouteGroupBuilder group = api.MapGroup("/professionals").WithTags("Professionals");

        group.MapPost("/", RegisterAsync)
            .RequireAccess(Policies.Admin)
            .WithName("RegisterProfessional")
            .WithSummary("Register a professional")
            .WithDescription("Creates a professional with the weekly windows in which they see patients (day of the week, from, to).")
            .Produces<ProfessionalResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapGet("/", ListAsync)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitingSetup.PublicReadRateLimit)
            .WithName("ListProfessionals")
            .WithSummary("List the active professionals")
            .WithDescription("Public list of the active professionals, by name: only `id`, `fullName` and `specialty`, so a visitor can find the id to ask for free times. No schedules and no inactive professionals. Limited to 60 requests per minute per address, shared with the free times: the next one gets 429 with a `Retry-After` header.")
            .Produces<IReadOnlyList<ProfessionalSummaryResponse>>()
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapGet("/{id}", GetAsync)
            .RequireAccess(Policies.OwnProfessional)
            .WithName(GetByIdRoute)
            .WithSummary("Get a professional")
            .WithDescription("Returns one professional with the weekly working hours. A professional can only read their own record.")
            .Produces<ProfessionalResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id}/working-hours", SetWorkingHoursAsync)
            .RequireAccess(Policies.Admin)
            .WithName("SetWorkingHours")
            .WithSummary("Replace the working hours")
            .WithDescription("Replaces the whole weekly schedule. Appointments that were already booked are not touched.")
            .Produces<ProfessionalResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/{id}/deactivate", DeactivateAsync)
            .RequireAccess(Policies.Admin)
            .WithName("DeactivateProfessional")
            .WithSummary("Deactivate a professional")
            .WithDescription("An inactive professional cannot receive new appointments. Repeating the call is harmless. Appointments already booked stay as they are.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{id}/agenda", GetAgendaAsync)
            .RequireAccess(Policies.OwnProfessional)
            .WithName("GetProfessionalAgenda")
            .WithSummary("Agenda of a professional")
            .WithDescription("Appointments that start between `from` and `to`, both days included, ordered by start. At most 31 days.")
            .Produces<IReadOnlyList<AppointmentResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{id}/available-slots", GetAvailableSlotsAsync)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitingSetup.PublicReadRateLimit)
            .WithName("GetAvailableSlots")
            .WithSummary("Free start times of a day")
            .WithDescription("Start times (every quarter hour) at which an appointment of `durationMinutes` can be booked on `date`, using the same rules as booking: working hours, existing appointments and the one-hour notice. An inactive professional has none. Limited to 60 requests per minute per address, shared with the list of professionals: the next one gets 429 with a `Retry-After` header.")
            .Produces<IReadOnlyList<SlotResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        return api;
    }

    private static async Task<IResult> RegisterAsync(
        RegisterProfessionalRequest request,
        RegisterProfessionalHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new RegisterProfessionalCommand(request.FullName, request.Specialty, request.WorkingHours), cancellationToken);
        return result.ToCreated(GetByIdRoute, professional => new { id = professional.Id });
    }

    private static async Task<IResult> ListAsync(
        ListActiveProfessionalsHandler handler,
        CancellationToken cancellationToken) =>
        (await handler.HandleAsync(cancellationToken)).ToOk();

    private static async Task<IResult> GetAsync(
        [FromRoute, Description("Professional id.")] Guid id,
        GetProfessionalHandler handler,
        CancellationToken cancellationToken) =>
        (await handler.HandleAsync(new GetProfessionalQuery(id), cancellationToken)).ToOk();

    private static async Task<IResult> SetWorkingHoursAsync(
        [FromRoute, Description("Professional id.")] Guid id,
        SetWorkingHoursRequest request,
        SetWorkingHoursHandler handler,
        CancellationToken cancellationToken) =>
        (await handler.HandleAsync(new SetWorkingHoursCommand(id, request.WorkingHours), cancellationToken)).ToOk();

    private static async Task<IResult> DeactivateAsync(
        [FromRoute, Description("Professional id.")] Guid id,
        DeactivateProfessionalHandler handler,
        CancellationToken cancellationToken) =>
        (await handler.HandleAsync(new DeactivateProfessionalCommand(id), cancellationToken)).ToNoContent();

    private static async Task<IResult> GetAgendaAsync(
        [FromRoute, Description("Professional id.")] Guid id,
        [FromQuery, Description("First day, yyyy-MM-dd.")] DateOnly from,
        [FromQuery, Description("Last day, yyyy-MM-dd. Not before `from`; at most 31 days in total.")] DateOnly to,
        GetProfessionalAgendaHandler handler,
        CancellationToken cancellationToken) =>
        (await handler.HandleAsync(new GetProfessionalAgendaQuery(id, from, to), cancellationToken)).ToOk();

    private static async Task<IResult> GetAvailableSlotsAsync(
        [FromRoute, Description("Professional id.")] Guid id,
        [FromQuery, Description("Day to look at, yyyy-MM-dd, in local clinic time.")] DateOnly date,
        GetAvailableSlotsHandler handler,
        CancellationToken cancellationToken,
        [FromQuery, Description("Length of the appointment in minutes: 15 to 120, in steps of 15. Defaults to 30.")]
        int durationMinutes = DefaultSlotMinutes) =>
        (await handler.HandleAsync(new GetAvailableSlotsQuery(id, date, durationMinutes), cancellationToken)).ToOk();
}

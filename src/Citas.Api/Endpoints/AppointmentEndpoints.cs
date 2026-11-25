using System.ComponentModel;
using Citas.Api.Contracts;
using Citas.Api.Http;
using Citas.Application.Appointments;
using Microsoft.AspNetCore.Mvc;

namespace Citas.Api.Endpoints;

internal static class AppointmentEndpoints
{
    private const string GetByIdRoute = "GetAppointment";

    public static RouteGroupBuilder MapAppointmentEndpoints(this RouteGroupBuilder api)
    {
        RouteGroupBuilder group = api.MapGroup("/appointments").WithTags("Appointments");

        group.MapPost("/", ScheduleAsync)
            .WithName("ScheduleAppointment")
            .WithSummary("Book an appointment")
            .WithDescription("Books an appointment if the professional is active and free, the time is inside their working hours, the patient has nothing else at that time and it starts at least one hour from now. Times are local clinic time, without zone or offset.")
            .Produces<AppointmentResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapGet("/{id}", GetAsync)
            .WithName(GetByIdRoute)
            .WithSummary("Get an appointment")
            .WithDescription("Returns one appointment with its current state: Scheduled, Cancelled, Completed or NoShow.")
            .Produces<AppointmentResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id}/reschedule", RescheduleAsync)
            .WithName("RescheduleAppointment")
            .WithSummary("Move an appointment")
            .WithDescription("Moves a scheduled appointment to a new time, with the same rules as booking. It needs at least 24 hours before the original start. The appointment may overlap its own previous time.")
            .Produces<AppointmentResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/{id}/cancel", CancelAsync)
            .WithName("CancelAppointment")
            .WithSummary("Cancel an appointment")
            .WithDescription("A patient must cancel at least 24 hours before the start; the clinic can cancel until the start (`byClinic: true`). A cancelled appointment frees its time.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/{id}/complete", CompleteAsync)
            .WithName("CompleteAppointment")
            .WithSummary("Mark as attended")
            .WithDescription("Marks a scheduled appointment as completed. It must have already started.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/{id}/no-show", NoShowAsync)
            .WithName("MarkAppointmentNoShow")
            .WithSummary("Mark as missed")
            .WithDescription("Marks a scheduled appointment as missed by the patient. It must have already started.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return api;
    }

    private static async Task<IResult> ScheduleAsync(
        ScheduleAppointmentRequest request,
        ScheduleAppointmentHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new ScheduleAppointmentCommand(
            request.ProfessionalId, request.PatientId, request.Start, request.DurationMinutes, request.Reason);
        var result = await handler.HandleAsync(command, cancellationToken);
        return result.ToCreated(GetByIdRoute, appointment => new { id = appointment.Id });
    }

    private static async Task<IResult> GetAsync(
        [FromRoute, Description("Appointment id.")] Guid id,
        GetAppointmentHandler handler,
        CancellationToken cancellationToken) =>
        (await handler.HandleAsync(new GetAppointmentQuery(id), cancellationToken)).ToOk();

    private static async Task<IResult> RescheduleAsync(
        [FromRoute, Description("Appointment id.")] Guid id,
        RescheduleAppointmentRequest request,
        RescheduleAppointmentHandler handler,
        CancellationToken cancellationToken) =>
        (await handler.HandleAsync(
            new RescheduleAppointmentCommand(id, request.NewStart, request.DurationMinutes), cancellationToken)).ToOk();

    private static async Task<IResult> CancelAsync(
        [FromRoute, Description("Appointment id.")] Guid id,
        CancelAppointmentRequest request,
        CancelAppointmentHandler handler,
        CancellationToken cancellationToken) =>
        (await handler.HandleAsync(new CancelAppointmentCommand(id, request.Reason, request.ByClinic), cancellationToken)).ToNoContent();

    private static async Task<IResult> CompleteAsync(
        [FromRoute, Description("Appointment id.")] Guid id,
        CompleteAppointmentHandler handler,
        CancellationToken cancellationToken) =>
        (await handler.HandleAsync(new CompleteAppointmentCommand(id), cancellationToken)).ToNoContent();

    private static async Task<IResult> NoShowAsync(
        [FromRoute, Description("Appointment id.")] Guid id,
        MarkNoShowHandler handler,
        CancellationToken cancellationToken) =>
        (await handler.HandleAsync(new MarkNoShowCommand(id), cancellationToken)).ToNoContent();
}

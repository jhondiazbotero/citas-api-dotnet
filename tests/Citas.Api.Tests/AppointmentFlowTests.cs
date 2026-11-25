using System.Net;
using System.Text.Json;
using Citas.Api.Tests.Support;
using Citas.Testing;

namespace Citas.Api.Tests;

public class AppointmentFlowTests(PostgresFixture postgres) : ApiTestBase(postgres)
{
    [Fact]
    public async Task Book_Reschedule_Cancel_KeepsTheFreeSlotsInSync()
    {
        Guid professionalId = await RegisterProfessionalAsync();
        Guid patientId = await RegisterPatientAsync();

        // 08:00-12:00 and 14:00-18:00 every quarter hour for 30-minute visits: 15 + 15 starts.
        string[] before = await FreeStartsAsync(professionalId, day: 3);
        Assert.Equal(30, before.Length);
        Assert.Contains(Local(3, 9), before);

        Guid appointmentId = await BookAsync(professionalId, patientId, Local(3, 9));

        string[] booked = await FreeStartsAsync(professionalId, day: 3);
        Assert.Equal(27, booked.Length);
        Assert.DoesNotContain(Local(3, 8, 45), booked);
        Assert.DoesNotContain(Local(3, 9), booked);
        Assert.DoesNotContain(Local(3, 9, 15), booked);
        Assert.Contains(Local(3, 8, 30), booked);
        Assert.Contains(Local(3, 9, 30), booked);

        using (var moved = await PostAsync($"{V1}/appointments/{appointmentId}/reschedule", new { newStart = Local(3, 10), durationMinutes = 30 }))
        {
            Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
            JsonElement body = await moved.ReadAsync();
            Assert.Equal(Local(3, 10), body.GetProperty("start").GetString());
            Assert.Equal("Scheduled", body.GetProperty("status").GetString());
        }

        string[] afterMove = await FreeStartsAsync(professionalId, day: 3);
        Assert.Contains(Local(3, 9), afterMove);
        Assert.DoesNotContain(Local(3, 10), afterMove);

        using (var cancelled = await PostAsync($"{V1}/appointments/{appointmentId}/cancel", new { reason = "El paciente no puede asistir" }))
        {
            Assert.Equal(HttpStatusCode.NoContent, cancelled.StatusCode);
        }

        string[] afterCancel = await FreeStartsAsync(professionalId, day: 3);
        Assert.Equal(30, afterCancel.Length);
        Assert.Contains(Local(3, 10), afterCancel);

        using var fetched = await GetAsync($"{V1}/appointments/{appointmentId}");
        JsonElement final = await fetched.ReadAsync();
        Assert.Equal("Cancelled", final.GetProperty("status").GetString());
        Assert.Equal("El paciente no puede asistir", final.GetProperty("cancellationReason").GetString());
    }

    [Fact]
    public async Task Creations_Return201WithALocationThatLeadsToTheResource()
    {
        using var professional = await PostAsync($"{V1}/professionals", new { fullName = "Dr. Pablo Prueba", specialty = "Odontología", workingHours = WeekdayHours() });
        using var patient = await PostAsync($"{V1}/patients", new { fullName = "Marta Prueba", documentNumber = "7654321" });

        Guid professionalId = await professional.ReadIdAsync();
        Guid patientId = await patient.ReadIdAsync();
        using var appointment = await ScheduleAsync(professionalId, patientId, Local(3, 9));
        Guid appointmentId = await appointment.ReadIdAsync();

        Assert.Equal($"{V1}/professionals/{professionalId}", AssertCreated(professional));
        Assert.Equal($"{V1}/patients/{patientId}", AssertCreated(patient));
        Assert.Equal($"{V1}/appointments/{appointmentId}", AssertCreated(appointment));

        foreach (string location in new[] { $"{V1}/professionals/{professionalId}", $"{V1}/patients/{patientId}", $"{V1}/appointments/{appointmentId}" })
        {
            using var response = await GetAsync(location);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    private static string AssertCreated(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        return response.Headers.Location!.AbsolutePath;
    }

    [Fact]
    public async Task Responses_CarryLocalClinicTimeWithoutZone()
    {
        Guid professionalId = await RegisterProfessionalAsync();
        Guid patientId = await RegisterPatientAsync();
        Guid appointmentId = await BookAsync(professionalId, patientId, Local(3, 9, 30), minutes: 45);

        using var response = await GetAsync($"{V1}/appointments/{appointmentId}");
        JsonElement body = await response.ReadAsync();

        Assert.Equal(Local(3, 9, 30), body.GetProperty("start").GetString());
        Assert.Equal(Local(3, 10, 15), body.GetProperty("end").GetString());
        Assert.Equal("2026-12-01T08:00:00", body.GetProperty("createdAt").GetString());
    }

    [Fact]
    public async Task Agenda_ListsTheAppointmentsOfTheRangeInOrder()
    {
        Guid professionalId = await RegisterProfessionalAsync();
        Guid patientId = await RegisterPatientAsync();
        Guid late = await BookAsync(professionalId, patientId, Local(4, 15));
        Guid early = await BookAsync(professionalId, patientId, Local(3, 9));
        await BookAsync(professionalId, patientId, Local(9, 9));

        using var response = await GetAsync($"{V1}/professionals/{professionalId}/agenda?from=2026-12-03&to=2026-12-04");
        JsonElement body = await response.ReadAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal([early, late], body.EnumerateArray().Select(a => a.GetProperty("id").GetGuid()));
    }

    [Fact]
    public async Task WorkingHours_CanBeReplacedAndTheSlotsFollow()
    {
        Guid professionalId = await RegisterProfessionalAsync();

        using var replaced = await Client.PutAsync(
            $"{V1}/professionals/{professionalId}/working-hours",
            new StringContent("""{"workingHours":[{"day":"Thursday","from":"10:00","to":"11:00"}]}""", System.Text.Encoding.UTF8, "application/json"),
            Cancellation);

        Assert.Equal(HttpStatusCode.OK, replaced.StatusCode);
        Assert.Equal(["2026-12-03T10:00:00", "2026-12-03T10:15:00", "2026-12-03T10:30:00"], await FreeStartsAsync(professionalId, day: 3));
        Assert.Empty(await FreeStartsAsync(professionalId, day: 2));
    }

    [Fact]
    public async Task Deactivated_Professional_HasNoSlotsAndCannotBeBooked()
    {
        Guid professionalId = await RegisterProfessionalAsync();
        Guid patientId = await RegisterPatientAsync();

        using (var deactivated = await PostAsync($"{V1}/professionals/{professionalId}/deactivate", new { }))
        {
            Assert.Equal(HttpStatusCode.NoContent, deactivated.StatusCode);
        }

        Assert.Empty(await FreeStartsAsync(professionalId, day: 3));
        using var booking = await ScheduleAsync(professionalId, patientId, Local(3, 9));
        await AssertProblemAsync(booking, HttpStatusCode.UnprocessableEntity, "Professional.Inactive");
    }

    [Fact]
    public async Task CompleteAndNoShow_WorkOnceTheAppointmentHasStarted()
    {
        Guid professionalId = await RegisterProfessionalAsync();
        Guid patientId = await RegisterPatientAsync();
        Guid first = await BookAsync(professionalId, patientId, Local(3, 9));
        Guid second = await BookAsync(professionalId, patientId, Local(3, 10));

        using (var tooEarly = await PostAsync($"{V1}/appointments/{first}/complete", new { }))
        {
            await AssertProblemAsync(tooEarly, HttpStatusCode.UnprocessableEntity, "Appointment.NotStartedYet");
        }

        Factory.Time.Advance(TimeSpan.FromDays(2.5)); // Thursday 2026-12-03 20:00 in the clinic

        using (var completed = await PostAsync($"{V1}/appointments/{first}/complete", new { }))
        {
            Assert.Equal(HttpStatusCode.NoContent, completed.StatusCode);
        }

        using (var missed = await PostAsync($"{V1}/appointments/{second}/no-show", new { }))
        {
            Assert.Equal(HttpStatusCode.NoContent, missed.StatusCode);
        }

        using (var again = await PostAsync($"{V1}/appointments/{first}/no-show", new { }))
        {
            await AssertProblemAsync(again, HttpStatusCode.UnprocessableEntity, "Appointment.NotScheduled");
        }

        using var fetched = await GetAsync($"{V1}/appointments/{first}");
        Assert.Equal("Completed", (await fetched.ReadAsync()).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Cancel_ByPatientInsideTheLast24Hours_IsRefusedButTheClinicCan()
    {
        Guid professionalId = await RegisterProfessionalAsync();
        Guid patientId = await RegisterPatientAsync();
        Guid appointmentId = await BookAsync(professionalId, patientId, Local(2, 9)); // tomorrow 09:00, 25 h away
        Factory.Time.Advance(TimeSpan.FromHours(2)); // now 10:00 Tuesday: 23 h left

        using (var byPatient = await PostAsync($"{V1}/appointments/{appointmentId}/cancel", new { reason = "Ya no puedo ir" }))
        {
            await AssertProblemAsync(byPatient, HttpStatusCode.UnprocessableEntity, "Appointment.CancellationTooLate");
        }

        using var byClinic = await PostAsync($"{V1}/appointments/{appointmentId}/cancel", new { reason = "El médico está enfermo", onBehalfOfPatient = false });
        Assert.Equal(HttpStatusCode.NoContent, byClinic.StatusCode);
    }
}

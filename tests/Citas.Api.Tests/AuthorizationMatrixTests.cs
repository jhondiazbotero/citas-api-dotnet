using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Citas.Api.Tests.Support;
using Citas.Domain.Staff;
using Citas.Testing;

namespace Citas.Api.Tests;

/// <summary>For every endpoint: who gets in (and gets the real answer), and who is stopped (401 without token, 403 with the wrong role).</summary>
public class AuthorizationMatrixTests(PostgresFixture postgres) : ApiTestBase(postgres)
{
    private const string Anon = "Anonymous";
    private const string Admin = "Admin";
    private const string Reception = "Reception";
    private const string Own = "ProfessionalOwner";
    private const string Other = "ProfessionalOther";

    private static readonly string[] AdminOnly = [Admin];
    private static readonly string[] FrontDesk = [Admin, Reception];
    private static readonly string[] OwnOrFrontDesk = [Admin, Reception, Own];
    private static readonly string[] Everybody = [Anon, Admin, Reception, Own, Other];
    private static readonly string[] Actors = Everybody;

    private sealed record Ids(Guid Professional, Guid OtherProfessional, Guid Patient, Guid Appointment);

    private sealed record Case(
        string Name,
        HttpMethod Method,
        Func<Ids, string> Url,
        Func<Ids, object?> Body,
        HttpStatusCode Success,
        string[] Allowed,
        bool NeedsStartedAppointment = false);

    private static object[] Hours() => WeekdayHours();

    private static readonly Case[] Cases =
    [
        new("POST professionals", HttpMethod.Post, _ => $"{V1}/professionals",
            _ => new { fullName = "Dr. Nuevo Prueba", specialty = "Pediatría", workingHours = Hours() }, HttpStatusCode.Created, AdminOnly),
        new("GET professionals", HttpMethod.Get, _ => $"{V1}/professionals", _ => null, HttpStatusCode.OK, Everybody),
        new("GET professionals/{id}", HttpMethod.Get, i => $"{V1}/professionals/{i.Professional}", _ => null, HttpStatusCode.OK, OwnOrFrontDesk),
        new("PUT professionals/{id}/working-hours", HttpMethod.Put, i => $"{V1}/professionals/{i.Professional}/working-hours",
            _ => new { workingHours = Hours() }, HttpStatusCode.OK, AdminOnly),
        new("POST professionals/{id}/deactivate", HttpMethod.Post, i => $"{V1}/professionals/{i.Professional}/deactivate", _ => new { }, HttpStatusCode.NoContent, AdminOnly),
        new("GET professionals/{id}/agenda", HttpMethod.Get, i => $"{V1}/professionals/{i.Professional}/agenda?from=2026-12-01&to=2026-12-07", _ => null, HttpStatusCode.OK, OwnOrFrontDesk),
        new("GET professionals/{id}/available-slots", HttpMethod.Get, i => $"{V1}/professionals/{i.Professional}/available-slots?date=2026-12-03", _ => null, HttpStatusCode.OK, Everybody),

        new("POST staff-users", HttpMethod.Post, _ => $"{V1}/staff-users",
            _ => new { email = "nueva.recepcion@tests.local", password = "Una-clave-larga-1", role = "Reception" }, HttpStatusCode.Created, AdminOnly),

        new("POST patients", HttpMethod.Post, _ => $"{V1}/patients",
            _ => new { fullName = "Marta Prueba", documentNumber = "7654321" }, HttpStatusCode.Created, FrontDesk),
        new("GET patients/{id}", HttpMethod.Get, i => $"{V1}/patients/{i.Patient}", _ => null, HttpStatusCode.OK, FrontDesk),

        new("POST appointments", HttpMethod.Post, _ => $"{V1}/appointments",
            i => new { professionalId = i.Professional, patientId = i.Patient, start = Local(4, 10), durationMinutes = 30, reason = "Control" }, HttpStatusCode.Created, FrontDesk),
        new("GET appointments/{id}", HttpMethod.Get, i => $"{V1}/appointments/{i.Appointment}", _ => null, HttpStatusCode.OK, FrontDesk),
        new("POST appointments/{id}/reschedule", HttpMethod.Post, i => $"{V1}/appointments/{i.Appointment}/reschedule",
            _ => new { newStart = Local(3, 10), durationMinutes = 30 }, HttpStatusCode.OK, FrontDesk),
        new("POST appointments/{id}/cancel", HttpMethod.Post, i => $"{V1}/appointments/{i.Appointment}/cancel",
            _ => new { reason = "Motivo de prueba" }, HttpStatusCode.NoContent, FrontDesk),
        new("POST appointments/{id}/complete", HttpMethod.Post, i => $"{V1}/appointments/{i.Appointment}/complete", _ => new { }, HttpStatusCode.NoContent, OwnOrFrontDesk, NeedsStartedAppointment: true),
        new("POST appointments/{id}/no-show", HttpMethod.Post, i => $"{V1}/appointments/{i.Appointment}/no-show", _ => new { }, HttpStatusCode.NoContent, OwnOrFrontDesk, NeedsStartedAppointment: true),
    ];

    public static TheoryData<string, string> Matrix()
    {
        var data = new TheoryData<string, string>();
        foreach (Case c in Cases)
        {
            foreach (string actor in Actors)
            {
                data.Add(c.Name, actor);
            }
        }

        return data;
    }

    [Fact]
    public void TheMatrixCoversEveryRouteOfTheApi()
    {
        // Keep this list and the OpenAPI document in step: a new endpoint must get a row in the matrix.
        Assert.Equal(16, Cases.Length);
        Assert.Equal(Cases.Length, Cases.Select(c => c.Name).Distinct().Count());
    }

    [Theory]
    [MemberData(nameof(Matrix))]
    public async Task Endpoint_AnswersByRole(string caseName, string actor)
    {
        Case endpoint = Cases.Single(c => c.Name == caseName);
        Ids ids = await PrepareAsync(endpoint.NeedsStartedAppointment);
        HttpClient client = ClientFor(actor, ids);
        using var request = new HttpRequestMessage(endpoint.Method, endpoint.Url(ids));
        if (endpoint.Body(ids) is { } body)
        {
            request.Content = JsonContent.Create(body, mediaType: null, Json.Options);
        }

        using HttpResponseMessage response = await client.SendAsync(request, Cancellation);

        if (endpoint.Allowed.Contains(actor))
        {
            Assert.Equal(endpoint.Success, response.StatusCode);
        }
        else if (actor == Anon)
        {
            await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "Auth.Unauthorized");
            Assert.Contains("Bearer", response.Headers.WwwAuthenticate.Select(h => h.Scheme));
        }
        else
        {
            await AssertProblemAsync(response, HttpStatusCode.Forbidden, "Auth.Forbidden");
        }
    }

    private HttpClient ClientFor(string actor, Ids ids) => actor switch
    {
        Anon => Anonymous,
        Admin => ClientAs(StaffRole.Admin),
        Reception => ClientAs(StaffRole.Reception),
        Own => ClientAs(StaffRole.Professional, ids.Professional),
        Other => ClientAs(StaffRole.Professional, ids.OtherProfessional),
        _ => throw new ArgumentOutOfRangeException(nameof(actor)),
    };

    private async Task<Ids> PrepareAsync(bool started)
    {
        Guid professional = await RegisterProfessionalAsync("Dra. Ana Prueba");
        Guid other = await RegisterProfessionalAsync("Dr. Otro Prueba");
        Guid patient = await RegisterPatientAsync();
        Guid appointment = await BookAsync(professional, patient, Local(3, 9));
        if (started)
        {
            Factory.Time.Advance(TimeSpan.FromDays(2.5)); // Thursday 2026-12-03 20:00 in the clinic: the 09:00 visit is over
        }

        return new Ids(professional, other, patient, appointment);
    }

    [Fact]
    public async Task The401And403_ExplainThemselves_WithoutNamingTheRoleThatWouldWork()
    {
        Ids ids = await PrepareAsync(started: false);
        string url = $"{V1}/professionals/{ids.Professional}/deactivate";
        using HttpClient reception = ClientAs(StaffRole.Reception);

        using var unauthorized = await Anonymous.PostAsync(url, null, Cancellation);
        using var forbidden = await reception.PostAsync(url, null, Cancellation);

        JsonElement a = await AssertProblemAsync(unauthorized, HttpStatusCode.Unauthorized, "Auth.Unauthorized");
        JsonElement f = await AssertProblemAsync(forbidden, HttpStatusCode.Forbidden, "Auth.Forbidden");
        Assert.Contains("token", a.GetProperty("detail").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("role", f.GetProperty("detail").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Admin", f.GetProperty("detail").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ADeniedCall_ChangesNothing()
    {
        Ids ids = await PrepareAsync(started: false);
        using HttpClient reception = ClientAs(StaffRole.Reception);
        using HttpClient professional = ClientAs(StaffRole.Professional, ids.Professional);

        using (var deactivate = await reception.PostAsJsonAsync($"{V1}/professionals/{ids.Professional}/deactivate", new { }, Json.Options, Cancellation))
        {
            Assert.Equal(HttpStatusCode.Forbidden, deactivate.StatusCode);
        }

        using (var cancel = await professional.PostAsJsonAsync($"{V1}/appointments/{ids.Appointment}/cancel", new { reason = "No debería poder" }, Json.Options, Cancellation))
        {
            Assert.Equal(HttpStatusCode.Forbidden, cancel.StatusCode);
        }

        using (var create = await reception.PostAsJsonAsync($"{V1}/professionals", new { fullName = "Dr. Colado Prueba", specialty = "X-Y", workingHours = Hours() }, Json.Options, Cancellation))
        {
            Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        }

        using var professionalNow = await GetAsync($"{V1}/professionals/{ids.Professional}");
        using var appointmentNow = await GetAsync($"{V1}/appointments/{ids.Appointment}");
        Assert.True((await professionalNow.ReadAsync()).GetProperty("isActive").GetBoolean());
        Assert.Equal("Scheduled", (await appointmentNow.ReadAsync()).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Professional_CompletesTheirOwnAppointment_NotSomeoneElses()
    {
        Ids ids = await PrepareAsync(started: true);
        using HttpClient other = ClientAs(StaffRole.Professional, ids.OtherProfessional);
        using HttpClient owner = ClientAs(StaffRole.Professional, ids.Professional);

        using (var denied = await other.PostAsJsonAsync($"{V1}/appointments/{ids.Appointment}/complete", new { }, Json.Options, Cancellation))
        {
            await AssertProblemAsync(denied, HttpStatusCode.Forbidden, "Auth.Forbidden");
        }

        using (var allowed = await owner.PostAsJsonAsync($"{V1}/appointments/{ids.Appointment}/complete", new { }, Json.Options, Cancellation))
        {
            Assert.Equal(HttpStatusCode.NoContent, allowed.StatusCode);
        }

        using var fetched = await GetAsync($"{V1}/appointments/{ids.Appointment}");
        Assert.Equal("Completed", (await fetched.ReadAsync()).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Professional_SeesTheirOwnAgendaOnly()
    {
        Ids ids = await PrepareAsync(started: false);
        using HttpClient owner = ClientAs(StaffRole.Professional, ids.Professional);

        using var own = await owner.GetAsync($"{V1}/professionals/{ids.Professional}/agenda?from=2026-12-01&to=2026-12-07", Cancellation);
        using var alien = await owner.GetAsync($"{V1}/professionals/{ids.OtherProfessional}/agenda?from=2026-12-01&to=2026-12-07", Cancellation);

        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        Assert.Equal(ids.Appointment, Assert.Single((await own.ReadAsync()).EnumerateArray()).GetProperty("id").GetGuid());
        await AssertProblemAsync(alien, HttpStatusCode.Forbidden, "Auth.Forbidden");
    }

    [Fact]
    public async Task Professional_SeesTheirOwnRecordOnly()
    {
        Ids ids = await PrepareAsync(started: false);
        using HttpClient owner = ClientAs(StaffRole.Professional, ids.Professional);

        using var own = await owner.GetAsync($"{V1}/professionals/{ids.Professional}", Cancellation);
        using var alien = await owner.GetAsync($"{V1}/professionals/{ids.OtherProfessional}", Cancellation);
        using var missing = await owner.GetAsync($"{V1}/professionals/{Guid.NewGuid()}", Cancellation);

        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        Assert.Equal(ids.Professional, (await own.ReadAsync()).GetProperty("id").GetGuid());
        await AssertProblemAsync(alien, HttpStatusCode.Forbidden, "Auth.Forbidden");
        await AssertProblemAsync(missing, HttpStatusCode.Forbidden, "Auth.Forbidden");
    }

    [Fact]
    public async Task Professional_AskingForAnAppointmentThatDoesNotExist_GetsTheSame403AsForSomeoneElses()
    {
        Ids ids = await PrepareAsync(started: true);
        using HttpClient owner = ClientAs(StaffRole.Professional, ids.Professional);
        using HttpClient admin = ClientAs(StaffRole.Admin);
        string missing = $"{V1}/appointments/{Guid.NewGuid()}/complete";

        using var asProfessional = await owner.PostAsJsonAsync(missing, new { }, Json.Options, Cancellation);
        using var asAdmin = await admin.PostAsJsonAsync(missing, new { }, Json.Options, Cancellation);

        // The professional cannot tell "not mine" from "does not exist"; the front desk gets the honest 404.
        await AssertProblemAsync(asProfessional, HttpStatusCode.Forbidden, "Auth.Forbidden");
        await AssertProblemAsync(asAdmin, HttpStatusCode.NotFound, "Appointment.NotFound");
    }

    [Fact]
    public async Task Professional_WithATokenThatLacksTheProfessionalClaim_IsRefusedOnOwnResources()
    {
        Ids ids = await PrepareAsync(started: true);
        string token = TestTokens.Mint(Factory.SigningKey, Factory.Time.GetUtcNow(), role: "Professional", professionalId: null);
        using var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        using var agenda = await client.GetAsync($"{V1}/professionals/{ids.Professional}/agenda?from=2026-12-01&to=2026-12-07", Cancellation);
        using var complete = await client.PostAsJsonAsync($"{V1}/appointments/{ids.Appointment}/complete", new { }, Json.Options, Cancellation);

        Assert.Equal(HttpStatusCode.Forbidden, agenda.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, complete.StatusCode);
    }

    [Fact]
    public async Task ATokenWithAnUnknownRole_GetsNoAccessToAnythingProtected()
    {
        Ids ids = await PrepareAsync(started: false);
        string token = TestTokens.Mint(Factory.SigningKey, Factory.Time.GetUtcNow(), role: "SuperAdmin");
        using var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        using var professional = await client.GetAsync($"{V1}/professionals/{ids.Professional}", Cancellation);
        using var patients = await client.GetAsync($"{V1}/patients/{ids.Patient}", Cancellation);
        using var agenda = await client.GetAsync($"{V1}/professionals/{ids.Professional}/agenda?from=2026-12-01&to=2026-12-07", Cancellation);

        Assert.Equal(HttpStatusCode.Forbidden, professional.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, patients.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, agenda.StatusCode);
    }

    [Fact]
    public async Task Cancel_OnBehalfOfThePatient_InsideTheLast24Hours_IsRefused_ButTheClinicCan()
    {
        Ids ids = await PrepareAsync(started: false);
        Factory.Time.Advance(TimeSpan.FromHours(36)); // Wednesday 20:00; the visit is Thursday 09:00: 13 h left
        using HttpClient reception = ClientAs(StaffRole.Reception);
        string url = $"{V1}/appointments/{ids.Appointment}/cancel";

        using (var implicitPatient = await reception.PostAsJsonAsync(url, new { reason = "Ya no puedo ir" }, Json.Options, Cancellation))
        {
            await AssertProblemAsync(implicitPatient, HttpStatusCode.UnprocessableEntity, "Appointment.CancellationTooLate");
        }

        using (var explicitPatient = await reception.PostAsJsonAsync(url, new { reason = "Ya no puedo ir", onBehalfOfPatient = true }, Json.Options, Cancellation))
        {
            await AssertProblemAsync(explicitPatient, HttpStatusCode.UnprocessableEntity, "Appointment.CancellationTooLate");
        }

        using (var byClinic = await reception.PostAsJsonAsync(url, new { reason = "El médico está enfermo", onBehalfOfPatient = false }, Json.Options, Cancellation))
        {
            Assert.Equal(HttpStatusCode.NoContent, byClinic.StatusCode);
        }
    }

    [Fact]
    public async Task Cancel_TheOldByClinicField_IsGone_AndCannotBeUsedToSkipTheRule()
    {
        Ids ids = await PrepareAsync(started: false);
        Factory.Time.Advance(TimeSpan.FromHours(36));

        using var response = await PostAsync($"{V1}/appointments/{ids.Appointment}/cancel", new { reason = "Ya no puedo ir", byClinic = true });

        await AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, "Appointment.CancellationTooLate");
    }

    [Fact]
    public void EveryApiEndpointStatesWhoMayCallIt()
    {
        var endpoints = Factory.Services
            .GetServices<Microsoft.AspNetCore.Routing.EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<Microsoft.AspNetCore.Routing.RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText?.StartsWith("/api/", StringComparison.Ordinal) == true)
            .ToList();

        Assert.NotEmpty(endpoints);
        var silent = endpoints
            .Where(e => !e.Metadata.OfType<Microsoft.AspNetCore.Authorization.IAuthorizeData>().Any()
                     && !e.Metadata.OfType<Microsoft.AspNetCore.Authorization.IAllowAnonymous>().Any())
            .Select(e => e.RoutePattern.RawText)
            .ToList();
        Assert.Empty(silent);

        string[] anonymous = [.. endpoints
            .Where(e => e.Metadata.OfType<Microsoft.AspNetCore.Authorization.IAllowAnonymous>().Any())
            .Select(e => e.RoutePattern.RawText!)
            .Order()];
        Assert.Equal(
            [
                "/api/v{version:apiVersion}/auth/token",
                "/api/v{version:apiVersion}/professionals/",
                "/api/v{version:apiVersion}/professionals/{id}/available-slots",
            ],
            anonymous);
    }
}

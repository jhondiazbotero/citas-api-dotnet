using System.Net;
using System.Text.Json;
using Citas.Api.Tests.Support;
using Citas.Application.Abstractions;
using Citas.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Citas.Api.Tests;

public class ErrorResponseTests(PostgresFixture postgres) : ApiTestBase(postgres)
{
    // ---- 404

    [Theory]
    [InlineData("patients", "Patient.NotFound")]
    [InlineData("professionals", "Professional.NotFound")]
    [InlineData("appointments", "Appointment.NotFound")]
    public async Task Get_OfAnUnknownId_Is404WithTheDomainCode(string resource, string code)
    {
        using var response = await GetAsync($"{V1}/{resource}/{Guid.NewGuid()}");

        JsonElement problem = await AssertProblemAsync(response, HttpStatusCode.NotFound, code);
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("detail").GetString()));
    }

    [Fact]
    public async Task Agenda_AndSlots_OfAnUnknownProfessional_Are404()
    {
        using var agenda = await GetAsync($"{V1}/professionals/{Guid.NewGuid()}/agenda?from=2026-12-03&to=2026-12-04");
        using var slots = await GetAsync($"{V1}/professionals/{Guid.NewGuid()}/available-slots?date=2026-12-03");

        await AssertProblemAsync(agenda, HttpStatusCode.NotFound, "Professional.NotFound");
        await AssertProblemAsync(slots, HttpStatusCode.NotFound, "Professional.NotFound");
    }

    [Fact]
    public async Task Booking_WithAnUnknownPatient_Is404()
    {
        Guid professionalId = await RegisterProfessionalAsync();

        using var response = await ScheduleAsync(professionalId, Guid.NewGuid(), Local(3, 9));

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "Patient.NotFound");
    }

    [Fact]
    public async Task AnUnknownRoute_Is404ProblemDetailsToo()
    {
        using var response = await GetAsync("/api/v1/nothing-here");

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "Http.NotFound");
    }

    // ---- 400

    [Fact]
    public async Task EmptyObject_IsAValidationError()
    {
        using var response = await PostRawAsync($"{V1}/patients", "{}");

        JsonElement problem = await AssertProblemAsync(response, HttpStatusCode.BadRequest, "Validation");
        Assert.Equal("Full name is required.", problem.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task InvalidField_Is400WithTheDomainCode()
    {
        using var response = await PostAsync($"{V1}/patients", new { fullName = "X", documentNumber = "1234567" });

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "Patient.InvalidName");
    }

    [Fact]
    public async Task DurationOutOfTheAllowedSteps_Is400()
    {
        Guid professionalId = await RegisterProfessionalAsync();
        Guid patientId = await RegisterPatientAsync();

        using var response = await ScheduleAsync(professionalId, patientId, Local(3, 9), minutes: 20);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "TimeRange.InvalidDuration");
    }

    [Theory]
    [InlineData("2026-12-03T09:00:00Z")]
    [InlineData("2026-12-03T09:00:00+00:00")]
    [InlineData("2026-12-03T09:00-05:00")]
    public async Task ADateWithZoneOrOffset_Is400WithAClearMessage(string start)
    {
        Guid professionalId = await RegisterProfessionalAsync();
        Guid patientId = await RegisterPatientAsync();

        using var response = await PostRawAsync(
            $"{V1}/appointments",
            $$"""{"professionalId":"{{professionalId}}","patientId":"{{patientId}}","start":"{{start}}","durationMinutes":30}""");

        JsonElement problem = await AssertProblemAsync(response, HttpStatusCode.BadRequest, "Request.InvalidDateTime");
        string detail = problem.GetProperty("detail").GetString()!;
        Assert.Contains("Send local clinic time without offset", detail, StringComparison.Ordinal);
        Assert.Contains("start", detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ADateInAnotherFormat_Is400()
    {
        using var response = await PostRawAsync($"{V1}/appointments/{Guid.NewGuid()}/reschedule", """{"newStart":"03/12/2026 09:00","durationMinutes":30}""");

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "Request.InvalidDateTime");
    }

    [Fact]
    public async Task MalformedJson_Is400_WithoutEchoingTheParserMessage()
    {
        using var response = await PostRawAsync($"{V1}/patients", "{ not json");

        JsonElement problem = await AssertProblemAsync(response, HttpStatusCode.BadRequest, "Request.InvalidBody");
        Assert.DoesNotContain("LineNumber", problem.GetProperty("detail").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task WrongTypeInsideTheBody_Is400()
    {
        using var response = await PostRawAsync($"{V1}/appointments", """{"professionalId":"not-a-guid"}""");

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "Request.InvalidBody");
    }

    [Fact]
    public async Task ADayOfTheWeekThatDoesNotExist_Is400()
    {
        using var response = await PostRawAsync(
            $"{V1}/professionals",
            """{"fullName":"Dra. Ana Prueba","specialty":"Medicina general","workingHours":[{"day":"Funday","from":"08:00","to":"12:00"}]}""");

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "Request.InvalidBody");
    }

    [Theory]
    [InlineData("agenda?from=hoy&to=2026-12-04")]
    [InlineData("agenda?to=2026-12-04")]
    [InlineData("available-slots?date=2026-13-45")]
    [InlineData("available-slots")]
    public async Task BadQueryParameters_Are400(string path)
    {
        Guid professionalId = await RegisterProfessionalAsync();

        using var response = await GetAsync($"{V1}/professionals/{professionalId}/{path}");

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "Request.InvalidParameter");
    }

    [Fact]
    public async Task AnIdThatIsNotAGuid_Is400()
    {
        using var response = await GetAsync($"{V1}/patients/12345");

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "Request.InvalidParameter");
    }

    [Fact]
    public async Task AgendaLongerThan31Days_Is400()
    {
        Guid professionalId = await RegisterProfessionalAsync();

        using var response = await GetAsync($"{V1}/professionals/{professionalId}/agenda?from=2026-12-01&to=2027-02-01");

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "Validation");
    }

    [Fact]
    public async Task ABodyThatIsNotJson_Is415()
    {
        using var response = await PostRawAsync($"{V1}/patients", "name=Luis", "text/plain");

        await AssertProblemAsync(response, HttpStatusCode.UnsupportedMediaType, "Request.UnsupportedMediaType");
    }

    // ---- 409

    [Fact]
    public async Task TwoPatientsWithTheSameDocument_Is409()
    {
        await RegisterPatientAsync("1234567");

        using var response = await PostAsync($"{V1}/patients", new { fullName = "Otra Persona", documentNumber = "1234567" });

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "Patient.DuplicateDocument");
    }

    [Fact]
    public async Task TwoAppointmentsOfTheSameProfessionalAtOverlappingTimes_Is409()
    {
        Guid professionalId = await RegisterProfessionalAsync();
        Guid patientId = await RegisterPatientAsync("1111111");
        Guid otherPatientId = await RegisterPatientAsync("2222222", "Otra Persona");
        await BookAsync(professionalId, patientId, Local(3, 9));

        using var response = await ScheduleAsync(professionalId, otherPatientId, Local(3, 9, 15));

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "Appointment.Overlap");
    }

    [Fact]
    public async Task ThePatientInTwoPlacesAtOnce_Is409()
    {
        Guid professionalId = await RegisterProfessionalAsync();
        Guid otherProfessionalId = await RegisterProfessionalAsync("Dr. Otro Prueba");
        Guid patientId = await RegisterPatientAsync();
        await BookAsync(professionalId, patientId, Local(3, 9));

        using var response = await ScheduleAsync(otherProfessionalId, patientId, Local(3, 9));

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "Appointment.PatientOverlap");
    }

    // ---- 422

    [Fact]
    public async Task BookingLessThanAnHourAhead_Is422()
    {
        Guid professionalId = await RegisterProfessionalAsync();
        Guid patientId = await RegisterPatientAsync();

        using var response = await ScheduleAsync(professionalId, patientId, Local(1, 8, 30)); // now is 08:00

        await AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, "Appointment.TooSoon");
    }

    [Fact]
    public async Task BookingOutsideTheWorkingHours_Is422()
    {
        Guid professionalId = await RegisterProfessionalAsync();
        Guid patientId = await RegisterPatientAsync();

        using var response = await ScheduleAsync(professionalId, patientId, Local(3, 13)); // lunch break

        await AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, "Appointment.OutsideWorkingHours");
    }
}

public class UnexpectedErrorTests(PostgresFixture postgres) : ApiTestBase(postgres)
{
    protected override CitasApiFactory CreateFactory(string connectionString) => new(
        connectionString,
        configureServices: services => services.AddScoped<IPatientRepository, ExplodingPatientRepository>());

    [Fact]
    public async Task AnUnexpectedException_Is500WithoutLeakingItsMessage()
    {
        using var response = await GetAsync($"{V1}/patients/{Guid.NewGuid()}");

        JsonElement problem = await AssertProblemAsync(response, HttpStatusCode.InternalServerError, "Server.UnexpectedError");
        string text = problem.GetRawText();
        Assert.DoesNotContain("super-secret-detail", text, StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ExplodingPatientRepository", text, StringComparison.Ordinal);
        Assert.DoesNotContain("InvalidOperationException", text, StringComparison.Ordinal);
        Assert.False(problem.TryGetProperty("detail", out _));
        Assert.False(problem.TryGetProperty("exception", out _));
        Assert.False(problem.TryGetProperty("stackTrace", out _));
    }

    private sealed class ExplodingPatientRepository : IPatientRepository
    {
        public Task<Citas.Domain.Patients.Patient?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("super-secret-detail: Host=db.internal;Password=hunter2");

        public Task<bool> ExistsByDocumentAsync(string documentNumber, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("super-secret-detail");

        public void Add(Citas.Domain.Patients.Patient patient) =>
            throw new InvalidOperationException("super-secret-detail");
    }
}

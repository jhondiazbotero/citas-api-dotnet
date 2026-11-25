using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Citas.Api.Tests.Support;
using Citas.Domain.Staff;
using Citas.Testing;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Citas.Api.Tests;

/// <summary>The administrator creates the rest of the staff through the API.</summary>
public class StaffUsersTests(PostgresFixture postgres) : ApiTestBase(postgres)
{
    private const string Password = "Una-clave-larga-1";
    private const string Url = $"{V1}/staff-users";

    private Task<HttpResponseMessage> CreateAsync(string email, string role = "Reception", string password = Password, Guid? professionalId = null, HttpClient? client = null) =>
        (client ?? Client).PostAsJsonAsync(Url, new { email, password, role, professionalId }, Json.Options, Cancellation);

    private async Task<HttpResponseMessage> SignInAsync(string email, string password) =>
        await Anonymous.PostAsJsonAsync($"{V1}/auth/token", new { email, password }, Json.Options, Cancellation);

    [Fact]
    public async Task Admin_CreatesAReceptionUser_WhoCanThenSignIn_AndTheAnswerHidesTheSecrets()
    {
        using var created = await CreateAsync("Recepcion.Nueva@Tests.Local");

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        string raw = await created.Content.ReadAsStringAsync(Cancellation);
        Assert.DoesNotContain(Password, raw, StringComparison.Ordinal);
        Assert.DoesNotContain("hash", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", raw, StringComparison.OrdinalIgnoreCase);
        JsonElement body = JsonDocument.Parse(raw).RootElement;
        Assert.Equal(["email", "id", "isActive", "professionalId", "role"], body.EnumerateObject().Select(p => p.Name).Order());
        Assert.Equal("recepcion.nueva@tests.local", body.GetProperty("email").GetString());
        Assert.Equal("Reception", body.GetProperty("role").GetString());
        Assert.True(body.GetProperty("isActive").GetBoolean());

        using var signIn = await SignInAsync("recepcion.nueva@tests.local", Password);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        Assert.Equal("Reception", (await signIn.ReadAsync()).GetProperty("role").GetString());
    }

    [Fact]
    public async Task Admin_CreatesAProfessionalUser_WhoOnlySeesTheirOwnRecord()
    {
        Guid own = await RegisterProfessionalAsync("Dra. Propia Prueba");
        Guid other = await RegisterProfessionalAsync("Dr. Ajeno Prueba");

        using var created = await CreateAsync("propia@tests.local", "Professional", professionalId: own);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(own, (await created.ReadAsync()).GetProperty("professionalId").GetGuid());

        using var signIn = await SignInAsync("propia@tests.local", Password);
        string token = (await signIn.ReadAsync()).GetProperty("accessToken").GetString()!;
        Assert.Equal(own.ToString(), new JsonWebTokenHandler().ReadJsonWebToken(token).GetClaim("professional_id").Value);

        using var asProfessional = Factory.CreateClient();
        asProfessional.DefaultRequestHeaders.Authorization = new("Bearer", token);
        using var mine = await asProfessional.GetAsync($"{V1}/professionals/{own}", Cancellation);
        using var theirs = await asProfessional.GetAsync($"{V1}/professionals/{other}", Cancellation);
        Assert.Equal(HttpStatusCode.OK, mine.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, theirs.StatusCode);
    }

    [Theory]
    [InlineData("duplicado@tests.local")]
    [InlineData("DUPLICADO@Tests.Local")]
    public async Task ARepeatedEmail_Answers409_WhateverTheCase(string second)
    {
        using (var first = await CreateAsync("duplicado@tests.local"))
        {
            Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        }

        using var repeated = await CreateAsync(second);

        await AssertProblemAsync(repeated, HttpStatusCode.Conflict, "StaffUser.DuplicateEmail");
    }

    [Fact]
    public async Task TwoRequestsWithTheSameEmailAtTheSameTime_LeaveOneUser_AndOneConflict()
    {
        Task<HttpResponseMessage>[] both = [CreateAsync("carrera@tests.local"), CreateAsync("carrera@tests.local")];

        HttpResponseMessage[] responses = await Task.WhenAll(both);

        try
        {
            Assert.Equal([HttpStatusCode.Created, HttpStatusCode.Conflict], responses.Select(r => r.StatusCode).Order());
            await AssertProblemAsync(responses.Single(r => r.StatusCode == HttpStatusCode.Conflict), HttpStatusCode.Conflict, "StaffUser.DuplicateEmail");
        }
        finally
        {
            foreach (HttpResponseMessage response in responses)
            {
                response.Dispose();
            }
        }
    }

    [Fact]
    public async Task AnAdminCannotBeCreatedHere()
    {
        using var response = await CreateAsync("otro.admin@tests.local", "Admin");

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "StaffUser.RoleNotAllowed");
        using var signIn = await SignInAsync("otro.admin@tests.local", Password);
        Assert.Equal(HttpStatusCode.Unauthorized, signIn.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("corta-11-ch")]
    public async Task ATooShortPassword_Answers400_AndCreatesNothing(string password)
    {
        using var response = await CreateAsync("clave.corta@tests.local", password: password);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "Validation");
        using var signIn = await SignInAsync("clave.corta@tests.local", password.Length == 0 ? "x" : password);
        Assert.Equal(HttpStatusCode.Unauthorized, signIn.StatusCode);
    }

    [Fact]
    public async Task APasswordOf256Characters_IsAccepted_AndOf257IsNot()
    {
        using var accepted = await CreateAsync("larga.uno@tests.local", password: new string('a', 256));
        using var refused = await CreateAsync("larga.dos@tests.local", password: new string('a', 257));

        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
        await AssertProblemAsync(refused, HttpStatusCode.BadRequest, "Validation");
    }

    [Fact]
    public async Task AProfessionalUser_NeedsAProfessional_ThatExistsAndIsActive()
    {
        Guid retired = await RegisterProfessionalAsync("Dr. Retirado Prueba");
        using (var deactivated = await PostAsync($"{V1}/professionals/{retired}/deactivate", new { }))
        {
            Assert.Equal(HttpStatusCode.NoContent, deactivated.StatusCode);
        }

        using var missingId = await CreateAsync("sin.id@tests.local", "Professional");
        using var unknown = await CreateAsync("desconocido@tests.local", "Professional", professionalId: Guid.NewGuid());
        using var inactive = await CreateAsync("inactivo@tests.local", "Professional", professionalId: retired);

        await AssertProblemAsync(missingId, HttpStatusCode.BadRequest, "StaffUser.ProfessionalRequired");
        await AssertProblemAsync(unknown, HttpStatusCode.NotFound, "Professional.NotFound");
        await AssertProblemAsync(inactive, HttpStatusCode.UnprocessableEntity, "Professional.Inactive");
    }

    [Fact]
    public async Task AReceptionUser_CannotBeLinkedToAProfessional()
    {
        Guid professional = await RegisterProfessionalAsync();

        using var response = await CreateAsync("enlazada@tests.local", "Reception", professionalId: professional);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "StaffUser.ProfessionalNotAllowed");
    }

    [Fact]
    public async Task AnUnknownRoleOrANumber_Answers400()
    {
        using var unknown = await Client.PostAsJsonAsync(Url, new { email = "x@tests.local", password = Password, role = "SuperAdmin" }, Json.Options, Cancellation);
        using var number = await Client.PostAsJsonAsync(Url, new { email = "y@tests.local", password = Password, role = 1 }, Json.Options, Cancellation);

        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, number.StatusCode);
    }

    [Fact]
    public async Task Reception_CannotCreateUsers()
    {
        using HttpClient reception = ClientAs(StaffRole.Reception);

        using var response = await CreateAsync("colado@tests.local", client: reception);

        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "Auth.Forbidden");
    }
}
